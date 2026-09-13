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

public enum PlayerPovLensPreset { GoProWide, GoProLinear, GoProSuperView, Narrow }
public enum PlayerPovMount { Eye, Forehead, Chest, Shoulder }
public enum CameraBehaviorPreset { Static, TrackPlayer, TrackFootball, FollowPlayCenter, Manual }
public enum SidelineSide { Left, Right }

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
    CameraVector WorldTarget,
    float HorizontalFieldOfView = 100,
    float VerticalFieldOfView = 75,
    PlayerPovLensPreset LensPreset = PlayerPovLensPreset.GoProWide,
    float DistortionStrength = 0,
    float HorizonLeveling = 0.7f,
    PlayerPovMount Mount = PlayerPovMount.Eye,
    float UpOffset = 0,
    float MotionSmoothing = 0.72f)
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
        new CameraVector(0, 1, 0),
        100,
        75,
        PlayerPovLensPreset.GoProWide,
        0,
        0.7f,
        PlayerPovMount.Eye,
        0,
        0.72f);
}

public readonly record struct SidelineCameraSettings(
    CameraBehaviorPreset Behavior,
    SidelineSide Side,
    float CameraHeight,
    float SidelineDistance,
    float FocalLengthMm,
    float MinimumFocalLengthMm,
    float MaximumFocalLengthMm,
    float ZoomSpeed,
    float PanDegrees,
    float TiltDegrees,
    Guid? TargetPlayerId,
    float TrackingStrength,
    CameraVector FramingOffset,
    bool AutoZoom,
    float TargetScreenSize)
{
    public static SidelineCameraSettings Default => new(
        CameraBehaviorPreset.FollowPlayCenter, SidelineSide.Left, 3.2f, 3,
        35, 18, 120, 12, 0, 0, null, 0.72f,
        new CameraVector(0, 1.2f, 0), false, 0.32f);

    public float HorizontalFieldOfView => FovForSensor(36, FocalLengthMm);
    public float VerticalFieldOfView => FovForSensor(24, FocalLengthMm);
    public static float FovForSensor(float sensorMillimeters, float focalLengthMillimeters) =>
        2 * (float)(Math.Atan(sensorMillimeters / (2 * focalLengthMillimeters)) * 180 / Math.PI);
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
        SidelineSettings = SidelineCameraSettings.Default;
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public CameraType Type { get; private set; }
    public CameraVector Position { get; private set; }
    public CameraVector RotationDegrees { get; private set; }
    public float FieldOfView { get; private set; }
    public Guid? PlayerId { get; private set; }
    public PlayerPovSettings PovSettings { get; private set; }
    public SidelineCameraSettings SidelineSettings { get; private set; }

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
        if (!Enum.IsDefined(settings.LensPreset) || !Enum.IsDefined(settings.Mount))
            throw new ArgumentOutOfRangeException(nameof(settings), "Unknown action-camera lens or mount.");
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
            NearClip = Math.Clamp(settings.NearClip, 0.005f, 0.2f),
            HorizontalFieldOfView = Math.Clamp(settings.HorizontalFieldOfView, 40, 150),
            VerticalFieldOfView = Math.Clamp(settings.VerticalFieldOfView, 30, 120),
            DistortionStrength = Math.Clamp(settings.DistortionStrength, 0, 1),
            HorizonLeveling = Math.Clamp(settings.HorizonLeveling, 0, 1),
            UpOffset = Math.Clamp(settings.UpOffset, -0.25f, 0.4f),
            MotionSmoothing = Math.Clamp(settings.MotionSmoothing, 0, 1)
        };
    }

    public void SetSidelineSettings(SidelineCameraSettings settings)
    {
        if (!Enum.IsDefined(settings.Behavior) || !Enum.IsDefined(settings.Side))
            throw new ArgumentOutOfRangeException(nameof(settings), "Unknown sideline camera behavior.");
        if (settings.TargetPlayerId == Guid.Empty)
            throw new ArgumentException("A sideline target player cannot be empty.", nameof(settings));
        if (!Finite(settings.FramingOffset))
            throw new ArgumentException("The sideline framing offset must be finite.", nameof(settings));
        var minimum = Math.Clamp(settings.MinimumFocalLengthMm, 10, 300);
        var maximum = Math.Clamp(settings.MaximumFocalLengthMm, minimum, 400);
        SidelineSettings = settings with
        {
            CameraHeight = Math.Clamp(settings.CameraHeight, 0.5f, 15),
            SidelineDistance = Math.Clamp(settings.SidelineDistance, 0.5f, 20),
            MinimumFocalLengthMm = minimum,
            MaximumFocalLengthMm = maximum,
            FocalLengthMm = Math.Clamp(settings.FocalLengthMm, minimum, maximum),
            ZoomSpeed = Math.Clamp(settings.ZoomSpeed, 1, 100),
            PanDegrees = Math.Clamp(settings.PanDegrees, -180, 180),
            TiltDegrees = Math.Clamp(settings.TiltDegrees, -80, 80),
            TrackingStrength = Math.Clamp(settings.TrackingStrength, 0, 1),
            TargetScreenSize = Math.Clamp(settings.TargetScreenSize, 0.1f, 0.9f)
        };
    }

    public CameraDefinition Duplicate(string name)
    {
        var copy = new CameraDefinition(Guid.NewGuid(), name, Type);
        copy.SetFreeCamera(Position, RotationDegrees, FieldOfView);
        copy.SetPlayer(PlayerId);
        copy.SetPlayerPovSettings(PovSettings);
        copy.SetSidelineSettings(SidelineSettings);
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
