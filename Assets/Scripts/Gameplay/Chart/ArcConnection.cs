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
    }
}
