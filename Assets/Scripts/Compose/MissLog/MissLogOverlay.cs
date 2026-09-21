using System.Linq;
using UnityEngine;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Shows the misses of the dropped miss log on the timeline strip.
    /// </summary>
    public static class MissLogOverlay
    {
        private static MissTickGraphic graphic;

        /// <summary>
        /// Creates the overlay on the timeline strip. Called once by the timeline when it starts.
        /// </summary>
        /// <param name="strip">The strip the timeline markers live on.</param>
        public static void Attach(RectTransform strip)
        {
            graphic = MissTickGraphic.Attach(strip);
        }

        /// <summary>
        /// Draws one line per missed note: red for a note with a LOST, yellow for a note with only FAR.
        /// </summary>
        /// <param name="result">The matched miss log.</param>
        public static void Show(MissMatchResult result)
        {
            if (graphic == null)
            {
                Debug.LogError("ミス記録の表示先（タイムライン）がありません。");
                return;
            }

            int offset = Gameplay.Values.ChartAudioOffset;
            graphic.SetTicks(
                result.Missed.Where(m => m.Records.Any(r => r.Kind == MissKind.Lost)).Select(m => m.Timing + offset),
                result.Missed.Where(m => m.Records.All(r => r.Kind == MissKind.Far)).Select(m => m.Timing + offset));
        }

        /// <summary>
        /// Removes the lines.
        /// </summary>
        public static void Clear()
        {
            graphic?.Clear();
        }
    }
}
