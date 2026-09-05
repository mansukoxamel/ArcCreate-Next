using System.Collections.Generic;
using ArcCreate.Gameplay.Data;

namespace ArcCreate.Gameplay.Chart
{
    public class ArcNoteGroup : LongNoteGroup<Arc>, IComparer<Arc>
    {
        public override void SetupNotes()
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                Arc arc = Notes[i];
                ChainArcIntoGroups(arc);
            }
        }

        public int Compare(Arc x, Arc y)
        {
            return x.CurrentDepth.CompareTo(y.CurrentDepth);
        }

        protected override void OnAdd(Arc note)
        {
            ChainArcIntoGroups(note);
        }

        protected override void OnUpdate(Arc note)
        {
            RemoveArcFromChainGroups(note);
            ChainArcIntoGroups(note);
        }

        protected override void OnRemove(Arc note)
        {
            RemoveArcFromChainGroups(note);
        }

        private void ChainArcIntoGroups(Arc arc)
        {
            foreach (Arc overlap in Services.Chart.FindByEndTiming<Arc>(
                arc.Timing - ArcConnection.TimingToleranceMs,
                arc.Timing + ArcConnection.TimingToleranceMs))
            {
                if (ArcConnection.IsConnected(overlap, arc))
                {
                    if (arc.PreviousArc == null
                     || arc.Color == overlap.Color)
                    {
                        arc.PreviousArc = overlap;
                    }

                    if (overlap.NextArc == null
                     || overlap.Color == arc.Color)
                    {
                        overlap.NextArc = arc;
                    }
                }
            }

            foreach (Arc overlap in Services.Chart.FindByTiming<Arc>(
                arc.EndTiming - ArcConnection.TimingToleranceMs,
                arc.EndTiming + ArcConnection.TimingToleranceMs))
            {
                if (ArcConnection.IsConnected(arc, overlap))
                {
                    if (arc.NextArc == null
                     || arc.Color == overlap.Color)
                    {
                        arc.NextArc = overlap;
                    }

                    if (overlap.PreviousArc == null
                     || overlap.Color == arc.Color)
                    {
                        overlap.PreviousArc = arc;
                    }
                }
            }
        }

        private void RemoveArcFromChainGroups(Arc arc)
        {
            if (arc.NextArc != null && arc.NextArc.PreviousArc == arc)
            {
                arc.NextArc.PreviousArc = null;
            }

            if (arc.PreviousArc != null && arc.PreviousArc.NextArc == arc)
            {
                arc.PreviousArc.NextArc = null;
            }

            arc.NextArc = null;
            arc.PreviousArc = null;
        }

    }
}
