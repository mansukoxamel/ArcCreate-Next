using System;
using ArcCreate.Gameplay.Data;

namespace ArcCreate.Gameplay.Chart
{
    public static class ArcConnection
    {
        public const int TimingToleranceMs = 9;
        public const double XTolerance = 0.1;

        public static bool IsConnected(Arc first, Arc second)
        {
            return
                !ReferenceEquals(first, second)
             && Math.Abs(first.EndTiming - second.Timing) <= TimingToleranceMs
             && Math.Abs((double)first.XEnd - second.XStart) < XTolerance
             && first.YEnd == second.YStart
             && first.IsTrace == second.IsTrace;
        }

        public static bool HasDirectionChange(Arc first, Arc second)
        {
            return Math.Sign(first.XEnd - first.XStart) != Math.Sign(second.XEnd - second.XStart)
                || Math.Sign(first.YEnd - first.YStart) != Math.Sign(second.YEnd - second.YStart);
        }
    }
}
