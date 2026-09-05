using ArcCreate.Gameplay;
using NUnit.Framework;

namespace Tests.Unit
{
    public class CameraTiltTest
    {
        [TestCase(0f, 0.05f, 0.002f)]
        [TestCase(0.03f, -0.05f, 0.0268f)]
        [TestCase(-0.03f, 0.05f, -0.0268f)]
        [TestCase(0.0495f, 0.05f, 0.04952f)]
        public void CalculateCameraTilt_FollowingArc_MovesFourPercentPerUpdate(
            float currentTilt,
            float targetTilt,
            float expected)
        {
            Assert.That(
                ArcFormula.CalculateCameraTilt(currentTilt, targetTilt),
                Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(0.05f, 0.049f)]
        [TestCase(-0.05f, -0.049f)]
        [TestCase(0.0005f, 0.00049f)]
        [TestCase(0f, 0f)]
        public void CalculateCameraTilt_ReturningToCenter_MovesTwoPercentPerUpdate(
            float currentTilt,
            float expected)
        {
            Assert.That(
                ArcFormula.CalculateCameraTilt(currentTilt, 0),
                Is.EqualTo(expected).Within(0.000001f));
        }
    }
}
