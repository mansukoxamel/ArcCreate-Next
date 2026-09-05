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
        public void IsSupportedDrop_AcceptsAffAndOggOnly(string name)
        {
            Assert.That(DirectFileProjectResolver.IsSupportedDrop(name), Is.True);
        }

        private string CreateFile(string name)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }
}
