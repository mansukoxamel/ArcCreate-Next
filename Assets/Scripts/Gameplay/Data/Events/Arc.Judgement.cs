using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Judgement;
using UnityEngine;

namespace ArcCreate.Gameplay.Data
{
    /// <summary>
    /// Partial class for judgement.
    /// </summary>
    public partial class Arc : LongNote, ILongNote, IArcJudgementReceiver
    {
        private int numJudgementRequestsSent = 0;
        private bool highlightRequestSent = false;
        private bool spawnedParticleThisFrame = false;

        public void ResetJudgeTo(int timing)
        {
            RecalculateJudgeTimings();
            highlight = highlight && timing >= Timing && timing <= EndTiming;
            longParticleUntil = int.MinValue;
            numJudgementRequestsSent = ComboAt(timing);
            highlightRequestSent = false;
            arcGroupAlpha = 1;
            hasBeenHitOnce = hasBeenHitOnce && timing >= Timing && timing <= EndTiming;
            for (int i = 0; i < segments.Count; i++)
            {
                ArcSegmentData segment = segments[i];
                segment.From = 0;
                segments[i] = segment;
            }
        }

        public override void RecalculateJudgeTimings()
        {
            TotalCombo = 0;
            FirstJudgeTime = double.MaxValue;
            TimeIncrement = double.MaxValue;

            if (IsTrace || EndTiming == Timing)
            {
                return;
            }

            double bpm = TimingGroupInstance.GetBpm(Timing);

            if (bpm == 0)
            {
                return;
            }

            int duration = EndTiming - Timing;
            bpm = System.Math.Abs(bpm);
            TimeIncrement = (bpm >= 255 ? 60_000 : 30_000) / bpm / Values.TimingPointDensity;

            int totalCombo = (int)(duration / TimeIncrement);
            int comboModifier = (IsFirstArcOfGroup ? 0 : 1) ^ 1;
            if (totalCombo <= comboModifier)
            {
                TotalCombo = 1;
                FirstJudgeTime = Timing + (duration / 2);
            }
            else
            {
                TotalCombo = totalCombo - comboModifier;
                FirstJudgeTime = Timing + (comboModifier * TimeIncrement);
            }
        }

        public void UpdateJudgement(int currentTiming, GroupProperties groupProperties)
        {
            int timing = groupProperties.EarlyJudgement ? Timing - Values.PerfectJudgeWindow : Timing;
            if (!IsTrace && currentTiming >= timing && Timing < EndTiming)
            {
                RequestJudgement(groupProperties);
            }
            if (!IsTrace && currentTiming >= Timing && Timing < EndTiming && !highlightRequestSent)
            {
                RequestHighlight(currentTiming, groupProperties);
                highlightRequestSent = true;
            }

            spawnedParticleThisFrame = false;
        }

        public void ProcessArcJudgement(bool isExpired, bool isJudgement, GroupProperties props)
        {
            int currentTiming = Services.Audio.ChartTiming;
            highlightRequestSent = false;
            float x = WorldXAt(currentTiming);
            float y = WorldYAt(currentTiming);
            Vector3 currentPos = new Vector3(x, y);

            if (isExpired)
            {
                SetGroupHighlight(false, int.MinValue);
                JudgementResult result = props.MapJudgementResult(JudgementResult.MissLate);

                if (isJudgement)
                {
                    if (!spawnedParticleThisFrame)
                    {
                        Services.Particle.PlayTextParticle(currentPos + props.CurrentJudgementOffset, result, Option<int>.None());
                        spawnedParticleThisFrame = true;
                    }

                    Services.Score.ProcessJudgement(TimingGroup, result, Option<int>.None());
                }
            }
            else if (currentTiming <= EndTiming + Values.HoldMissLateJudgeWindow)
            {
                SetGroupHighlight(true, currentTiming + Values.HoldParticlePersistDuration);
                if (!hasBeenHitOnce)
                {
                    Services.Hitsound.PlayArcHitsound(Timing);
                }

                hasBeenHitOnce = true;
                JudgementResult result = props.MapJudgementResult(JudgementResult.Max);

                if (isJudgement)
                {
                    if (!spawnedParticleThisFrame)
                    {
                        Services.Particle.PlayTextParticle(currentPos + props.CurrentJudgementOffset, result, Option<int>.None());
                        spawnedParticleThisFrame = true;
                    }

                    Services.Score.ProcessJudgement(TimingGroup, result, Option<int>.None());
                }
            }
        }

        private void RequestJudgement(GroupProperties props)
        {
            for (int t = numJudgementRequestsSent; t < TotalCombo; t++)
            {
                int timing = (int)(Timing + (t * TimeIncrement));
                int startTiming = timing - (int)(TimeIncrement / 2);
                bool shortened = t == 0
                    && PreviousArc != null
                    && ArcConnection.HasDirectionChange(PreviousArc, this);
                int lateTiming = timing + ArcFormula.CalculateArcMissDuration((float)TimeIncrement, shortened);
                Services.Judgement.Request(new ArcJudgementRequest()
                {
                    StartAtTiming = startTiming,
                    ExpireAtTiming = lateTiming,
                    AutoAtTiming = timing,
                    Arc = this,
                    IsJudgement = true,
                    Receiver = this,
                    Properties = props,
                });
            }

            numJudgementRequestsSent = TotalCombo;
        }

        private void RequestHighlight(int timing, GroupProperties props)
        {
            Services.Judgement.Request(new ArcJudgementRequest()
            {
                StartAtTiming = timing,
                ExpireAtTiming = timing + Values.HoldHighlightPersistDuration,
                AutoAtTiming = timing,
                Arc = this,
                IsJudgement = false,
                Receiver = this,
                Properties = props,
            });
        }
    }
}
