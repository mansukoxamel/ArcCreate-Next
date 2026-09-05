using ArcCreate.Gameplay;
using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class ArcRenderingFormulaTest
    {
        [TestCase(500, 7)]
        [TestCase(999, 14)]
        [TestCase(1000, 7)]
        [TestCase(1001, 8)]
        public void CalculateArcSegmentCount_UsesOfficialDurationThreshold(
            int duration,
            int expected)
        {
            Assert.That(ArcFormula.CalculateArcSegmentCount(duration, 1), Is.EqualTo(expected));
        }

        [Test]
        public void CalculateArcSegmentProgress_UsesNormalizedStepAndAppendsEndpoint()
        {
            Assert.That(
                ArcFormula.CalculateArcSegmentProgress(500, 1, 0),
                Is.EqualTo(1f / 7f).Within(0.000001f));
            Assert.That(ArcFormula.CalculateArcSegmentProgress(500, 1, 6), Is.EqualTo(1));

            Assert.That(
                ArcFormula.CalculateArcSegmentProgress(501, 1, 6),
                Is.EqualTo(7f / 7.014f).Within(0.000001f));
            Assert.That(ArcFormula.CalculateArcSegmentProgress(501, 1, 7), Is.EqualTo(1));
        }

        [Test]
        public void CalculateArcSegmentProgress_ZeroResolutionDrawsOnlyEndpoint()
        {
            Assert.That(ArcFormula.CalculateArcSegmentCount(500, 0), Is.EqualTo(1));
            Assert.That(ArcFormula.CalculateArcSegmentProgress(500, 0, 0), Is.EqualTo(1));
        }

        [Test]
        public void CalculateArcSegmentCount_CustomResolutionScalesNormalizedCount()
        {
            Assert.That(ArcFormula.CalculateArcSegmentCount(500, 2), Is.EqualTo(14));
        }

        [Test]
        public void ArcWorldPosition_TruncatesAffEndpointsBeforeInterpolation()
        {
            Assert.That(
                ArcFormula.ArcXToWorld(0.1234f, 0.5009f, 0.5f, ArcLineType.S),
                Is.EqualTo(1.6f).Within(0.000001f));
            Assert.That(
                ArcFormula.ArcYToWorld(0.1234f, 0.8765f, 0.5f, ArcLineType.S),
                Is.EqualTo(3.245f).Within(0.000001f));
        }

        [Test]
        public void ArcWorldPosition_TruncatesNegativeInternalCoordinateTowardZero()
        {
            Assert.That(
                ArcFormula.ArcXToWorld(0.1234f, 0.1234f, 0, ArcLineType.S),
                Is.EqualTo(3.2f).Within(0.000001f));
        }
    }
}
