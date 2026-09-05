using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class ArcConnectionTest
    {
        [TestCase(-9, true)]
        [TestCase(9, true)]
        [TestCase(-10, false)]
        [TestCase(10, false)]
        public void IsConnected_UsesNativeTimingTolerance(int timingDifference, bool expected)
        {
            Arc first = CreateArc(endTiming: 1000);
            Arc second = CreateArc(startTiming: 1000 + timingDifference);

            Assert.That(ArcConnection.IsConnected(first, second), Is.EqualTo(expected));
        }

        [TestCase(0.099f, true)]
        [TestCase(0.1f, false)]
        [TestCase(0.11f, false)]
        public void IsConnected_UsesNativeXTolerance(float xDifference, bool expected)
        {
            Arc first = CreateArc(xEnd: 0.5f);
            Arc second = CreateArc(xStart: 0.5f + xDifference);

            Assert.That(ArcConnection.IsConnected(first, second), Is.EqualTo(expected));
        }

        [Test]
        public void IsConnected_RequiresExactYAndMatchingTraceType()
        {
            Arc first = CreateArc(yEnd: 0.5f);

            Assert.That(
                ArcConnection.IsConnected(first, CreateArc(yStart: 0.500001f)),
                Is.False);
            Assert.That(
                ArcConnection.IsConnected(first, CreateArc(isTrace: true)),
                Is.False);
        }

        [Test]
        public void IsConnected_DoesNotRequireMatchingColor()
        {
            Arc first = CreateArc(color: 0);
            Arc second = CreateArc(color: 1);

            Assert.That(ArcConnection.IsConnected(first, second), Is.True);
        }

        [Test]
        public void IsConnected_RejectsTheSameArcInstance()
        {
            Arc arc = CreateArc();

            Assert.That(ArcConnection.IsConnected(arc, arc), Is.False);
        }

        private static Arc CreateArc(
            int startTiming = 1000,
            int endTiming = 1000,
            float xStart = 0.5f,
            float xEnd = 0.5f,
            float yStart = 0.5f,
            float yEnd = 0.5f,
            int color = 0,
            bool isTrace = false)
        {
            return new Arc
            {
                Timing = startTiming,
                EndTiming = endTiming,
                XStart = xStart,
                XEnd = xEnd,
                YStart = yStart,
                YEnd = yEnd,
                Color = color,
                IsTrace = isTrace,
            };
        }
    }
}
