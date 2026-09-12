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
    public const float MinimumFreeLookYawDegrees = -110f;
    public const float MaximumFreeLookYawDegrees = 110f;
    public const float MinimumFreeLookPitchDegrees = -75f;
    public const float MaximumFreeLookPitchDegrees = 65f;

    private Camera3D _camera = null!;
    private Node3D _cameraHome = null!;
    private IReadOnlyDictionary<Guid, Node3D> _pawns = null!;
    private Node3D? _football;
    private PlayerPawn? _povPawn;
    private Node3D? _povAnchor;
    private Node3D? _povMount;
    private uint _normalCullMask;
    private float _normalNearClip;
    private PlayerPovSettings _povSettings = PlayerPovSettings.Default;
    private Transform3D _neutralPovTransform = Transform3D.Identity;
    private Guid _activePovCameraId;
    private float _freeLookYaw;
    private float _freeLookPitch;
    private bool _recentering;
    private bool _mouseCaptured;
    private Input.MouseModeEnum _mouseModeBeforeCapture = Input.MouseModeEnum.Visible;
    private bool _povMountInitialized;
    private bool _configured;
    private int _cutGeneration;

    public bool IsPlayerPovActive => _povAnchor is not null;
    public Guid? ActivePovPlayerId => _povPawn?.Player?.Id;
    public PlayerPovMode ActivePovMode => _povSettings.Mode;
    public float FreeLookYawDegrees => _freeLookYaw;
    public float FreeLookPitchDegrees => _freeLookPitch + _povSettings.PitchOffsetDegrees;
    public bool IsRecentering => _recentering;
    public bool MouseCaptureActive => _mouseCaptured;
    public float PovTrackingError => _povAnchor is null || _povMount is null
        ? 0
        : _povAnchor.GlobalPosition.DistanceTo(_povMount.GlobalPosition);

    public void Configure(
        Camera3D camera,
        Node3D cameraHome,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        Node3D? football = null)
    {
        if (_configured)
            DeactivatePlayerPov();
        _camera = camera;
        _cameraHome = cameraHome;
        _pawns = pawns;
        _football = football;
        _normalCullMask = camera.CullMask;
        _normalNearClip = camera.Near;
        _configured = true;
        SetProcess(true);
        SetProcessUnhandledInput(true);
    }

    public override void _Process(double delta)
    {
        UpdatePlayerPovMount((float)delta);
        UpdatePlayerPovView((float)delta);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsPlayerPovActive || _povSettings.Mode != PlayerPovMode.FreeLook)
            return;

        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            ReleaseMouseCapture();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (inputEvent.IsActionPressed("pov_recenter"))
        {
            RecenterPlayerPov();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && !_mouseCaptured)
        {
            CaptureMouse();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (inputEvent is InputEventMouseMotion motion && _mouseCaptured)
        {
            ApplyFreeLookDelta(motion.Relative);
            GetViewport().SetInputAsHandled();
        }
    }

    public void ApplyFreeLookDelta(Vector2 mouseDelta)
    {
        if (!IsPlayerPovActive || _povSettings.Mode != PlayerPovMode.FreeLook)
            return;
        _recentering = false;
        _freeLookYaw -= mouseDelta.X * _povSettings.MouseSensitivity;
        _freeLookPitch -= mouseDelta.Y * _povSettings.MouseSensitivity;
        ClampFreeLook();
        ApplyViewRotation();
    }

    public void RecenterPlayerPov()
    {
        if (IsPlayerPovActive && _povSettings.Mode == PlayerPovMode.FreeLook)
            _recentering = true;
    }

    public void Preview(CameraDefinition definition, PlayDefinition play, bool captureFreeLook = true)
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
                PlacePlayerPov(definition, captureFreeLook);
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

    private void PlacePlayerPov(CameraDefinition definition, bool captureFreeLook)
    {
        if (!definition.PlayerId.HasValue || !_pawns.TryGetValue(definition.PlayerId.Value, out var pawn))
            throw new InvalidOperationException("Select a valid player for the Player POV camera.");

        var playerPawn = pawn as PlayerPawn;
        var sameCamera = _activePovCameraId == definition.Id && _povAnchor is not null && _povPawn == playerPawn;
        var modeChanged = _povSettings.Mode != definition.PovSettings.Mode;
        if (_povAnchor is not null && _povPawn != playerPawn)
            DeactivatePlayerPov();

        _povSettings = definition.PovSettings;
        if (!sameCamera || modeChanged)
        {
            _freeLookYaw = 0;
            _freeLookPitch = 0;
            _recentering = false;
        }
        _activePovCameraId = definition.Id;
        _povPawn = playerPawn;
        _povAnchor = playerPawn?.EyeAnchor ?? pawn;
        _povPawn?.SetFirstPersonView(true);
        _camera.CullMask = _normalCullMask & ~HumanoidRig.FirstPersonHeadLayerMask;
        _camera.Near = _povSettings.NearClip;
        _camera.Fov = 75;

        if (_povMount is null || !GodotObject.IsInstanceValid(_povMount))
        {
            _povMount = new Node3D { Name = "PlayerPovStabilizedMount" };
            pawn.AddChild(_povMount);
        }
        if (_camera.GetParent() != _povMount)
            _camera.Reparent(_povMount, false);
        _camera.Position = new Vector3(0, -0.01f, -_povSettings.ForwardOffset);
        _neutralPovTransform = CurrentAnchorTransform();
        _povMountInitialized = false;
        UpdatePlayerPovMount(0);
        ApplyViewRotation();
        UpdateMouseCaptureForMode(captureFreeLook);
    }

    private void EnsureCameraHome()
    {
        if (_camera.GetParent() != _cameraHome)
            _camera.Reparent(_cameraHome, false);
    }

    private void UpdatePlayerPovMount(float delta)
    {
        if (!_configured || _povAnchor is null || _povMount is null)
            return;
        if (!GodotObject.IsInstanceValid(_povAnchor) || !GodotObject.IsInstanceValid(_povMount))
        {
            DeactivatePlayerPov();
            return;
        }

        var currentAnchor = CurrentAnchorTransform();
        var bob = _povSettings.HeadBobStrength;
        var targetOrigin = _neutralPovTransform.Origin.Lerp(currentAnchor.Origin, bob);
        var targetRotation = _neutralPovTransform.Basis.GetRotationQuaternion()
            .Slerp(currentAnchor.Basis.GetRotationQuaternion(), bob)
            .Normalized();
        var target = new Transform3D(new Basis(targetRotation), targetOrigin);
        if (!_povMountInitialized || delta <= 0)
        {
            _povMount.Transform = target;
            _povMountInitialized = true;
            return;
        }

        var followRate = Mathf.Lerp(30f, 5f, _povSettings.StabilizationStrength);
        var weight = 1 - Mathf.Exp(-followRate * delta);
        var origin = _povMount.Position.Lerp(target.Origin, weight);
        var rotation = _povMount.Quaternion.Slerp(target.Basis.GetRotationQuaternion(), weight).Normalized();
        _povMount.Transform = new Transform3D(new Basis(rotation), origin);
    }

    private Transform3D CurrentAnchorTransform()
    {
        var current = _povMount!.GetParent<Node3D>().GlobalTransform.AffineInverse() * _povAnchor!.GlobalTransform;
        return new Transform3D(current.Basis.Orthonormalized(), current.Origin);
    }

    private void UpdatePlayerPovView(float delta)
    {
        if (!IsPlayerPovActive || _povMount is null)
            return;

        if (_povSettings.Mode == PlayerPovMode.FreeLook)
        {
            var stick = Input.GetVector("pov_look_left", "pov_look_right", "pov_look_up", "pov_look_down");
            if (stick.LengthSquared() > 0.001f)
            {
                _recentering = false;
                _freeLookYaw -= stick.X * _povSettings.ControllerSensitivity * delta;
                _freeLookPitch -= stick.Y * _povSettings.ControllerSensitivity * delta;
                ClampFreeLook();
            }
            if (_recentering)
                UpdateRecenter(delta);
        }
        else if (_povSettings.Mode == PlayerPovMode.LookAtTarget && TryResolveLookTarget(out var target))
        {
            var localDirection = _povMount.GlobalBasis.Inverse() * (target - _camera.GlobalPosition).Normalized();
            var desiredYaw = Mathf.RadToDeg(Mathf.Atan2(-localDirection.X, -localDirection.Z));
            var desiredPitch = Mathf.RadToDeg(Mathf.Atan2(
                localDirection.Y,
                Mathf.Sqrt(localDirection.X * localDirection.X + localDirection.Z * localDirection.Z)));
            var weight = 1 - Mathf.Exp(-7f * delta);
            _freeLookYaw = Mathf.Lerp(_freeLookYaw, Mathf.Clamp(desiredYaw, MinimumFreeLookYawDegrees, MaximumFreeLookYawDegrees), weight);
            _freeLookPitch = Mathf.Lerp(
                _freeLookPitch,
                Mathf.Clamp(desiredPitch, MinimumFreeLookPitchDegrees, MaximumFreeLookPitchDegrees) - _povSettings.PitchOffsetDegrees,
                weight);
            ClampFreeLook();
        }
        else if (_povSettings.Mode == PlayerPovMode.LockedForward)
        {
            _freeLookYaw = 0;
            _freeLookPitch = 0;
        }

        ApplyViewRotation();
    }

    private void UpdateRecenter(float delta)
    {
        var weight = 1 - Mathf.Exp(-8f * delta);
        _freeLookYaw = Mathf.Lerp(_freeLookYaw, 0, weight);
        _freeLookPitch = Mathf.Lerp(_freeLookPitch, 0, weight);
        if (Mathf.Abs(_freeLookYaw) < 0.05f && Mathf.Abs(_freeLookPitch) < 0.05f)
        {
            _freeLookYaw = 0;
            _freeLookPitch = 0;
            _recentering = false;
        }
    }

    private bool TryResolveLookTarget(out Vector3 target)
    {
        switch (_povSettings.TargetKind)
        {
            case PlayerPovTargetKind.Player when _povSettings.TargetPlayerId.HasValue &&
                                                     _pawns.TryGetValue(_povSettings.TargetPlayerId.Value, out var player):
                target = player is PlayerPawn pawn ? pawn.EyeAnchor.GlobalPosition : player.GlobalPosition;
                return true;
            case PlayerPovTargetKind.Football when _football is not null && GodotObject.IsInstanceValid(_football):
                target = _football.GlobalPosition;
                return true;
            case PlayerPovTargetKind.WorldPoint:
                target = ToGodot(_povSettings.WorldTarget);
                return true;
            default:
                target = default;
                return false;
        }
    }

    private void ClampFreeLook()
    {
        _freeLookYaw = Mathf.Clamp(_freeLookYaw, MinimumFreeLookYawDegrees, MaximumFreeLookYawDegrees);
        _freeLookPitch = Mathf.Clamp(
            _freeLookPitch,
            MinimumFreeLookPitchDegrees - _povSettings.PitchOffsetDegrees,
            MaximumFreeLookPitchDegrees - _povSettings.PitchOffsetDegrees);
    }

    private void ApplyViewRotation()
    {
        if (_camera is null || !GodotObject.IsInstanceValid(_camera))
            return;
        _camera.RotationDegrees = new Vector3(
            _povSettings.PitchOffsetDegrees + _freeLookPitch,
            _freeLookYaw,
            0);
    }

    private void UpdateMouseCaptureForMode(bool captureFreeLook)
    {
        if (IsPlayerPovActive && _povSettings.Mode == PlayerPovMode.FreeLook && captureFreeLook)
            CaptureMouse();
        else if (_povSettings.Mode != PlayerPovMode.FreeLook)
            ReleaseMouseCapture();
    }

    private void CaptureMouse()
    {
        if (_mouseCaptured)
            return;
        _mouseModeBeforeCapture = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseCaptured = true;
    }

    private void ReleaseMouseCapture()
    {
        if (!_mouseCaptured)
            return;
        Input.MouseMode = _mouseModeBeforeCapture == Input.MouseModeEnum.Captured
            ? Input.MouseModeEnum.Visible
            : _mouseModeBeforeCapture;
        _mouseCaptured = false;
    }

    private void DeactivatePlayerPov()
    {
        if (!_configured)
            return;

        ReleaseMouseCapture();
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
        _activePovCameraId = Guid.Empty;
        _freeLookYaw = 0;
        _freeLookPitch = 0;
        _recentering = false;
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
