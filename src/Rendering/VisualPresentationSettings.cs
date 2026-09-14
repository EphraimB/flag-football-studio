using System;

namespace FlagFootballStudio.Presentation;

public enum SportsLightingPreset
{
    Day,
    GoldenHour,
    Overcast,
    NightFieldLights
}

public enum PresentationQualityPreset
{
    Preview,
    High,
    Final
}

public enum ShadowQualityPreset
{
    Low,
    Medium,
    High
}

public readonly record struct VisualPresentationSettings(
    SportsLightingPreset Lighting,
    PresentationQualityPreset Quality,
    ShadowQualityPreset Shadows,
    float Exposure)
{
    public static VisualPresentationSettings Default => new(
        SportsLightingPreset.Day,
        PresentationQualityPreset.Preview,
        ShadowQualityPreset.Medium,
        1f);

    public VisualPresentationSettings Validated() => this with
    {
        Exposure = Math.Clamp(Exposure, 0.6f, 1.4f)
    };
}
