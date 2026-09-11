using System;

namespace FlagFootballStudio.Domain;

public sealed class CameraCut
{
    public CameraCut(Guid id, Guid playId, Guid cameraId, double timeSeconds)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A camera cut ID cannot be empty.", nameof(id));
        if (playId == Guid.Empty)
            throw new ArgumentException("A camera cut requires a play.", nameof(playId));
        if (cameraId == Guid.Empty)
            throw new ArgumentException("A camera cut requires a camera.", nameof(cameraId));
        if (timeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), "Camera cut time cannot be negative.");

        Id = id;
        PlayId = playId;
        CameraId = cameraId;
        TimeSeconds = timeSeconds;
    }

    public Guid Id { get; }
    public Guid PlayId { get; }
    public Guid CameraId { get; }
    public double TimeSeconds { get; }
}
