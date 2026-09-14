using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum VenueAudioHookKind
{
    CrowdAmbience,
    OppositeCrowdAmbience,
    GoldSidelineChatter,
    NavySidelineChatter,
    GoldBenchChatter,
    NavyBenchChatter,
    Whistle,
    Footsteps,
    FlagsEquipment,
    CatchThrowImpacts,
    CelebrationReactions
}

/// <summary>
/// Stable spatial anchors for future venue audio. No streams, synthesis, or playback are created here.
/// </summary>
public partial class VenueAudioHooks : Node3D
{
    private readonly Dictionary<VenueAudioHookKind, Node3D> _anchors = [];

    public IReadOnlyDictionary<VenueAudioHookKind, Node3D> Anchors => _anchors;

    public override void _Ready()
    {
        AddAnchor(VenueAudioHookKind.CrowdAmbience, new Vector3(18.5f, 3, 0));
        AddAnchor(VenueAudioHookKind.OppositeCrowdAmbience, new Vector3(-18.5f, 3, 0));
        AddAnchor(VenueAudioHookKind.GoldSidelineChatter, new Vector3(-11.4f, 1.5f, 0));
        AddAnchor(VenueAudioHookKind.NavySidelineChatter, new Vector3(11.4f, 1.5f, 0));
        AddAnchor(VenueAudioHookKind.GoldBenchChatter, new Vector3(-14.3f, 1.25f, -8.3f));
        AddAnchor(VenueAudioHookKind.NavyBenchChatter, new Vector3(14.3f, 1.25f, 8.3f));
        AddAnchor(VenueAudioHookKind.Whistle, new Vector3(0, 1.7f, 0));
        AddAnchor(VenueAudioHookKind.Footsteps, new Vector3(0, 0.08f, 0));
        AddAnchor(VenueAudioHookKind.FlagsEquipment, new Vector3(0, 1, 0));
        AddAnchor(VenueAudioHookKind.CatchThrowImpacts, new Vector3(0, 1.4f, 0));
        AddAnchor(VenueAudioHookKind.CelebrationReactions, new Vector3(0, 2.5f, 0));
    }

    public Node3D Anchor(VenueAudioHookKind kind) => _anchors[kind];

    private void AddAnchor(VenueAudioHookKind kind, Vector3 position)
    {
        var anchor = new Node3D { Name = kind.ToString(), Position = position };
        AddChild(anchor);
        _anchors.Add(kind, anchor);
    }
}
