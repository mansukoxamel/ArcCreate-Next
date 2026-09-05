using System.Collections.Generic;
using ArcCreate.Gameplay.Data;
using ArcCreate.Utility;
using UnityEngine;

namespace ArcCreate.Gameplay.Judgement.Input
{
    public class TouchInputHandler : IInputHandler
    {
        protected List<TouchInput> CurrentInputs { get; } = new List<TouchInput>(10);

        public virtual void PollInput()
        {
            CurrentInputs.Clear();
            int count = UnityEngine.Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);

                TouchInput input = new TouchInput(touch, GetCameraRay(touch.position));
                CurrentInputs.Add(input);

                Services.InputFeedback.LaneFeedback(Mathf.RoundToInt(input.Lane));
                Services.InputFeedback.FloatlineFeedback(input.VerticalPos.y);
            }

            Services.Judgement.Debug.SetTouchState(CurrentInputs);
        }

        public void HandleTapRequests(
            int currentTiming,
            UnorderedList<LaneTapJudgementRequest> laneTapRequests,
            UnorderedList<ArcTapJudgementRequest> arcTapRequests)
        {
            for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
            {
                TouchInput input = CurrentInputs[inpIndex];
                if (!input.IsTap)
                {
                    continue;
                }

                int minTimingDifference = int.MaxValue;
                float minPositionDifference = float.MaxValue;

                bool applicableLaneRequestExists = false;
                LaneTapJudgementRequest applicableLaneRequest = default;
                int applicableLaneRequestIndex = 0;

                for (int i = laneTapRequests.Count - 1; i >= 0; i--)
                {
                    LaneTapJudgementRequest req = laneTapRequests[i];
                    int timingDifference = req.AutoAtTiming - currentTiming;
                    if (timingDifference > minTimingDifference)
                    {
                        continue;
                    }

                    Vector2 judgementSize = req.Properties.CurrentJudgementSize;
                    Vector3 judgementOffset = req.Properties.CurrentJudgementOffset;
                    bool useLane = judgementOffset.y == 0;
                    Vector3 worldPosition = new Vector3(ArcFormula.LaneToWorldX(req.Lane), 0, 0) + judgementOffset;
                    Vector3 screenPosition = Services.Camera.GameplayCamera.WorldToScreenPoint(worldPosition);
                    Vector3 deltaToNote = screenPosition - input.ScreenPos;
                    float distanceToNote = deltaToNote.sqrMagnitude;
                    if (LaneCollide(input, screenPosition, req.Lane, judgementSize, useLane)
                    && (timingDifference < minTimingDifference || distanceToNote <= minPositionDifference))
                    {
                        minTimingDifference = timingDifference;
                        minPositionDifference = distanceToNote;
                        applicableLaneRequestExists = true;
                        applicableLaneRequest = req;
                        applicableLaneRequestIndex = i;
                    }
                }

                bool applicableArcTapRequestExists = false;
                ArcTapJudgementRequest applicableArcTapRequest = default;
                int applicableArcTapRequestIndex = 0;

                for (int i = arcTapRequests.Count - 1; i >= 0; i--)
                {
                    ArcTapJudgementRequest req = arcTapRequests[i];
                    int timingDifference = req.AutoAtTiming - currentTiming;
                    if (timingDifference > minTimingDifference)
                    {
                        continue;
                    }

                    Vector2 judgementSize = req.Properties.CurrentJudgementSize;
                    Vector3 judgementOffset = req.Properties.CurrentJudgementOffset;
                    Vector3 worldPosition = new Vector3(req.X, req.Y, 0) + judgementOffset;
                    Vector3 screenPosition = Services.Camera.GameplayCamera.WorldToScreenPoint(worldPosition);
                    Vector3 deltaToNote = screenPosition - input.ScreenPos;
                    float distanceToNote = deltaToNote.sqrMagnitude;

                    if (ArcTapCollide(input, screenPosition, worldPosition, req.Width, judgementSize)
                    && (timingDifference < minTimingDifference || distanceToNote <= minPositionDifference))
                    {
                        minTimingDifference = timingDifference;
                        minPositionDifference = distanceToNote;
                        applicableArcTapRequestExists = true;
                        applicableArcTapRequest = req;
                        applicableArcTapRequestIndex = i;
                    }
                }

                if (applicableArcTapRequestExists)
                {
                    applicableArcTapRequest.Receiver.ProcessArcTapJudgement(currentTiming - applicableArcTapRequest.AutoAtTiming, applicableArcTapRequest.Properties);
                    arcTapRequests.RemoveAt(applicableArcTapRequestIndex);
                }
                else if (applicableLaneRequestExists)
                {
                    applicableLaneRequest.Receiver.ProcessLaneTapJudgement(currentTiming - applicableLaneRequest.AutoAtTiming, applicableLaneRequest.Properties);
                    laneTapRequests.RemoveAt(applicableLaneRequestIndex);
                }
            }
        }

        public void HandleLaneHoldRequests(int currentTiming, UnorderedList<LaneHoldJudgementRequest> requests)
        {
            for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
            {
                TouchInput input = CurrentInputs[inpIndex];

                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    LaneHoldJudgementRequest req = requests[i];

                    if (currentTiming < req.StartAtTiming || req.Receiver.IsLocked)
                    {
                        continue;
                    }

                    Vector2 judgementSize = req.Properties.CurrentJudgementSize;
                    Vector3 judgementOffset = req.Properties.CurrentJudgementOffset;
                    bool useLane = judgementOffset.y == 0;
                    Vector3 worldPosition = new Vector3(ArcFormula.LaneToWorldX(req.Lane), 0, 0) + judgementOffset;
                    Vector3 screenPosition = Services.Camera.GameplayCamera.WorldToScreenPoint(worldPosition);

                    if (LaneCollide(input, screenPosition, req.Lane, judgementSize, useLane))
                    {
                        req.Receiver.ProcessLaneHoldJudgement(currentTiming >= req.ExpireAtTiming, req.IsJudgement, req.Properties);
                        requests.RemoveAt(i);
                    }
                }
            }
        }

        public void HandleArcRequests(int currentTiming, UnorderedList<ArcJudgementRequest> requests)
        {
            ArcColorLogic.NewFrame(currentTiming);
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                ArcColorLogic.Get(requests[i].Arc.Color);
            }

            // Notify whether active arcs and assigned fingers still exist.
            for (int c = 0; c <= ArcColorLogic.MaxColor; c++)
            {
                ArcColorLogic color = ArcColorLogic.Get(c);

                bool arcOfColorExists = false;
                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    ArcJudgementRequest req = requests[i];
                    if (currentTiming >= req.Arc.Timing
                     && currentTiming <= req.Arc.EndTiming
                     && req.Arc.Color == color.Color)
                    {
                        arcOfColorExists = true;
                        break;
                    }
                }

                color.ExistsArcWithinRange(arcOfColorExists);
                for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
                {
                    TouchInput input = CurrentInputs[inpIndex];
                    color.FingerExists(input.Id);
                }
            }

            // Opposite-color arcs closer than 200 internal units accept any finger for 500 ms.
            bool graceActive = false;
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                ArcJudgementRequest req1 = requests[i];
                if (currentTiming > req1.Arc.EndTiming || currentTiming < req1.Arc.Timing)
                {
                    continue;
                }

                for (int j = i - 1; j >= 0; j--)
                {
                    ArcJudgementRequest req2 = requests[j];
                    if (req2.Arc.Color == req1.Arc.Color
                     || currentTiming > req2.Arc.EndTiming
                     || currentTiming < req2.Arc.Timing)
                    {
                        continue;
                    }

                    Vector2 position1 = new Vector2(
                        req1.Arc.WorldSegmentedXAt(currentTiming),
                        req1.Arc.WorldSegmentedYAt(currentTiming)) + (Vector2)req1.Properties.CurrentJudgementOffset;
                    Vector2 position2 = new Vector2(
                        req2.Arc.WorldSegmentedXAt(currentTiming),
                        req2.Arc.WorldSegmentedYAt(currentTiming)) + (Vector2)req2.Properties.CurrentJudgementOffset;

                    if (ArcFormula.AreArcsWithinIntersectionDistance(position1, position2))
                    {
                        graceActive = true;
                        break;
                    }
                }

                if (graceActive)
                {
                    ArcColorLogic.StartGracePeriodForAllColors();
                    break;
                }
            }

            // A physical lift releases ownership. Only an active arc of the same color
            // supplies the interval used by the native reacquisition lock.
            for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
            {
                TouchInput input = CurrentInputs[inpIndex];
                if (input.Phase != TouchPhase.Ended && input.Phase != TouchPhase.Canceled)
                {
                    continue;
                }

                for (int c = 0; c <= ArcColorLogic.MaxColor; c++)
                {
                    float judgeInterval = 0;
                    for (int i = requests.Count - 1; i >= 0; i--)
                    {
                        ArcJudgementRequest req = requests[i];
                        if (req.Arc.Color == c
                         && currentTiming >= req.Arc.Timing
                         && currentTiming <= req.Arc.EndTiming)
                        {
                            judgeInterval = (float)req.Arc.TimeIncrement;
                            break;
                        }
                    }

                    ArcColorLogic.Get(c).FingerLifted(input.Id, judgeInterval);
                }
            }

            // Cache geometry before assigning any new finger. Initial acquisition and
            // retained tracking deliberately use different lookahead and X ranges.
            var collisionByFinger = new Dictionary<int, Dictionary<Arc, bool>>();
            for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
            {
                TouchInput input = CurrentInputs[inpIndex];

                if (input.Phase == TouchPhase.Ended || input.Phase == TouchPhase.Canceled)
                {
                    continue;
                }

                var collisions = new Dictionary<Arc, bool>();
                var distances = new Dictionary<Arc, float>();
                collisionByFinger[input.Id] = collisions;
                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    ArcJudgementRequest req = requests[i];
                    if (currentTiming > req.Arc.EndTiming && !req.Properties.SloppyJudgement)
                    {
                        continue;
                    }
                    if (currentTiming < req.Arc.Timing || collisions.ContainsKey(req.Arc))
                    {
                        continue;
                    }

                    Vector2 judgementSize = req.Properties.CurrentJudgementSize;
                    Vector2 judgementOffset = req.Properties.CurrentJudgementOffset;
                    bool retained = ArcColorLogic.Get(req.Arc.Color).IsAssignedTo(input.Id);
                    int positionTiming = ArcFormula.ArcInputPositionTiming(currentTiming, req.Arc.EndTiming, retained);
                    Vector2 arcPosition = new Vector2(
                        req.Arc.WorldSegmentedXAt(positionTiming),
                        req.Arc.WorldSegmentedYAt(positionTiming)) + judgementOffset;
                    distances[req.Arc] = (arcPosition - (Vector2)input.VerticalPos).sqrMagnitude;
                    collisions[req.Arc] = ArcCollide(
                        input,
                        req.Arc,
                        currentTiming,
                        judgementSize,
                        judgementOffset,
                        retained);
                }

                // The native color owner is chosen from all active arcs, rather than
                // treating a miss against one same-color arc as a miss against all.
                for (int c = 0; c <= ArcColorLogic.MaxColor; c++)
                {
                    bool arcChecked = false;
                    bool hit = false;
                    float minDistance = float.MaxValue;
                    float judgeInterval = 0;
                    foreach (KeyValuePair<Arc, bool> collision in collisions)
                    {
                        Arc arc = collision.Key;
                        if (arc.Color != c)
                        {
                            continue;
                        }

                        arcChecked = true;
                        judgeInterval = (float)arc.TimeIncrement;
                        if (!collision.Value)
                        {
                            continue;
                        }

                        float distance = distances[arc];
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                        }

                        hit = true;
                    }

                    if (!arcChecked)
                    {
                        continue;
                    }

                    ArcColorLogic colorLogic = ArcColorLogic.Get(c);
                    if (hit)
                    {
                        colorLogic.FingerHit(input.Id, minDistance, judgeInterval);
                    }
                    else
                    {
                        colorLogic.FingerMiss(input.Id, judgeInterval);
                    }
                }
            }

            // Reply to requests
            for (int inpIndex = 0; inpIndex < CurrentInputs.Count; inpIndex++)
            {
                TouchInput input = CurrentInputs[inpIndex];

                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    ArcJudgementRequest req = requests[i];
                    if (currentTiming < req.StartAtTiming)
                    {
                        continue;
                    }

                    ArcColorLogic colorLogic = ArcColorLogic.Get(req.Arc.Color);
                    bool collide = collisionByFinger.TryGetValue(input.Id, out Dictionary<Arc, bool> collisions)
                        && collisions.TryGetValue(req.Arc, out bool requestCollision)
                        && requestCollision;
                    bool acceptInput = colorLogic.ShouldAcceptInput(input.Id);

                    if (collide && acceptInput)
                    {
                        req.Receiver.ProcessArcJudgement(currentTiming >= req.ExpireAtTiming, req.IsJudgement, req.Properties);
                        requests.RemoveAt(i);
                    }
                }
            }

            ArcColorLogic.ApplyRedValue();
        }

        public void ResetJudge()
        {
            ArcColorLogic.ResetAll();
        }

        protected Ray GetCameraRay(Vector2 screenPosition)
        {
            return Services.Camera.GameplayCamera.ScreenPointToRay(screenPosition);
        }

        private bool ArcCollide(
            TouchInput touch,
            Arc arc,
            int currentTiming,
            Vector2 judgementSize,
            Vector3 judgementOffset,
            bool retained)
        {
            int positionTiming = ArcFormula.ArcInputPositionTiming(currentTiming, arc.EndTiming, retained);
            Vector3 arcWorldPosition = new Vector3(
                arc.WorldSegmentedXAt(positionTiming),
                arc.WorldSegmentedYAt(positionTiming)) + judgementOffset;
            float skyInputY = Services.Judgement.SkyInputY;
            if (arcWorldPosition.y <= skyInputY)
            {
                touch.VerticalPos.y = Mathf.Min(touch.VerticalPos.y, skyInputY);
            }

            return ArcFormula.IsWithinArcInputRange(
                touch.VerticalPos,
                arcWorldPosition,
                retained,
                judgementSize);
        }

        private bool ArcTapCollide(TouchInput input, Vector3 screenPosition, Vector3 worldPosition, float width, Vector2 judgementSize)
        {
            float skyInputY = Services.Judgement.SkyInputY;
            if (worldPosition.y <= skyInputY)
            {
                input.VerticalPos.y = Mathf.Min(input.VerticalPos.y, skyInputY);
            }

            float hitboxX = Values.ArcTapHitboxX + (Values.LaneWidth / 2 * (width - 1));
            float dSx = Mathf.Abs(input.ScreenPos.x - screenPosition.x);
            float dSy = input.ScreenPos.y - screenPosition.y;
            bool screenCollide = dSx <= (Values.LaneScreenHitboxHorizontal * 2 * hitboxX / Values.LaneWidth * judgementSize.x)
                              && dSy >= (-Values.LaneScreenHitboxVertical * 2 * Values.ArcTapHitboxYDown / Values.LaneWidth * judgementSize.y)
                              && dSy <= (Values.LaneScreenHitboxVertical * 2 * Values.ArcTapHitboxYUp / Values.LaneWidth * judgementSize.y);

            float dWx = Mathf.Abs(input.VerticalPos.x - worldPosition.x);
            float dWy = input.VerticalPos.y - worldPosition.y;
            bool worldCollide = dWx <= (hitboxX * judgementSize.x)
                             && dWy >= (-Values.ArcTapHitboxYDown * judgementSize.y)
                             && dWy <= (Values.ArcTapHitboxYUp * judgementSize.y);
            return worldCollide || screenCollide;
        }

        private bool LaneCollide(TouchInput input, Vector3 screenPosition, float lane, Vector2 judgementSize, bool useLane)
        {
            float dLx = Mathf.Abs(input.Lane - lane);
            bool worldCollide = dLx <= 0.5f * judgementSize.x && useLane;
            bool screenCollide = Mathf.Abs(input.ScreenPos.x - screenPosition.x) <= (Values.LaneScreenHitboxHorizontal * judgementSize.x)
                              && Mathf.Abs(input.ScreenPos.y - screenPosition.y) <= (Values.LaneScreenHitboxVertical * judgementSize.y);
            return worldCollide || screenCollide;
        }
    }
}
