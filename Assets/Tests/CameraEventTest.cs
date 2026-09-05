using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class CameraEventTest
    {
        [TestCase(999, 0f)]
        [TestCase(1000, 0f)]
        [TestCase(1250, 0.25f)]
        [TestCase(1500, 0.5f)]
        [TestCase(1750, 0.75f)]
        [TestCase(2000, 1f)]
        [TestCase(2001, 1f)]
        public void PercentAt_SCamera_UsesLinearInterpolation(int timing, float expected)
        {
            CameraEvent camera = new CameraEvent
            {
                Timing = 1000,
                Duration = 1000,
                CameraType = CameraType.S,
            };

            Assert.That(camera.PercentAt(timing), Is.EqualTo(expected).Within(0.000001f));
        }
    }
}
