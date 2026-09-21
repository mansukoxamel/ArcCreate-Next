using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Compose.MissLog
{
    /// <summary>
    /// Draws one coloured vertical line on the timeline for every miss of the dropped miss log,
    /// so the places where the player missed can be seen without moving through the song.
    /// The lines follow the timeline's zoom and scroll.
    /// </summary>
    public class MissTickGraphic : MaskableGraphic
    {
        private const float TickWidth = 4f;

        private readonly List<Tick> ticks = new List<Tick>();
        private int lastViewFrom = int.MinValue;
        private int lastViewTo = int.MinValue;

        /// <summary>
        /// Creates the graphic as a full-size child of a timeline strip.
        /// </summary>
        /// <param name="parent">The strip the lines are drawn on.</param>
        /// <returns>The new graphic.</returns>
        public static MissTickGraphic Attach(RectTransform parent)
        {
            GameObject holder = new GameObject("MissTicks", typeof(RectTransform));
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            MissTickGraphic graphic = holder.AddComponent<MissTickGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        /// <summary>
        /// Replaces the lines. Times are audio times in milliseconds.
        /// </summary>
        /// <param name="lost">The times of the lost notes, drawn in red.</param>
        /// <param name="far">The times of the FAR notes, drawn in yellow.</param>
        public void SetTicks(IEnumerable<int> lost, IEnumerable<int> far)
        {
            ticks.Clear();
            foreach (int timing in far)
            {
                ticks.Add(new Tick { Timing = timing, Color = new Color32(255, 214, 0, 255) });
            }

            foreach (int timing in lost)
            {
                ticks.Add(new Tick { Timing = timing, Color = new Color32(255, 40, 40, 255) });
            }

            lastViewFrom = int.MinValue;
            SetVerticesDirty();
        }

        /// <summary>
        /// Removes every line.
        /// </summary>
        public void Clear()
        {
            ticks.Clear();
            SetVerticesDirty();
        }

        /// <inheritdoc/>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (ticks.Count == 0 || Services.Timeline == null)
            {
                return;
            }

            int from = Services.Timeline.ViewFromTiming;
            int to = Services.Timeline.ViewToTiming;
            if (to <= from)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            foreach (Tick tick in ticks)
            {
                if (tick.Timing < from || tick.Timing > to)
                {
                    continue;
                }

                float x = rect.xMin + (rect.width * (tick.Timing - from) / (to - from));
                int start = vh.currentVertCount;
                vh.AddVert(new Vector3(x - (TickWidth / 2), rect.yMin), tick.Color, Vector2.zero);
                vh.AddVert(new Vector3(x - (TickWidth / 2), rect.yMax), tick.Color, Vector2.zero);
                vh.AddVert(new Vector3(x + (TickWidth / 2), rect.yMax), tick.Color, Vector2.zero);
                vh.AddVert(new Vector3(x + (TickWidth / 2), rect.yMin), tick.Color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }

        private void Update()
        {
            if (ticks.Count == 0 || Services.Timeline == null)
            {
                return;
            }

            int from = Services.Timeline.ViewFromTiming;
            int to = Services.Timeline.ViewToTiming;
            if (from != lastViewFrom || to != lastViewTo)
            {
                lastViewFrom = from;
                lastViewTo = to;
                SetVerticesDirty();
            }
        }

        private struct Tick
        {
            public int Timing;
            public Color32 Color;
        }
    }
}
