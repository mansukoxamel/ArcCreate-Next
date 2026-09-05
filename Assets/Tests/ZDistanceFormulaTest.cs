using System.Collections.Generic;
using ArcCreate.ChartFormat;
using ArcCreate.Gameplay;
using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class ZDistanceFormulaTest
    {
        private float originalBaseBpm;

        [SetUp]
        public void SetUp()
        {
            originalBaseBpm = Values.BaseBpm;
        }

        [TearDown]
        public void TearDown()
        {
            Values.BaseBpm = originalBaseBpm;
        }

        [Test]
        public void FloorPositionToZ_UsesNativeSpeedScaleWithoutProjectBaseBpm()
        {
            Values.BaseBpm = 100;
            float first = ArcFormula.FloorPositionToZ(120_000, 150f);

            Values.BaseBpm = 240;
            float second = ArcFormula.FloorPositionToZ(120_000, 150f);

            Assert.That(first, Is.EqualTo(-100).Within(0.000001f));
            Assert.That(second, Is.EqualTo(-100).Within(0.000001f));
        }

        [Test]
        public void ZToFloorPosition_IsInverseOfNativeDistanceFormula()
        {
            Assert.That(
                ArcFormula.ZToFloorPosition(-100, 150f),
                Is.EqualTo(120_000).Within(0.000001));
        }

        [Test]
        public void TimingGroupDistance_IntegratesEveryTimingIntervalAtSelectedSpeed()
        {
            TimingGroup group = new TimingGroup(1);
            group.Load(new ChartTimingGroup
            {
                Properties = new RawTimingGroup(),
                Timings = new List<TimingEvent>
                {
                    new TimingEvent { TimingGroup = 1, Timing = 0, Bpm = 120, Divisor = 4 },
                    new TimingEvent { TimingGroup = 1, Timing = 1000, Bpm = 240, Divisor = 4 },
                    new TimingEvent { TimingGroup = 1, Timing = 1500, Bpm = 60, Divisor = 4 },
                },
            });

            double floorDistance = group.GetFloorPosition(2000) - group.GetFloorPosition(0);
            float zDistance = ArcFormula.FloorPositionToZ(floorDistance, 135f);

            Assert.That(floorDistance, Is.EqualTo(270_000));
            Assert.That(zDistance, Is.EqualTo(-202.5f).Within(0.000001f));
        }
    }
}
