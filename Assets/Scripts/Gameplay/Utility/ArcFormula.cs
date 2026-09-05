using System.Collections.Generic;
using ArcCreate.Data;
using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Data;
using UnityEngine;

namespace ArcCreate.Gameplay
{
    public static class ArcFormula
    {
        private const float NativeZDistanceScale = 6000f * Constants.DropRateScalar;

        public static float ArcXToWorld(float x)
        {
            return (-Values.LaneWidth * 2 * x) + Values.LaneWidth;
        }

        public static float ArcYToWorld(float y)
        {
            return Values.ArcY0 + ((Values.ArcY1 - Values.ArcY0) * y);
        }

        public static float ArcXToWorld(float start, float end, float t, ArcLineType type)
        {
            float internalStart = (int)((850f * start) - 425f);
            float internalEnd = (int)((850f * end) - 425f);
            return -X(internalStart, internalEnd, t, type) / 100f;
        }

        public static float ArcYToWorld(float start, float end, float t, ArcLineType type)
        {
            float internalStart = (int)((450f * start) + 100f);
            float internalEnd = (int)((450f * end) + 100f);
            return Y(internalStart, internalEnd, t, type) / 100f;
        }

        public static float WorldXToArc(float x)
        {
            return (x - Values.LaneWidth) / -Values.LaneWidth / 2;
        }

        public static float WorldYToArc(float y)
        {
            return (y - Values.ArcY0) / (Values.ArcY1 - Values.ArcY0);
        }

        public static float LaneToWorldX(int lane)
        {
            return (-Values.LaneWidth * lane) + (Values.LaneWidth * 2.5f);
        }

        public static float LaneToWorldX(float lane)
        {
            return (-Values.LaneWidth * lane) + (Values.LaneWidth * 2.5f);
        }

        public static float LaneToArcX(int lane)
        {
            return (0.5f * lane) - 0.75f;
        }

        public static bool WithinRenderRange(float z)
        {
            return z >= -Values.TrackLengthForward && z <= Values.TrackLengthBackward;
        }

        public static float ArcXToLane(float x)
        {
            return (x + 0.75f) / 0.5f;
        }

        public static int WorldXToLane(float x)
        {
            return Mathf.RoundToInt((x - (Values.LaneWidth * 2.5f)) / -Values.LaneWidth);
        }

        public static double ZToFloorPosition(float z, int timingGroup) =>
            ZToFloorPosition(z, Services.Chart.GetTimingGroup(timingGroup).GroupProperties);

        public static double ZToFloorPosition(float z, GroupProperties groupProperties) =>
            ZToFloorPosition(z, float.IsNaN(groupProperties.DropRateSC) ? Settings.DropRate.Value : groupProperties.DropRateSC);

        public static double ZToFloorPosition(float z, float dropRate) => (double)(z / dropRate * -NativeZDistanceScale);

        public static float FloorPositionToZ(double fp, int timingGroup) =>
            FloorPositionToZ(fp, Services.Chart.GetTimingGroup(timingGroup).GroupProperties);

        public static float FloorPositionToZ(double fp, TimingGroup timingGroup) =>
            FloorPositionToZ(fp, timingGroup.GroupProperties);

        public static float FloorPositionToZ(double fp, GroupProperties groupProperties) => FloorPositionToZ(fp,
            float.IsNaN(groupProperties.DropRateSC) ? Settings.DropRate.Value : groupProperties.DropRateSC);

        public static float FloorPositionToZ(double fp, float dropRate) => (float)(fp * dropRate / -NativeZDistanceScale);

        public static float S(float start, float end, float t)
        {
            return ((1 - t) * start) + (end * t);
        }

        public static float O(float start, float end, float t)
        {
            return start + ((end - start) * (1 - Mathf.Cos(1.5707963f * t)));
        }

        public static float I(float start, float end, float t)
        {
            return start + ((end - start) * Mathf.Sin(1.5707963f * t));
        }

        public static float B(float start, float end, float t)
        {
            float o = 1 - t;
            return (Mathf.Pow(o, 3) * start)
                 + (3 * Mathf.Pow(o, 2) * t * start)
                 + (3 * o * Mathf.Pow(t, 2) * end)
                 + (Mathf.Pow(t, 3) * end);
        }

        public static float X(float start, float end, float t, ArcLineType type)
        {
            switch (type)
            {
                default:
                case ArcLineType.S:
                    return S(start, end, t);
                case ArcLineType.B:
                    return B(start, end, t);
                case ArcLineType.Si:
                case ArcLineType.SiSi:
                case ArcLineType.SiSo:
                    return I(start, end, t);
                case ArcLineType.So:
                case ArcLineType.SoSi:
                case ArcLineType.SoSo:
                    return O(start, end, t);
            }
        }

        public static float Y(float start, float end, float t, ArcLineType type)
        {
            switch (type)
            {
                default:
                case ArcLineType.S:
                case ArcLineType.Si:
                case ArcLineType.So:
                    return S(start, end, t);
                case ArcLineType.B:
                    return B(start, end, t);
                case ArcLineType.SiSi:
                case ArcLineType.SoSi:
                    return I(start, end, t);
                case ArcLineType.SiSo:
                case ArcLineType.SoSo:
                    return O(start, end, t);
            }
        }

        public static float Qi(float value)
        {
            return value * value * value;
        }

        public static float Qo(float value)
        {
            value--;
            return (value * value * value) + 1;
        }

        public static float CalculateCameraTilt(float currentTilt, float targetTilt)
        {
            float factor = targetTilt == 0
                ? Values.CameraTiltReturnFactor
                : Values.CameraTiltFollowFactor;
            return currentTilt + ((targetTilt - currentTilt) * factor);
        }

        public static List<int> CalculateLongNoteJudgeTimings(int from, int to, float bpm)
        {
            List<int> result = new List<int>();

            int u = 0;
            bpm = Mathf.Abs(bpm);
            float interval = 60000f / bpm / (bpm >= 255 ? 1 : 2) / Values.TimingPointDensity;
            int total = (int)((to - from) / interval);
            if ((u ^ 1) >= total)
            {
                result.Add((int)(from + ((to - from) * 0.5f)));
                return result;
            }

            int n = u ^ 1;
            while (true)
            {
                int t = (int)(from + (n * interval));
                if (t < to)
                {
                    result.Add(t);
                }

                if (total == ++n)
                {
                    break;
                }
            }

            return result;
        }

        public static float CalculateTapSizeScalar(float z)
        {
            if (z <= 0)
            {
                return Mathf.Abs(1.5f + (6.25f * -z / Values.TrackLengthForward));
            }

            return Mathf.Abs(1.5f + (7.25f * z / Values.TrackLengthBackward));
        }

        public static float CalculateBeatlineSizeScalar(float thickness, float z)
        {
            if (z <= 0)
            {
                return Mathf.Abs(thickness + (thickness * 3 * -z / Values.TrackLengthForward));
            }

            return Mathf.Abs(thickness + (thickness * 3 * z / Values.TrackLengthBackward));
        }

        public static float CalculateShortNoteAlpha(float z)
        {
            return Mathf.Clamp01(
                (z - Values.ShortNoteFadeStartZ)
                / (Values.ShortNoteFadeEndZ - Values.ShortNoteFadeStartZ));
        }

        public static float CalculateArcSegmentAlphaScalar(float z, bool isSlam)
        {
            float fadeStart = isSlam ? Values.SlamArcFadeStartZ : Values.LongArcFadeStartZ;
            float progress = Mathf.Clamp01((z - fadeStart) / Values.ArcFadeLength);
            return Mathf.Lerp(Values.MinArcAlphaScalar, 1, progress);
        }

        public static int CalculateArcLockDuration(float arcJudgeInterval)
        {
            return (int)Mathf.Min(arcJudgeInterval * 4, 1000);
        }

        public static int CalculateArcMissDuration(float arcJudgeInterval, bool shortened)
        {
            float multiplier = shortened ? 0.5f : 2;
            return (int)Mathf.Min(arcJudgeInterval * multiplier, 500);
        }

        public static int ArcInputPositionTiming(int currentTiming, int endTiming, bool retained)
        {
            return retained ? currentTiming : Mathf.Min(currentTiming + Values.ArcInitialLookahead, endTiming);
        }

        public static bool IsWithinArcInputRange(
            Vector2 touchPosition,
            Vector2 arcPosition,
            bool retained,
            Vector2 judgementSize)
        {
            float hitboxX = retained ? Values.ArcHitboxX : Values.ArcInitialHitboxX;
            return Mathf.Abs(touchPosition.x - arcPosition.x) < hitboxX * judgementSize.x
                && Mathf.Abs(touchPosition.y - arcPosition.y) < Values.ArcHitboxY * judgementSize.y;
        }

        public static bool AreArcsWithinIntersectionDistance(Vector2 first, Vector2 second)
        {
            return (first - second).sqrMagnitude
                < Values.ArcIntersectionDistance * Values.ArcIntersectionDistance;
        }

        public static int CalculateArcSegmentCount(int duration, float arcResolution)
        {
            float normalizedSegmentCount = CalculateArcNormalizedSegmentCount(duration, arcResolution);
            return Mathf.Max(Mathf.CeilToInt(normalizedSegmentCount), 1);
        }

        public static float CalculateArcSegmentProgress(int duration, float arcResolution, int segmentIndex)
        {
            float normalizedSegmentCount = CalculateArcNormalizedSegmentCount(duration, arcResolution);
            if (normalizedSegmentCount <= 0)
            {
                return 1;
            }

            return Mathf.Min((segmentIndex + 1) / normalizedSegmentCount, 1);
        }

        public static float CalculateArcSegmentInterval(int duration, float arcResolution)
        {
            float normalizedSegmentCount = CalculateArcNormalizedSegmentCount(duration, arcResolution);
            return normalizedSegmentCount <= 0 ? Mathf.Max(duration, 1) : duration / normalizedSegmentCount;
        }

        private static float CalculateArcNormalizedSegmentCount(int duration, float arcResolution)
        {
            if (duration <= 0 || arcResolution <= 0)
            {
                return 0;
            }

            float segmentsPerSecond = duration < 1000 ? 14f : 7f;
            return duration / 1000f * segmentsPerSecond * arcResolution;
        }
    }
}
