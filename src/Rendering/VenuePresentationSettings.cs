using System;

namespace FlagFootballStudio.Presentation;

public enum VenuePreset
{
    PracticeField,
    CommunityField,
    CollegeField
}

/// <summary>
/// Presentation-only venue controls. These settings never participate in football simulation.
/// </summary>
public readonly record struct VenuePresentationSettings(
    VenuePreset Preset,
    float SpectatorDensity,
    bool ShowSpectators,
    bool ShowSidelineEquipment)
{
    public static VenuePresentationSettings Default =>
        new(VenuePreset.CommunityField, 0.45f, true, true);

    public VenuePresentationSettings Validated()
    {
        if (!Enum.IsDefined(Preset))
            throw new ArgumentOutOfRangeException(nameof(Preset), "Unknown venue preset.");
        return this with { SpectatorDensity = Math.Clamp(SpectatorDensity, 0, 1) };
    }
}
