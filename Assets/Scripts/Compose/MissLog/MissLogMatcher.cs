using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ArcCreate.Gameplay.Data;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// One missed note of the chart: every log row that belongs to the same note is folded into it.
    /// </summary>
    public sealed class MissedNote
    {
        /// <summary>
        /// Gets the notes of the chart that this entry stands for. Several notes only when the log could
        /// not tell them apart (an old log without lanes, or an arc without colour information).
        /// </summary>
        public List<Note> Notes { get; } = new List<Note>();

        /// <summary>
        /// Gets the log rows folded into this entry.
        /// </summary>
        public List<MissRecord> Records { get; } = new List<MissRecord>();

        /// <summary>
        /// Gets or sets the chart time of the note, in milliseconds.
        /// </summary>
        public int Timing { get; set; }
    }

    /// <summary>
    /// The result of matching a miss log against a chart.
    /// </summary>
    public sealed class MissMatchResult
    {
        /// <summary>
        /// Gets the missed notes, sorted by chart time.
        /// </summary>
        public List<MissedNote> Missed { get; } = new List<MissedNote>();

        /// <summary>
        /// Gets the log rows for which the chart has no matching note.
        /// </summary>
        public List<MissRecord> Unmatched { get; } = new List<MissRecord>();

        /// <summary>
        /// Gets every note to select.
        /// </summary>
        public IEnumerable<Note> AllNotes => Missed.SelectMany(m => m.Notes).Distinct();
    }

    /// <summary>
    /// Finds the notes of a chart that a miss log refers to.
    /// A note is identified by its type, its start time and, for taps and holds, its lane (read from the note
    /// itself by Xynapse, so simultaneous taps are told apart); an arc by its colour and start / end position;
    /// an arc tap by the arc it lies on.
    /// </summary>
    public static class MissLogMatcher
    {
        private const float PositionTolerance = 0.011f;

        /// <summary>
        /// Matches the rows of a miss log against the notes of a chart.
        /// </summary>
        /// <param name="log">The parsed miss log.</param>
        /// <param name="taps">The tap notes of the chart.</param>
        /// <param name="holds">The hold notes of the chart.</param>
        /// <param name="arcs">The arcs of the chart.</param>
        /// <param name="arcTaps">The arc taps of the chart.</param>
        /// <returns>The missed notes and the rows that could not be matched.</returns>
        public static MissMatchResult Match(
            MissLogFile log,
            IEnumerable<Tap> taps,
            IEnumerable<Hold> holds,
            IEnumerable<Arc> arcs,
            IEnumerable<ArcTap> arcTaps)
        {
            MissMatchResult result = new MissMatchResult();
            Dictionary<string, MissedNote> byKey = new Dictionary<string, MissedNote>();
            Tap[] tapArray = taps.ToArray();
            Hold[] holdArray = holds.ToArray();
            Arc[] arcArray = arcs.ToArray();
            ArcTap[] arcTapArray = arcTaps.ToArray();

            foreach (MissRecord record in log.Records)
            {
                List<Note> candidates = FindCandidates(record, tapArray, holdArray, arcArray, arcTapArray);
                if (candidates.Count == 0)
                {
                    result.Unmatched.Add(record);
                    continue;
                }

                string key = string.Join(",", candidates.Select(n => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(n)).OrderBy(h => h));
                if (!byKey.TryGetValue(key, out MissedNote missed))
                {
                    missed = new MissedNote { Timing = record.NoteMs };
                    missed.Notes.AddRange(candidates);
                    byKey[key] = missed;
                    result.Missed.Add(missed);
                }

                missed.Records.Add(record);
            }

            result.Missed.Sort((a, b) => a.Timing.CompareTo(b.Timing));
            return result;
        }

        private static List<Note> FindCandidates(
            MissRecord record,
            Tap[] taps,
            Hold[] holds,
            Arc[] arcs,
            ArcTap[] arcTaps)
        {
            switch (record.NoteType)
            {
                case MissNoteType.Tap:
                    return taps
                        .Where(t => t.Timing == record.NoteMs && LaneMatches(t.Lane, record.Lane))
                        .Cast<Note>().ToList();
                case MissNoteType.Hold:
                    return holds
                        .Where(h => h.Timing == record.NoteMs && LaneMatches(h.Lane, record.Lane))
                        .Cast<Note>().ToList();
                case MissNoteType.Arc:
                    return FindArcs(record, arcs).Cast<Note>().ToList();
                case MissNoteType.ArcTap:
                    return FindArcTaps(record, arcTaps).Cast<Note>().ToList();
                default:
                    return new List<Note>();
            }
        }

        private static bool LaneMatches(float noteLane, int recordLane)
        {
            return recordLane <= 0 || Math.Abs(noteLane - recordLane) < 0.01f;
        }

        // The chart_info of an arc row is like "red x=0.50->1.00 y=1.00->1.00 end=4928".
        private static IEnumerable<Arc> FindArcs(MissRecord record, Arc[] arcs)
        {
            IEnumerable<Arc> found = arcs.Where(a => a.Timing == record.NoteMs);
            Match info = Regex.Match(
                record.ChartInfo,
                @"^(blue|red)\s+x=(-?[\d.]+)->(-?[\d.]+)\s+y=(-?[\d.]+)->(-?[\d.]+)\s+end=(\d+)");
            if (!info.Success)
            {
                return found;
            }

            int color = info.Groups[1].Value == "blue" ? 0 : 1;
            float xStart = ParseFloat(info.Groups[2].Value);
            float xEnd = ParseFloat(info.Groups[3].Value);
            int endTiming = int.Parse(info.Groups[6].Value, CultureInfo.InvariantCulture);
            return found.Where(a =>
                a.Color == color
                && a.EndTiming == endTiming
                && Math.Abs(a.XStart - xStart) < PositionTolerance
                && Math.Abs(a.XEnd - xEnd) < PositionTolerance);
        }

        // The chart_info of an arc tap row is like "on red arc 14142-15000" (the arc it lies on).
        private static IEnumerable<ArcTap> FindArcTaps(MissRecord record, ArcTap[] arcTaps)
        {
            IEnumerable<ArcTap> found = arcTaps.Where(t => t.Timing == record.NoteMs);
            Match info = Regex.Match(record.ChartInfo, @"^on (blue|red) arc (\d+)-(\d+)");
            if (!info.Success)
            {
                return found;
            }

            int color = info.Groups[1].Value == "blue" ? 0 : 1;
            int arcStart = int.Parse(info.Groups[2].Value, CultureInfo.InvariantCulture);
            int arcEnd = int.Parse(info.Groups[3].Value, CultureInfo.InvariantCulture);
            return found.Where(t =>
                t.Arc != null && t.Arc.Color == color && t.Arc.Timing == arcStart && t.Arc.EndTiming == arcEnd);
        }

        private static float ParseFloat(string text)
        {
            return float.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
