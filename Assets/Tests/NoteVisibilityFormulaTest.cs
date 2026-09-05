using ArcCreate.Gameplay;
using NUnit.Framework;

namespace Tests.Unit
{
    public class NoteVisibilityFormulaTest
    {
        [TestCase(-100, 0)]
        [TestCase(-90, 0)]
        [TestCase(-85, 0.5f)]
        [TestCase(-80, 1)]
        [TestCase(0, 1)]
        [TestCase(53.5f, 1)]
        public void ShortNoteAlpha_MatchesNativeFrontFade(float z, float expected)
        {
            Assert.That(ArcFormula.CalculateShortNoteAlpha(z), Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(-100, false, 1f / 3f)]
        [TestCase(-95, false, 1f / 3f)]
        [TestCase(-90, false, 2f / 3f)]
        [TestCase(-85, false, 1)]
        [TestCase(-90, true, 1f / 3f)]
        [TestCase(-85, true, 2f / 3f)]
        [TestCase(-80, true, 1)]
        public void ArcSegmentAlpha_MatchesNativeOpacityRamp(float z, bool isSlam, float expected)
        {
            Assert.That(
                ArcFormula.CalculateArcSegmentAlphaScalar(z, isSlam),
                Is.EqualTo(expected).Within(0.000001f));
        }
    }
}
