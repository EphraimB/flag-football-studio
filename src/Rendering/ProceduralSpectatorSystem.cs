using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FlagFootballStudio.Presentation;

/// <summary>
/// Deterministic, animation-free crowd made from three shared MultiMeshes. Placement is stable for
/// a given venue preset/density/quality and intentionally has no football or AI dependencies.
/// </summary>
public partial class ProceduralSpectatorSystem : Node3D
{
    private static readonly Color[] SkinTones =
    [
        new("f1c7a7"), new("cf9068"), new("a96543"), new("75452f"), new("43291f")
    ];

    private static readonly Color[] ClothingColors =
    [
        new("d8b541"), new("172848"), new("d6d9df"), new("7a2738"),
        new("32745e"), new("3e5f97"), new("25282d"), new("bd6b3b")
    ];

    private readonly MultiMeshInstance3D _heads = new() { Name = "SpectatorHeads" };
    private readonly MultiMeshInstance3D _torsos = new() { Name = "SpectatorTorsos" };
    private readonly MultiMeshInstance3D _legs = new() { Name = "SpectatorLegs" };

    public int InstanceCount { get; private set; }
    public int SeatedCount { get; private set; }
    public int StandingCount { get; private set; }
    public string PlacementSignature { get; private set; } = string.Empty;
    public int MultiMeshCount => 3;
    public bool UsesSharedResources =>
        ReferenceEquals(_heads.Multimesh.Mesh, SharedHeadMesh) &&
        ReferenceEquals(_torsos.Multimesh.Mesh, SharedTorsoMesh) &&
        ReferenceEquals(_legs.Multimesh.Mesh, SharedLegMesh);

    private static SphereMesh SharedHeadMesh { get; } = new()
    {
        Radius = 0.15f,
        Height = 0.3f,
        RadialSegments = 8,
        Rings = 4,
        Material = StudioMaterialLibrary.SpectatorSkin()
    };

    private static CapsuleMesh SharedTorsoMesh { get; } = new()
    {
        Radius = 0.22f,
        Height = 0.72f,
        RadialSegments = 8,
        Rings = 2,
        Material = StudioMaterialLibrary.SpectatorClothing()
    };

    private static BoxMesh SharedLegMesh { get; } = new()
    {
        Size = new Vector3(0.34f, 0.52f, 0.24f),
        Material = StudioMaterialLibrary.SpectatorClothing()
    };

    public override void _Ready()
    {
        AddChild(_legs);
        AddChild(_torsos);
        AddChild(_heads);
        _heads.Multimesh = NewMultiMesh(SharedHeadMesh);
        _torsos.Multimesh = NewMultiMesh(SharedTorsoMesh);
        _legs.Multimesh = NewMultiMesh(SharedLegMesh);
    }

    public void Configure(
        VenuePreset preset,
        float density,
        PresentationQualityPreset quality,
        bool showSpectators)
    {
        var seats = SeatingPositions(preset);
        var qualityScale = quality switch
        {
            PresentationQualityPreset.Final => 1f,
            PresentationQualityPreset.High => 0.72f,
            _ => 0.38f
        };
        var desired = showSpectators
            ? Math.Clamp((int)MathF.Round(seats.Count * Math.Clamp(density, 0, 1) * qualityScale), 0, seats.Count)
            : 0;
        var selected = DeterministicSubset(seats, desired);

        SetCount(_heads.Multimesh, selected.Count);
        SetCount(_torsos.Multimesh, selected.Count);
        SetCount(_legs.Multimesh, selected.Count);
        SeatedCount = 0;
        StandingCount = 0;
        var signature = new List<string>(selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            var seat = selected[index];
            var standing = seat.Ordinal % 11 == 3;
            if (standing) StandingCount++; else SeatedCount++;
            var sideScale = seat.Position.X < 0 ? -1f : 1f;
            var baseY = seat.Position.Y + (standing ? 0.48f : 0.18f);
            var torsoY = baseY + (standing ? 0.66f : 0.48f);
            var headY = torsoY + 0.48f;
            var yaw = sideScale < 0 ? -MathF.PI / 2 : MathF.PI / 2;

            var headTransform = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)),
                new Vector3(seat.Position.X, headY, seat.Position.Z));
            var torsoBasis = Basis.FromEuler(new Vector3(standing ? 0 : -0.08f * sideScale, yaw, 0));
            var torsoTransform = new Transform3D(torsoBasis,
                new Vector3(seat.Position.X, torsoY, seat.Position.Z));
            var legBasis = Basis.FromEuler(new Vector3(standing ? 0 : -0.72f, yaw, 0));
            var legOffset = standing ? Vector3.Zero : new Vector3(-0.18f * sideScale, 0.08f, 0);
            var legTransform = new Transform3D(legBasis,
                new Vector3(seat.Position.X, baseY, seat.Position.Z) + legOffset);

            _heads.Multimesh.SetInstanceTransform(index, headTransform);
            _torsos.Multimesh.SetInstanceTransform(index, torsoTransform);
            _legs.Multimesh.SetInstanceTransform(index, legTransform);
            _heads.Multimesh.SetInstanceColor(index, SkinTones[(seat.Ordinal * 3 + 1) % SkinTones.Length]);
            _torsos.Multimesh.SetInstanceColor(index, ClothingColors[(seat.Ordinal * 5 + 2) % ClothingColors.Length]);
            _legs.Multimesh.SetInstanceColor(index, ClothingColors[(seat.Ordinal * 7 + 4) % ClothingColors.Length].Darkened(0.2f));
            signature.Add($"{seat.Ordinal}:{seat.Position.X:F2},{seat.Position.Y:F2},{seat.Position.Z:F2}:{standing}");
        }

        InstanceCount = selected.Count;
        PlacementSignature = string.Join('|', signature);
    }

    public static IReadOnlyList<Vector3> SpectatorViewpoints(VenuePreset preset) =>
        SeatingPositions(preset).Where((_, index) => index % 9 == 0).Select(seat =>
            seat.Position + new Vector3(seat.Position.X < 0 ? 0.3f : -0.3f, 1.25f, 0)).ToArray();

    private static MultiMesh NewMultiMesh(Mesh mesh) => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true,
        Mesh = mesh
    };

    private static void SetCount(MultiMesh multiMesh, int count)
    {
        multiMesh.InstanceCount = 0;
        multiMesh.InstanceCount = count;
        multiMesh.VisibleInstanceCount = count;
    }

    private static List<Seat> DeterministicSubset(IReadOnlyList<Seat> seats, int desired) =>
        seats.OrderBy(seat => StableOrder(seat.Ordinal)).Take(desired).OrderBy(seat => seat.Ordinal).ToList();

    private static int StableOrder(int value) => unchecked((value * 1103515245 + 12345) & int.MaxValue);

    private static List<Seat> SeatingPositions(VenuePreset preset)
    {
        var sectionsPerSide = preset switch
        {
            VenuePreset.PracticeField => 1,
            VenuePreset.CommunityField => 2,
            _ => 3
        };
        var rows = preset switch
        {
            VenuePreset.PracticeField => 2,
            VenuePreset.CommunityField => 3,
            _ => 4
        };
        var seatsPerRow = preset switch
        {
            VenuePreset.PracticeField => 8,
            VenuePreset.CommunityField => 10,
            _ => 12
        };
        var seats = new List<Seat>();
        var ordinal = 0;
        foreach (var side in new[] { -1f, 1f })
        for (var section = 0; section < sectionsPerSide; section++)
        {
            var sectionZ = (section - (sectionsPerSide - 1) * 0.5f) * 10.5f;
            for (var row = 0; row < rows; row++)
            for (var seat = 0; seat < seatsPerRow; seat++)
            {
                var z = sectionZ + (seat - (seatsPerRow - 1) * 0.5f) * 0.72f;
                var x = side * (17.3f + row * 0.62f);
                seats.Add(new Seat(ordinal++, new Vector3(x, 0.72f + row * 0.42f, z)));
            }
        }
        return seats;
    }

    private readonly record struct Seat(int Ordinal, Vector3 Position);
}
