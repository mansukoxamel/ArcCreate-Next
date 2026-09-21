using System.Linq;
using ArcCreate.Compose.MissLog;
using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class MissLogTest
    {
        // Rows are taken from real files written by Xynapse (M:\PORTAL\FROM_TABLET, 2026-09-21).
        private const string Header =
            "# Xynapse miss log v3\n"
            + "song: sayonarahatsukoi\n"
            + "title: Sayonara Hatsukoi\n"
            + "difficulty: FUTURE 7\n"
            + "date: 2026-09-21 17:43:51\n"
            + "speed: 100%\n"
            + "counts: FAR 0 (LATE 0 / EARLY 0), LOST 3\n"
            + "# some comment line\n"
            + "# note_ms\tmm:ss.mmm\tkind\ttiming\tdiff_ms\tnote_type\tlane\tjudged_ms\tchart_info\n";

        [Test]
        public void IsMissLog_DetectsTheHeader()
        {
            Assert.That(MissLogParser.IsMissLog("# Xynapse miss log v3"), Is.True);
            Assert.That(MissLogParser.IsMissLog("\uFEFF# Xynapse miss log v1"), Is.True);
            Assert.That(MissLogParser.IsMissLog("AudioOffset:0"), Is.False);
            Assert.That(MissLogParser.IsMissLog(null), Is.False);
        }

        [Test]
        public void Parse_ReadsHeaderAndRows()
        {
            MissLogFile file = MissLogParser.Parse(
                Header
                + "4044\t00:04.044\tLOST\t-\t-\tTap\t3\t4147\tlane 4 / lane 3\n"
                + "10181\t00:10.181\tFAR\tLATE\t+51\tArcTap\t-\t10232\ton red arc 9500-10500\n");

            Assert.That(file.Version, Is.EqualTo(3));
            Assert.That(file.SongId, Is.EqualTo("sayonarahatsukoi"));
            Assert.That(file.Title, Is.EqualTo("Sayonara Hatsukoi"));
            Assert.That(file.Difficulty, Is.EqualTo("FUTURE 7"));
            Assert.That(file.Speed, Is.EqualTo("100%"));
            Assert.That(file.Records.Count, Is.EqualTo(2));
            Assert.That(file.Records[0].Kind, Is.EqualTo(MissKind.Lost));
            Assert.That(file.Records[0].NoteType, Is.EqualTo(MissNoteType.Tap));
            Assert.That(file.Records[0].Lane, Is.EqualTo(3));
            Assert.That(file.Records[0].JudgedMs, Is.EqualTo(4147));
            Assert.That(file.Records[1].Kind, Is.EqualTo(MissKind.Far));
            Assert.That(file.Records[1].NoteType, Is.EqualTo(MissNoteType.ArcTap));
            Assert.That(file.Records[1].Lane, Is.EqualTo(0));
        }

        [Test]
        public void Parse_ReadsAnOlderVersionWithoutTheLaneColumn()
        {
            MissLogFile file = MissLogParser.Parse(
                "# Xynapse miss log v2\nsong: lucifer\n"
                + "# note_ms\tmm:ss.mmm\tkind\ttiming\tdiff_ms\tnote_type\tjudged_ms\tchart_info\n"
                + "13714\t00:13.714\tLOST\t-\t-\tHold\t14362\tlane 1 end=20142\n"
                + "13146\t00:13.146\tLOST\t-\t-\tTap\t13247\tlane 1 / lane 4\n");

            Assert.That(file.Records[0].Lane, Is.EqualTo(1));
            Assert.That(file.Records[1].Lane, Is.EqualTo(0), "two candidate lanes are not a lane");
        }

        [Test]
        public void Parse_ReportsTheLineOfABrokenRow()
        {
            MissLogFormatException error = Assert.Throws<MissLogFormatException>(() => MissLogParser.Parse(
                Header + "abc\t00:00.000\tLOST\t-\t-\tTap\t1\t0\t\n"));

            Assert.That(error.Message, Does.Contain("10行目"));
        }

        [Test]
        public void Parse_RejectsAFileThatIsNotAMissLog()
        {
            Assert.Throws<MissLogFormatException>(() => MissLogParser.Parse("AudioOffset:0\n-\n"));
        }

        [Test]
        public void Match_TellsSimultaneousTapsApartByLane()
        {
            Tap left = new Tap { Timing = 4044, Lane = 3 };
            Tap right = new Tap { Timing = 4044, Lane = 4 };
            MissLogFile file = MissLogParser.Parse(
                Header + "4044\t00:04.044\tLOST\t-\t-\tTap\t3\t4147\tlane 4 / lane 3\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new[] { left, right }, new Hold[0], new Arc[0], new ArcTap[0]);

            Assert.That(result.Unmatched, Is.Empty);
            Assert.That(result.AllNotes.ToArray(), Is.EqualTo(new Note[] { left }));
        }

        [Test]
        public void Match_FoldsTheTicksOfOneHoldIntoOneNote()
        {
            Hold hold = new Hold { Timing = 13714, EndTiming = 20142, Lane = 1 };
            MissLogFile file = MissLogParser.Parse(
                Header
                + "13714\t00:13.714\tLOST\t-\t-\tHold\t1\t14362\tlane 1 end=20142\n"
                + "13714\t00:13.714\tLOST\t-\t-\tHold\t1\t14571\tlane 1 end=20142\n"
                + "13714\t00:13.714\tLOST\t-\t-\tHold\t1\t18216\tlane 1 end=20142\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new Tap[0], new[] { hold }, new Arc[0], new ArcTap[0]);

            Assert.That(result.Missed.Count, Is.EqualTo(1));
            Assert.That(result.Missed[0].Records.Count, Is.EqualTo(3));
            Assert.That(result.Missed[0].Notes, Is.EqualTo(new Note[] { hold }));
        }

        [Test]
        public void Match_FindsAnArcByColourAndPosition()
        {
            Arc red = new Arc { Timing = 3857, EndTiming = 4928, XStart = 0.5f, XEnd = 1f, YStart = 1f, YEnd = 1f, Color = 1 };
            Arc blue = new Arc { Timing = 3857, EndTiming = 4928, XStart = 0.5f, XEnd = 1f, YStart = 1f, YEnd = 1f, Color = 0 };
            MissLogFile file = MissLogParser.Parse(
                Header + "3857\t00:03.857\tLOST\t-\t-\tArc\t-\t4929\tred x=0.50->1.00 y=1.00->1.00 end=4928\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new Tap[0], new Hold[0], new[] { blue, red }, new ArcTap[0]);

            Assert.That(result.AllNotes.ToArray(), Is.EqualTo(new Note[] { red }));
        }

        [Test]
        public void Match_FindsAnArcTapByTheArcItLiesOn()
        {
            Arc onRed = new Arc { Timing = 15857, EndTiming = 16714, Color = 1 };
            Arc onBlue = new Arc { Timing = 15857, EndTiming = 16714, Color = 0 };
            ArcTap wanted = new ArcTap { Timing = 16500, Arc = onRed };
            ArcTap other = new ArcTap { Timing = 16500, Arc = onBlue };
            MissLogFile file = MissLogParser.Parse(
                Header + "16500\t00:16.500\tLOST\t-\t-\tArc" + "Tap\t-\t16602\ton red arc 15857-16714\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new Tap[0], new Hold[0], new Arc[0], new[] { other, wanted });

            Assert.That(result.AllNotes.ToArray(), Is.EqualTo(new Note[] { wanted }));
        }

        [Test]
        public void Match_ReportsARowWhoseNoteIsNotInTheChart()
        {
            MissLogFile file = MissLogParser.Parse(
                Header + "9999\t00:09.999\tLOST\t-\t-\tTap\t2\t10100\tlane 2\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new[] { new Tap { Timing = 1000, Lane = 2 } }, new Hold[0], new Arc[0], new ArcTap[0]);

            Assert.That(result.Missed, Is.Empty);
            Assert.That(result.Unmatched.Count, Is.EqualTo(1));
        }

        [Test]
        public void Match_SortsTheMissedNotesByTime()
        {
            Tap early = new Tap { Timing = 1000, Lane = 1 };
            Tap late = new Tap { Timing = 5000, Lane = 2 };
            MissLogFile file = MissLogParser.Parse(
                Header
                + "5000\t00:05.000\tLOST\t-\t-\tTap\t2\t5100\tlane 2\n"
                + "1000\t00:01.000\tFAR\tLATE\t+50\tTap\t1\t1050\tlane 1\n");

            MissMatchResult result = MissLogMatcher.Match(
                file, new[] { late, early }, new Hold[0], new Arc[0], new ArcTap[0]);

            Assert.That(result.Missed.Select(m => m.Timing).ToArray(), Is.EqualTo(new[] { 1000, 5000 }));
        }
    }
}
