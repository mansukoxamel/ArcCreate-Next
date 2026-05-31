using System;

namespace ArcCreate.Gameplay.Scenecontrol
{
    [Flags]
    public enum EnabledFeatures : long
    {
        None = 0,
        JudgeManipulation = 1,
        DropRateManipulation = 2,
        JudgementTriggerTimingGroup = 4,
        All = None | JudgeManipulation | DropRateManipulation | JudgementTriggerTimingGroup,
    }
}