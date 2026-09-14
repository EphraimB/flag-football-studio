using System;

namespace FlagFootballStudio.Presentation;

public readonly record struct AmbientAudioSettings(
    float CrowdVolume,
    float SidelineChatterVolume,
    float PlayerChatterVolume,
    float ActionSfxVolume,
    bool AmbientConversationsEnabled,
    bool CrowdReactionsEnabled,
    bool DebugAmbienceBoost = false)
{
    public static AmbientAudioSettings Default => new(0.32f, 0.22f, 0.18f, 0.7f, true, true);

    public AmbientAudioSettings Validated() => this with
    {
        CrowdVolume = Math.Clamp(CrowdVolume, 0, 1),
        SidelineChatterVolume = Math.Clamp(SidelineChatterVolume, 0, 1),
        PlayerChatterVolume = Math.Clamp(PlayerChatterVolume, 0, 1),
        ActionSfxVolume = Math.Clamp(ActionSfxVolume, 0, 1)
    };
}

public enum VenueAudioLayer
{
    CrowdAmbience,
    SidelineChatter,
    PlayerChatter,
    ActionSfx
}

public enum AuthoredDialoguePriority
{
    Natural,
    Featured
}

public enum CrowdReactionKind
{
    BackgroundMurmur,
    Moderate,
    Cheer,
    Disappointment,
    TouchdownCelebration,
    Interception
}

public enum AmbientConversationContext
{
    PrePlay,
    Huddle,
    Sideline,
    PostPlay
}

public enum VenueAudioTestMode
{
    Raw2D,
    ForcedNearSpatial,
    ProductionSpatial
}

public enum VenueAudioTestKind
{
    Crowd,
    Sideline,
    PlayerChatterBed,
    Footstep,
    CatchImpact,
    Whistle,
    Cheer
}
