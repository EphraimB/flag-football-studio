namespace FlagFootballStudio.Presentation;

public readonly record struct HumanoidAnimationCue(
    HumanoidAnimationState State,
    float MovementSpeed,
    float NormalizedSpeed,
    float Acceleration,
    float TurnDegrees,
    float TurnDirection,
    float StrideFrequency,
    float BodyLean,
    float HeadMotionScale)
{
    public static HumanoidAnimationCue ForState(HumanoidAnimationState state) => state switch
    {
        HumanoidAnimationState.Jog => new(state, 3.5f, 0.5f, 0, 0, 0, 1.85f, 0.08f, 0.45f),
        HumanoidAnimationState.Sprint => new(state, 6.5f, 0.95f, 0, 0, 0, 2.65f, 0.2f, 0.65f),
        HumanoidAnimationState.Acceleration => new(state, 3.8f, 0.55f, 5, 0, 0, 2.05f, 0.19f, 0.5f),
        HumanoidAnimationState.Deceleration => new(state, 2.8f, 0.4f, -5, 0, 0, 1.65f, -0.08f, 0.35f),
        HumanoidAnimationState.PostCatchRun => new(state, 5.8f, 0.84f, 0, 0, 0, 2.45f, 0.17f, 0.55f),
        _ => new(state, 0, 0, 0, 0, 0, 0, 0, 0.2f)
    };
}
