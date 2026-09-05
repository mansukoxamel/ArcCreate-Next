using System;
using System.IO;
using ArcCreate.Compose.Project;
using NUnit.Framework;

namespace Tests.Unit
{
    public class DirectFileProjectResolverTest
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"ArcCreateNext-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [Test]
        public void ResolveAudioForChart_PrefersChartSpecificOgg()
        {
            string chart = CreateFile("3.aff");
            CreateFile("base.ogg");
            string specific = CreateFile("3.ogg");

            Assert.That(DirectFileProjectResolver.ResolveAudioForChart(chart), Is.EqualTo(specific));
        }

        [Test]
        public void ResolveAudioForChart_FallsBackToBaseOgg()
        {
            string chart = CreateFile("2.aff");
            string baseAudio = CreateFile("base.ogg");
            CreateFile("preview.ogg");

            Assert.That(DirectFileProjectResolver.ResolveAudioForChart(chart), Is.EqualTo(baseAudio));
        }

        [Test]
        public void FindChartsForAudio_BaseOggReturnsAllAffsInNameOrder()
        {
            string audio = CreateFile("base.ogg");
            string chart2 = CreateFile("2.aff");
            string chart0 = CreateFile("0.aff");
            string chart1 = CreateFile("1.aff");

            Assert.That(
                DirectFileProjectResolver.FindChartsForAudio(audio),
                Is.EqualTo(new[] { chart0, chart1, chart2 }));
        }

        [Test]
        public void FindChartsForAudio_ChartSpecificOggReturnsMatchingAffOnly()
        {
            string audio = CreateFile("3.ogg");
            CreateFile("2.aff");
            string chart3 = CreateFile("3.aff");

            Assert.That(DirectFileProjectResolver.FindChartsForAudio(audio), Is.EqualTo(new[] { chart3 }));
        }

        [TestCase("chart.mp3")]
        [TestCase("chart.wav")]
        [TestCase("chart.arcproj")]
        [TestCase("preview.ogg")]
        public void IsSupportedDrop_RejectsLegacyAndProjectFormats(string name)
        {
            Assert.That(DirectFileProjectResolver.IsSupportedDrop(name), Is.False);
        }

        [TestCase("chart.aff")]
        [TestCase("base.ogg")]
        [TestCase("CHART.AFF")]
        public void IsSupportedDrop_AcceptsAffAndOgg(string name)
        {
            Assert.That(DirectFileProjectResolver.IsSupportedDrop(name), Is.True);
        }

        [Test]
        public void IsSupportedDrop_AcceptsJpgButRejectsPng()
        {
            Assert.That(DirectFileProjectResolver.IsSupportedDrop("base.jpg"), Is.True);
            Assert.That(DirectFileProjectResolver.IsSupportedDrop("base.png"), Is.False);
        }

        [Test]
        public void IsSupportedDrop_AcceptsDirectory()
        {
            Assert.That(DirectFileProjectResolver.IsSupportedDrop(directory), Is.True);
        }

        [Test]
        public void FindLoadableChartsInDirectory_FiltersChartsWithoutAudio()
        {
            string chart0 = CreateFile("0.aff");
            string chart3 = CreateFile("3.aff");
            CreateFile("3.ogg");

            Assert.That(
                DirectFileProjectResolver.FindLoadableChartsInDirectory(directory),
                Is.EqualTo(new[] { chart3 }));
            Assert.That(File.Exists(chart0), Is.True);
        }

        [Test]
        public void ResolveJacketForChart_PrefersNormalChartThen1080ChartThenNormalBaseThen1080Base()
        {
            string chart = CreateFile("3.aff");
            string dropped = CreateFile("base_256.jpg");

            Assert.That(DirectFileProjectResolver.ResolveJacketForChart(chart, dropped), Is.EqualTo(dropped));

            string highResolutionBaseJacket = CreateFile("1080_base.jpg");
            Assert.That(
                DirectFileProjectResolver.ResolveJacketForChart(chart, dropped),
                Is.EqualTo(highResolutionBaseJacket));

            string baseJacket = CreateFile("base.jpg");
            Assert.That(DirectFileProjectResolver.ResolveJacketForChart(chart, dropped), Is.EqualTo(baseJacket));

            string highResolutionChartJacket = CreateFile("1080_3.jpg");
            Assert.That(
                DirectFileProjectResolver.ResolveJacketForChart(chart, dropped),
                Is.EqualTo(highResolutionChartJacket));

            string chartJacket = CreateFile("3.jpg");
            Assert.That(DirectFileProjectResolver.ResolveJacketForChart(chart, dropped), Is.EqualTo(chartJacket));
        }

        [Test]
        public void AreSameFile_IgnoresCaseAndRelativeSegments()
        {
            string first = Path.Combine(directory, "base.ogg");
            string second = Path.Combine(directory, ".", "BASE.OGG");
            Assert.That(DirectFileProjectResolver.AreSameFile(first, second), Is.True);
        }

        [Test]
        public void ResolveHistoryDirectory_UsesTheContainingFolderForFiles()
        {
            string chart = CreateFile("2.aff");

            Assert.That(DirectFileProjectResolver.ResolveHistoryDirectory(chart), Is.EqualTo(directory));
            Assert.That(DirectFileProjectResolver.ResolveHistoryDirectory(directory), Is.EqualTo(directory));
        }

        [Test]
        public void NormalizeHistoryDirectories_DeduplicatesFoldersAndRemovesMissingPaths()
        {
            string chart = CreateFile("2.aff");
            string audio = CreateFile("base.ogg");

            Assert.That(
                DirectFileProjectResolver.NormalizeHistoryDirectories(
                    new[] { chart, audio, Path.Combine(directory, "missing", "chart.aff") },
                    10),
                Is.EqualTo(new[] { directory }));
        }

        private string CreateFile(string name)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }
}
