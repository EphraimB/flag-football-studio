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
    float HeadMotionScale,
    float GaitPhase,
    float LeftFootPlantWeight,
    float RightFootPlantWeight)
{
    public static HumanoidAnimationCue ForState(HumanoidAnimationState state) => state switch
    {
        HumanoidAnimationState.Jog => new(state, 3.5f, 0.5f, 0, 0, 0, 1.85f, 0.08f, 0.45f, -1, 0.5f, 0.5f),
        HumanoidAnimationState.Sprint => new(state, 6.5f, 0.95f, 0, 0, 0, 2.65f, 0.2f, 0.65f, -1, 0.35f, 0.35f),
        HumanoidAnimationState.Acceleration => new(state, 3.8f, 0.55f, 5, 0, 0, 2.05f, 0.19f, 0.5f, -1, 0.55f, 0.55f),
        HumanoidAnimationState.Deceleration => new(state, 2.8f, 0.4f, -5, 0, 0, 1.65f, -0.08f, 0.35f, -1, 0.7f, 0.7f),
        HumanoidAnimationState.PostCatchRun => new(state, 5.8f, 0.84f, 0, 0, 0, 2.45f, 0.17f, 0.55f, -1, 0.4f, 0.4f),
        HumanoidAnimationState.RouteCut => new(state, 2.5f, 0.4f, -2, 18, 1, 0, 0.02f, 0.2f, 0, 0.15f, 1),
        HumanoidAnimationState.PreSnapReady or HumanoidAnimationState.QuarterbackSet =>
            new(state, 0, 0, 0, 0, 0, 0, 0, 0.1f, 0, 1, 1),
        _ => new(state, 0, 0, 0, 0, 0, 0, 0, 0.2f, 0, 0.85f, 0.85f)
    };
}
