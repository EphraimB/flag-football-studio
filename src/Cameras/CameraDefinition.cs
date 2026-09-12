using System;

namespace FlagFootballStudio.Domain;

public enum CameraType
{
    BroadcastWide,
    SidelineLow,
    EndZone,
    PlayerPov,
    FreeCamera
}

public enum PlayerPovMode
{
    LockedForward,
    FreeLook,
    LookAtTarget
}

public enum PlayerPovTargetKind
{
    Player,
    Football,
    WorldPoint
}

public readonly record struct CameraVector(float X, float Y, float Z);

public readonly record struct PlayerPovSettings(
    PlayerPovMode Mode,
    float MouseSensitivity,
    float ControllerSensitivity,
    float StabilizationStrength,
    float HeadBobStrength,
    float PitchOffsetDegrees,
    float ForwardOffset,
    float NearClip,
    PlayerPovTargetKind TargetKind,
    Guid? TargetPlayerId,
    CameraVector WorldTarget)
{
    public static PlayerPovSettings Default => new(
        PlayerPovMode.LockedForward,
        0.12f,
        120f,
        0.72f,
        1,
        -6,
        0.055f,
        0.025f,
        PlayerPovTargetKind.Football,
        null,
        new CameraVector(0, 1, 0));
}

public sealed class CameraDefinition
{
    public CameraDefinition(Guid id, string name, CameraType type)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A camera ID cannot be empty.", nameof(id));
        Id = id;
        Name = ValidateName(name);
        Type = type;
        Position = new CameraVector(15, 19, 24);
        RotationDegrees = new CameraVector(-32, 32, 0);
        FieldOfView = 52;
        PovSettings = PlayerPovSettings.Default;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public CameraType Type { get; private set; }
    public CameraVector Position { get; private set; }
    public CameraVector RotationDegrees { get; private set; }
    public float FieldOfView { get; private set; }
    public Guid? PlayerId { get; private set; }
    public PlayerPovSettings PovSettings { get; private set; }

    public void Rename(string name) => Name = ValidateName(name);

    public void SetType(CameraType type) => Type = type;

    public void SetFreeCamera(CameraVector position, CameraVector rotationDegrees, float fieldOfView)
    {
        if (fieldOfView is < 10 or > 120)
            throw new ArgumentOutOfRangeException(nameof(fieldOfView), "FOV must be between 10 and 120 degrees.");
        Position = position;
        RotationDegrees = rotationDegrees;
        FieldOfView = fieldOfView;
    }

    public void SetPlayer(Guid? playerId)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("A Player POV target cannot be empty.", nameof(playerId));
        PlayerId = playerId;
        if (PovSettings.TargetPlayerId == playerId)
            PovSettings = PovSettings with { TargetPlayerId = null };
    }

    public void SetPlayerPovSettings(PlayerPovSettings settings)
    {
        if (!Enum.IsDefined(settings.Mode))
            throw new ArgumentOutOfRangeException(nameof(settings), "Unknown Player POV mode.");
        if (!Enum.IsDefined(settings.TargetKind))
            throw new ArgumentOutOfRangeException(nameof(settings), "Unknown Player POV target type.");
        if (settings.TargetPlayerId == Guid.Empty)
            throw new ArgumentException("A Player POV target player cannot be empty.", nameof(settings));
        if (settings.TargetKind == PlayerPovTargetKind.Player && settings.TargetPlayerId == PlayerId)
            throw new ArgumentException("A Player POV camera must look at another player.", nameof(settings));
        if (!Finite(settings.WorldTarget))
            throw new ArgumentException("The Player POV world target must be finite.", nameof(settings));

        PovSettings = settings with
        {
            MouseSensitivity = Math.Clamp(settings.MouseSensitivity, 0.01f, 1f),
            ControllerSensitivity = Math.Clamp(settings.ControllerSensitivity, 10f, 360f),
            StabilizationStrength = Math.Clamp(settings.StabilizationStrength, 0, 1),
            HeadBobStrength = Math.Clamp(settings.HeadBobStrength, 0, 1),
            PitchOffsetDegrees = Math.Clamp(settings.PitchOffsetDegrees, -30, 30),
            ForwardOffset = Math.Clamp(settings.ForwardOffset, 0, 0.25f),
            NearClip = Math.Clamp(settings.NearClip, 0.005f, 0.2f)
        };
    }

    public CameraDefinition Duplicate(string name)
    {
        var copy = new CameraDefinition(Guid.NewGuid(), name, Type);
        copy.SetFreeCamera(Position, RotationDegrees, FieldOfView);
        copy.SetPlayer(PlayerId);
        copy.SetPlayerPovSettings(PovSettings);
        return copy;
    }

    private static bool Finite(CameraVector vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A camera name is required.", nameof(name));
        return name.Trim();
    }
}
