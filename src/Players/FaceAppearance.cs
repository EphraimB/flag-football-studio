using System;

namespace FlagFootballStudio.Domain;

public sealed class FaceAppearance
{
    public const float MinimumScale = 0.75f;
    public const float MaximumScale = 1.25f;
    public const float MinimumOffset = -0.2f;
    public const float MaximumOffset = 0.2f;

    public FaceAppearance()
    {
        SetParameters(
            1, 1, 1, 1, 1, 0, 1, 1, 1, 1,
            1, 0, 1, 1, 1, 0, 1, 1, 1, 0);
        EyeColor = new AppearanceColor(78, 111, 126);
    }

    public float HeadWidth { get; private set; }
    public float HeadHeight { get; private set; }
    public float JawWidth { get; private set; }
    public float JawHeight { get; private set; }
    public float ChinWidth { get; private set; }
    public float ChinProjection { get; private set; }
    public float CheekboneWidth { get; private set; }
    public float CheekFullness { get; private set; }
    public float ForeheadHeight { get; private set; }
    public float EyeSpacing { get; private set; }
    public float EyeSize { get; private set; }
    public float EyeVerticalPosition { get; private set; }
    public float EyebrowHeight { get; private set; }
    public float NoseWidth { get; private set; }
    public float NoseLength { get; private set; }
    public float NoseProjection { get; private set; }
    public float MouthWidth { get; private set; }
    public float LipFullness { get; private set; }
    public float EarSize { get; private set; }
    public float EarPosition { get; private set; }
    public AppearanceColor EyeColor { get; private set; }

    public void SetEyeColor(AppearanceColor color) => EyeColor = color;

    public void SetParameters(
        float headWidth,
        float headHeight,
        float jawWidth,
        float jawHeight,
        float chinWidth,
        float chinProjection,
        float cheekboneWidth,
        float cheekFullness,
        float foreheadHeight,
        float eyeSpacing,
        float eyeSize,
        float eyeVerticalPosition,
        float eyebrowHeight,
        float noseWidth,
        float noseLength,
        float noseProjection,
        float mouthWidth,
        float lipFullness,
        float earSize,
        float earPosition)
    {
        HeadWidth = Scale(headWidth);
        HeadHeight = Scale(headHeight);
        JawHeight = Scale(jawHeight);
        CheekFullness = Scale(cheekFullness);
        ForeheadHeight = Scale(foreheadHeight);
        EyebrowHeight = Scale(eyebrowHeight);
        NoseLength = Scale(noseLength);
        LipFullness = Scale(lipFullness);
        EarSize = Scale(earSize);

        JawWidth = MathF.Min(Scale(jawWidth), HeadWidth + 0.12f);
        ChinWidth = MathF.Min(Scale(chinWidth), JawWidth + 0.03f);
        CheekboneWidth = MathF.Min(Scale(cheekboneWidth), HeadWidth + 0.1f);
        EyeSpacing = MathF.Min(Scale(eyeSpacing), HeadWidth + 0.08f);
        EyeSize = MathF.Min(Scale(eyeSize), 0.65f + EyeSpacing * 0.45f);
        NoseWidth = MathF.Min(Scale(noseWidth), EyeSpacing + 0.08f);
        MouthWidth = MathF.Min(Scale(mouthWidth), JawWidth + 0.08f);

        ChinProjection = Offset(chinProjection);
        EyeVerticalPosition = Offset(eyeVerticalPosition);
        NoseProjection = Offset(noseProjection);
        EarPosition = Offset(earPosition);
    }

    private static float Scale(float value) => Math.Clamp(value, MinimumScale, MaximumScale);
    private static float Offset(float value) => Math.Clamp(value, MinimumOffset, MaximumOffset);
}
