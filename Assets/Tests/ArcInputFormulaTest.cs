using ArcCreate.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Unit
{
    public class ArcInputFormulaTest
    {
        [Test]
        public void InitialAcquisition_UsesLookaheadAndWiderXRange()
        {
            Assert.That(ArcFormula.ArcInputPositionTiming(1000, 2000, false), Is.EqualTo(1120));
            Assert.That(ArcFormula.ArcInputPositionTiming(1950, 2000, false), Is.EqualTo(2000));
            Assert.That(ArcFormula.ArcInputPositionTiming(1000, 2000, true), Is.EqualTo(1000));

            Assert.That(
                ArcFormula.IsWithinArcInputRange(new Vector2(3.019f, 0), Vector2.zero, false, Vector2.one),
                Is.True);
            Assert.That(
                ArcFormula.IsWithinArcInputRange(new Vector2(1.901f, 0), Vector2.zero, true, Vector2.one),
                Is.False);
        }

        [Test]
        public void InputRange_ExcludesNativeBoundaryValues()
        {
            Assert.That(
                ArcFormula.IsWithinArcInputRange(new Vector2(3.02f, 0), Vector2.zero, false, Vector2.one),
                Is.False);
            Assert.That(
                ArcFormula.IsWithinArcInputRange(new Vector2(0, 2.5f), Vector2.zero, true, Vector2.one),
                Is.False);
        }

        [Test]
        public void OppositeColorProximity_UsesStrictTwoWorldUnitDistance()
        {
            Assert.That(ArcFormula.AreArcsWithinIntersectionDistance(Vector2.zero, new Vector2(1.999f, 0)), Is.True);
            Assert.That(ArcFormula.AreArcsWithinIntersectionDistance(Vector2.zero, new Vector2(2, 0)), Is.False);
        }

        [Test]
        public void ReacquisitionLock_TruncatesAndCapsLikeNativeCode()
        {
            Assert.That(ArcFormula.CalculateArcLockDuration(123.4f), Is.EqualTo(493));
            Assert.That(ArcFormula.CalculateArcLockDuration(300), Is.EqualTo(1000));
        }

        [Test]
        public void MissDuration_UsesNormalAndDirectionChangeWindows()
        {
            Assert.That(ArcFormula.CalculateArcMissDuration(200, false), Is.EqualTo(400));
            Assert.That(ArcFormula.CalculateArcMissDuration(200, true), Is.EqualTo(100));
            Assert.That(ArcFormula.CalculateArcMissDuration(333.9f, false), Is.EqualTo(500));
            Assert.That(ArcFormula.CalculateArcMissDuration(333.9f, true), Is.EqualTo(166));
        }
    }
}
