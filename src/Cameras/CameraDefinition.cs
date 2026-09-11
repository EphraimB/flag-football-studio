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

public readonly record struct CameraVector(float X, float Y, float Z);

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
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public CameraType Type { get; private set; }
    public CameraVector Position { get; private set; }
    public CameraVector RotationDegrees { get; private set; }
    public float FieldOfView { get; private set; }
    public Guid? PlayerId { get; private set; }

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
    }

    public CameraDefinition Duplicate(string name)
    {
        var copy = new CameraDefinition(Guid.NewGuid(), name, Type);
        copy.SetFreeCamera(Position, RotationDegrees, FieldOfView);
        copy.SetPlayer(PlayerId);
        return copy;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A camera name is required.", nameof(name));
        return name.Trim();
    }
}
