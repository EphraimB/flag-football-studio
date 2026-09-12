using Godot;

namespace FlagFootballStudio.Presentation;

public enum FacialExpressionState
{
    Neutral,
    Smile,
    Focused,
    Concerned,
    Surprised,
    Frustrated
}

public readonly record struct FacialExpressionPose(
    float BrowRaise,
    float BrowTilt,
    float Smile,
    float MouthOpen,
    float EyelidClosure)
{
    public static FacialExpressionPose For(FacialExpressionState expression) => expression switch
    {
        FacialExpressionState.Smile => new(0.1f, 0, 1, 0.12f, 0.08f),
        FacialExpressionState.Focused => new(-0.35f, -0.45f, 0, 0, 0.18f),
        FacialExpressionState.Concerned => new(0.35f, 0.55f, -0.2f, 0.1f, 0.04f),
        FacialExpressionState.Surprised => new(1, 0, 0, 1, -0.35f),
        FacialExpressionState.Frustrated => new(-0.45f, 0.65f, -0.55f, 0.08f, 0.22f),
        _ => default
    };

    public FacialExpressionPose WithBrowOffsets(float raise, float tilt) => this with
    {
        BrowRaise = Mathf.Clamp(BrowRaise + raise, -1, 1),
        BrowTilt = Mathf.Clamp(BrowTilt + tilt, -1, 1)
    };

    public static FacialExpressionPose Lerp(FacialExpressionPose from, FacialExpressionPose to, float weight) => new(
        Mathf.Lerp(from.BrowRaise, to.BrowRaise, weight),
        Mathf.Lerp(from.BrowTilt, to.BrowTilt, weight),
        Mathf.Lerp(from.Smile, to.Smile, weight),
        Mathf.Lerp(from.MouthOpen, to.MouthOpen, weight),
        Mathf.Lerp(from.EyelidClosure, to.EyelidClosure, weight));

    public bool IsEqualApprox(FacialExpressionPose other) =>
        Mathf.IsEqualApprox(BrowRaise, other.BrowRaise) &&
        Mathf.IsEqualApprox(BrowTilt, other.BrowTilt) &&
        Mathf.IsEqualApprox(Smile, other.Smile) &&
        Mathf.IsEqualApprox(MouthOpen, other.MouthOpen) &&
        Mathf.IsEqualApprox(EyelidClosure, other.EyelidClosure);
}
