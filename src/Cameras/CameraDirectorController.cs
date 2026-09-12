using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class CameraDirectorController : Node
{
    public const float DefaultPlayerPovNearClip = 0.025f;
    public const float DefaultPlayerPovPitchDegrees = -6f;

    private Camera3D _camera = null!;
    private Node3D _cameraHome = null!;
    private IReadOnlyDictionary<Guid, Node3D> _pawns = null!;
    private PlayerPawn? _povPawn;
    private Node3D? _povAnchor;
    private Node3D? _povMount;
    private uint _normalCullMask;
    private float _normalNearClip;
    private bool _povMountInitialized;
    private bool _configured;
    private int _cutGeneration;

    public float PlayerPovNearClip { get; set; } = DefaultPlayerPovNearClip;
    public float PlayerPovPitchDegrees { get; set; } = DefaultPlayerPovPitchDegrees;
    public float PlayerPovStabilizationRate { get; set; } = 12f;
    public bool IsPlayerPovActive => _povAnchor is not null;
    public Guid? ActivePovPlayerId => _povPawn?.Player?.Id;
    public float PovTrackingError => _povAnchor is null || _povMount is null
        ? 0
        : _povAnchor.GlobalPosition.DistanceTo(_povMount.GlobalPosition);

    public void Configure(Camera3D camera, Node3D cameraHome, IReadOnlyDictionary<Guid, Node3D> pawns)
    {
        if (_configured)
            DeactivatePlayerPov();
        _camera = camera;
        _cameraHome = cameraHome;
        _pawns = pawns;
        _normalCullMask = camera.CullMask;
        _normalNearClip = camera.Near;
        _configured = true;
        SetProcess(true);
    }

    public override void _Process(double delta) => UpdatePlayerPov((float)delta);

    public void Preview(CameraDefinition definition, PlayDefinition play)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(play);
        var focus = FormationCenter(play);

        if (definition.Type != CameraType.PlayerPov)
            DeactivatePlayerPov();

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

        var playerPawn = pawn as PlayerPawn;
        if (_povAnchor is not null && _povPawn != playerPawn)
            DeactivatePlayerPov();

        _povPawn = playerPawn;
        _povAnchor = playerPawn?.EyeAnchor ?? pawn;
        _povPawn?.SetFirstPersonView(true);
        _camera.CullMask = _normalCullMask & ~HumanoidRig.FirstPersonHeadLayerMask;
        _camera.Near = Mathf.Clamp(PlayerPovNearClip, 0.005f, 0.2f);
        _camera.Fov = 75;

        if (_povMount is null || !GodotObject.IsInstanceValid(_povMount))
        {
            _povMount = new Node3D { Name = "PlayerPovStabilizedMount" };
            pawn.AddChild(_povMount);
        }
        if (_camera.GetParent() != _povMount)
            _camera.Reparent(_povMount, false);
        _camera.Position = new Vector3(0, -0.01f, -0.055f);
        _camera.RotationDegrees = new Vector3(Mathf.Clamp(PlayerPovPitchDegrees, -60, 25), 0, 0);
        _povMountInitialized = false;
        UpdatePlayerPov(0);
    }

    private void EnsureCameraHome()
    {
        if (_camera.GetParent() != _cameraHome)
            _camera.Reparent(_cameraHome, false);
    }

    private void UpdatePlayerPov(float delta)
    {
        if (!_configured || _povAnchor is null || _povMount is null)
            return;
        if (!GodotObject.IsInstanceValid(_povAnchor) || !GodotObject.IsInstanceValid(_povMount))
        {
            DeactivatePlayerPov();
            return;
        }

        var target = _povMount.GetParent<Node3D>().GlobalTransform.AffineInverse() * _povAnchor.GlobalTransform;
        target = new Transform3D(target.Basis.Orthonormalized(), target.Origin);
        if (!_povMountInitialized || delta <= 0)
        {
            _povMount.Transform = target;
            _povMountInitialized = true;
            return;
        }

        var weight = 1 - Mathf.Exp(-Mathf.Max(1, PlayerPovStabilizationRate) * delta);
        var origin = _povMount.Position.Lerp(target.Origin, weight);
        var rotation = _povMount.Quaternion.Slerp(target.Basis.GetRotationQuaternion(), weight).Normalized();
        _povMount.Transform = new Transform3D(new Basis(rotation), origin);
    }

    private void DeactivatePlayerPov()
    {
        if (!_configured)
            return;

        if (_povPawn is not null && GodotObject.IsInstanceValid(_povPawn))
            _povPawn.SetFirstPersonView(false);
        if (_camera is not null && GodotObject.IsInstanceValid(_camera))
        {
            _camera.CullMask = _normalCullMask;
            _camera.Near = _normalNearClip;
            if (_cameraHome is not null && GodotObject.IsInstanceValid(_cameraHome) && _camera.GetParent() != _cameraHome)
                _camera.Reparent(_cameraHome, true);
        }
        if (_povMount is not null && GodotObject.IsInstanceValid(_povMount))
            _povMount.QueueFree();

        _povPawn = null;
        _povAnchor = null;
        _povMount = null;
        _povMountInitialized = false;
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
