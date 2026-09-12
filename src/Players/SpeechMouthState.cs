using Godot;

namespace FlagFootballStudio.Presentation;

public enum SpeechMouthShape
{
    Rest,
    A,
    E,
    I,
    O,
    U,
    Mbp,
    Fv,
    L,
    Wq
}

public readonly record struct SpeechMouthPose(
    float JawOpen,
    float Width,
    float LipFullness,
    float Roundness,
    float UpperLipRaise,
    float LowerLipDrop,
    float CornerPull,
    float TeethExposure)
{
    public static SpeechMouthPose Rest => new(0, 1, 1, 0, 0, 0, 0, 0);

    public static SpeechMouthPose For(SpeechMouthShape shape) => shape switch
    {
        SpeechMouthShape.A => new(0.78f, 1, 0.95f, 0.15f, 0.12f, 0.62f, 0.05f, 0.6f),
        SpeechMouthShape.E => new(0.34f, 1.28f, 0.76f, 0, 0.18f, 0.22f, 0.7f, 0.72f),
        SpeechMouthShape.I => new(0.24f, 1.2f, 0.72f, 0, 0.12f, 0.16f, 0.55f, 0.66f),
        SpeechMouthShape.O => new(0.62f, 0.78f, 1.12f, 1, 0.08f, 0.52f, -0.1f, 0.18f),
        SpeechMouthShape.U => new(0.4f, 0.68f, 1.24f, 0.88f, 0.04f, 0.34f, -0.2f, 0.12f),
        SpeechMouthShape.Mbp => new(0, 0.94f, 1.3f, 0.2f, -0.12f, -0.12f, 0, 0),
        SpeechMouthShape.Fv => new(0.14f, 1.02f, 0.84f, 0, 0.22f, -0.08f, 0.12f, 0.82f),
        SpeechMouthShape.L => new(0.46f, 1.02f, 0.9f, 0.08f, 0.18f, 0.3f, 0.08f, 0.78f),
        SpeechMouthShape.Wq => new(0.27f, 0.66f, 1.22f, 0.96f, 0, 0.2f, -0.2f, 0.08f),
        _ => Rest
    };

    public SpeechMouthPose WithManualControls(
        float jawOpen,
        float width,
        float lipFullness,
        float upperLip,
        float lowerLip) => this with
    {
        JawOpen = Mathf.Clamp(JawOpen + jawOpen, 0, 1),
        Width = Mathf.Clamp(Width * width, 0.55f, 1.5f),
        LipFullness = Mathf.Clamp(LipFullness * lipFullness, 0.55f, 1.5f),
        UpperLipRaise = Mathf.Clamp(UpperLipRaise + upperLip, -1, 1),
        LowerLipDrop = Mathf.Clamp(LowerLipDrop + lowerLip, -1, 1)
    };

    public static SpeechMouthPose Lerp(SpeechMouthPose from, SpeechMouthPose to, float weight) => new(
        Mathf.Lerp(from.JawOpen, to.JawOpen, weight),
        Mathf.Lerp(from.Width, to.Width, weight),
        Mathf.Lerp(from.LipFullness, to.LipFullness, weight),
        Mathf.Lerp(from.Roundness, to.Roundness, weight),
        Mathf.Lerp(from.UpperLipRaise, to.UpperLipRaise, weight),
        Mathf.Lerp(from.LowerLipDrop, to.LowerLipDrop, weight),
        Mathf.Lerp(from.CornerPull, to.CornerPull, weight),
        Mathf.Lerp(from.TeethExposure, to.TeethExposure, weight));

    public bool IsEqualApprox(SpeechMouthPose other) =>
        Mathf.IsEqualApprox(JawOpen, other.JawOpen) &&
        Mathf.IsEqualApprox(Width, other.Width) &&
        Mathf.IsEqualApprox(LipFullness, other.LipFullness) &&
        Mathf.IsEqualApprox(Roundness, other.Roundness) &&
        Mathf.IsEqualApprox(UpperLipRaise, other.UpperLipRaise) &&
        Mathf.IsEqualApprox(LowerLipDrop, other.LowerLipDrop) &&
        Mathf.IsEqualApprox(CornerPull, other.CornerPull) &&
        Mathf.IsEqualApprox(TeethExposure, other.TeethExposure);
}
