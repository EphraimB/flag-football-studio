using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class CameraDirectorController : Node
{
    private Camera3D _camera = null!;
    private Node3D _cameraHome = null!;
    private IReadOnlyDictionary<Guid, Node3D> _pawns = null!;
    private int _cutGeneration;

    public void Configure(Camera3D camera, Node3D cameraHome, IReadOnlyDictionary<Guid, Node3D> pawns)
    {
        _camera = camera;
        _cameraHome = cameraHome;
        _pawns = pawns;
    }

    public void Preview(CameraDefinition definition, PlayDefinition play)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(play);
        var focus = FormationCenter(play);

        switch (definition.Type)
        {
            case CameraType.BroadcastWide:
                PlacePreset(focus + new Vector3(15, 18, 22), focus, 52);
                break;
            case CameraType.SidelineLow:
                PlacePreset(new Vector3(-13, 3.2f, focus.Z), focus + new Vector3(0, 1.2f, 0), 58);
                break;
            case CameraType.EndZone:
                PlacePreset(new Vector3(focus.X, 8.5f, -22), focus + new Vector3(0, 1, 3), 55);
                break;
            case CameraType.PlayerPov:
                PlacePlayerPov(definition);
                break;
            case CameraType.FreeCamera:
                EnsureCameraHome();
                _camera.Position = ToGodot(definition.Position);
                _camera.RotationDegrees = ToGodot(definition.RotationDegrees);
                _camera.Fov = definition.FieldOfView;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(definition), "Unknown camera type.");
        }
    }

    public async Task PlayCutsAsync(GameProject project, PlayDefinition play)
    {
        var generation = ++_cutGeneration;
        var previousTime = 0d;
        foreach (var cut in project.CameraCutsFor(play.Id))
        {
            var delay = cut.TimeSeconds - previousTime;
            if (delay > 0)
                await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
            if (generation != _cutGeneration)
                return;

            Preview(project.Camera(cut.CameraId), play);
            previousTime = cut.TimeSeconds;
        }
    }

    public void StopCuts() => _cutGeneration++;

    private void PlacePreset(Vector3 position, Vector3 target, float fov)
    {
        EnsureCameraHome();
        _camera.Position = position;
        _camera.Fov = fov;
        _camera.LookAt(target, Vector3.Up);
    }

    private void PlacePlayerPov(CameraDefinition definition)
    {
        if (!definition.PlayerId.HasValue || !_pawns.TryGetValue(definition.PlayerId.Value, out var pawn))
            throw new InvalidOperationException("Select a valid player for the Player POV camera.");

        var eyeAnchor = pawn is PlayerPawn playerPawn ? playerPawn.EyeAnchor : pawn;
        if (_camera.GetParent() != eyeAnchor)
            _camera.Reparent(eyeAnchor, false);
        _camera.Position = Vector3.Zero;
        _camera.RotationDegrees = Vector3.Zero;
        _camera.Fov = 75;
    }

    private void EnsureCameraHome()
    {
        if (_camera.GetParent() != _cameraHome)
            _camera.Reparent(_cameraHome, false);
    }

    private static Vector3 FormationCenter(PlayDefinition play)
    {
        if (play.StartingPositions.Count == 0)
            return new Vector3(0, 1, 0);
        return new Vector3(
            play.StartingPositions.Values.Average(point => point.X),
            1,
            play.StartingPositions.Values.Average(point => point.Y));
    }

    private static Vector3 ToGodot(CameraVector vector) => new(vector.X, vector.Y, vector.Z);
}
