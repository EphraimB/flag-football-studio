using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

/// <summary>Shared physical layout contract used by visible fixtures, lights, and camera clearance checks.</summary>
public static class VenueLayout
{
    public const float PlayingFieldHalfWidth = 10;
    public const float PlayingFieldHalfLength = 20;
    public const float SidelineCameraX = 13;
    public const float SidelineCameraClearRadius = 2.25f;

    public static IReadOnlyList<Vector3> FieldLightPositions { get; } =
    [
        new(-12, 13, -16),
        new(12, 13, -16),
        new(-12, 13, 16),
        new(12, 13, 16)
    ];
}
