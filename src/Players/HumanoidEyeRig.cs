using System;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidEyeRig : Node3D
{
    public const float MaximumHorizontalGazeDegrees = 35f;
    public const float MaximumVerticalGazeDegrees = 25f;
    private const float GazeSpeedDegreesPerSecond = 180f;

    private static readonly SphereMesh EyeballMesh = new()
    {
        Radius = 0.055f,
        Height = 0.11f,
        RadialSegments = 12,
        Rings = 6
    };
    private static readonly CylinderMesh IrisMesh = new()
    {
        TopRadius = 0.025f,
        BottomRadius = 0.025f,
        Height = 0.006f,
        RadialSegments = 16
    };
    private static readonly CylinderMesh PupilMesh = new()
    {
        TopRadius = 0.012f,
        BottomRadius = 0.012f,
        Height = 0.008f,
        RadialSegments = 16
    };
    private static readonly BoxMesh EyelidMesh = new() { Size = new Vector3(0.125f, 0.052f, 0.024f) };

    private readonly Node3D[] _eyePivots = new Node3D[2];
    private readonly MeshInstance3D[] _eyeballs = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _irises = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _pupils = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _upperLids = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _lowerLids = new MeshInstance3D[2];
    private Node3D? _targetNode;
    private Vector3 _worldTarget;
    private bool _hasWorldTarget;
    private float _eyeSize = 1;
    private float _expressionLidClosure;
    private float _blinkAmount;
    private float _targetHorizontal;
    private float _targetVertical;
    private float _currentHorizontal;
    private float _currentVertical;

    public float HorizontalGazeDegrees => _currentHorizontal;
    public float VerticalGazeDegrees => _currentVertical;
    public float TargetHorizontalGazeDegrees => _targetHorizontal;
    public float TargetVerticalGazeDegrees => _targetVertical;
    public float BlinkAmount => _blinkAmount;
    public float EyelidClosure { get; private set; }
    public float EyeSeparation { get; private set; }
    public float EyeSize => _eyeSize;
    public bool HasSceneGazeTarget => _targetNode is not null || _hasWorldTarget;

    public override void _Ready()
    {
        BuildEye(0, "Left");
        BuildEye(1, "Right");
    }

    public void Configure(FaceAppearance face, AppearanceColor eyeColor, Material eyelidMaterial)
    {
        ArgumentNullException.ThrowIfNull(face);
        _eyeSize = face.EyeSize;
        var leftCenter = HumanoidFaceMesh.EyeCenter(face, true);
        var rightCenter = HumanoidFaceMesh.EyeCenter(face, false);
        EyeSeparation = leftCenter.DistanceTo(rightCenter);
        ConfigureEye(0, leftCenter, eyeColor, eyelidMaterial);
        ConfigureEye(1, rightCenter, eyeColor, eyelidMaterial);
        UpdateEyelids();
        ApplyGazeRotation();
    }

    public void SetExpressionPose(FacialExpressionPose pose)
    {
        _expressionLidClosure = pose.EyelidClosure;
        UpdateEyelids();
    }

    public void SetBlinkAmount(float amount)
    {
        _blinkAmount = Mathf.Clamp(amount, 0, 1);
        UpdateEyelids();
    }

    public void SetManualGaze(float horizontal, float vertical)
    {
        ClearGazeTarget();
        _targetHorizontal = Mathf.Clamp(horizontal, -1, 1) * MaximumHorizontalGazeDegrees;
        _targetVertical = Mathf.Clamp(vertical, -1, 1) * MaximumVerticalGazeDegrees;
    }

    public void LookAtNode(Node3D target)
    {
        _targetNode = target ?? throw new ArgumentNullException(nameof(target));
        _hasWorldTarget = false;
    }

    public void LookAtWorldPoint(Vector3 worldPoint)
    {
        _targetNode = null;
        _worldTarget = worldPoint;
        _hasWorldTarget = true;
    }

    public void ClearGazeTarget()
    {
        _targetNode = null;
        _hasWorldTarget = false;
    }

    public override void _Process(double delta)
    {
        UpdateTargetFromScene();
        var step = GazeSpeedDegreesPerSecond * (float)delta;
        _currentHorizontal = Mathf.MoveToward(_currentHorizontal, _targetHorizontal, step);
        _currentVertical = Mathf.MoveToward(_currentVertical, _targetVertical, step);
        ApplyGazeRotation();
    }

    private void BuildEye(int index, string side)
    {
        var pivot = new Node3D { Name = $"{side}EyePivot" };
        AddChild(pivot);
        _eyePivots[index] = pivot;

        _eyeballs[index] = AddMesh(pivot, $"{side}Eyeball", EyeballMesh);
        _irises[index] = AddMesh(pivot, $"{side}Iris", IrisMesh);
        _irises[index].Position = new Vector3(0, 0, -0.052f);
        _irises[index].RotationDegrees = new Vector3(90, 0, 0);
        _pupils[index] = AddMesh(pivot, $"{side}Pupil", PupilMesh);
        _pupils[index].Position = new Vector3(0, 0, -0.057f);
        _pupils[index].RotationDegrees = new Vector3(90, 0, 0);

        _upperLids[index] = AddMesh(this, $"{side}UpperLid", EyelidMesh);
        _lowerLids[index] = AddMesh(this, $"{side}LowerLid", EyelidMesh);
    }

    private void ConfigureEye(int index, Vector3 center, AppearanceColor eyeColor, Material eyelidMaterial)
    {
        _eyePivots[index].Position = center;
        _eyePivots[index].Scale = new Vector3(_eyeSize, _eyeSize * 0.72f, _eyeSize * 0.82f);
        _eyeballs[index].MaterialOverride = CreateMaterial(new Color("f1eee6"));
        _irises[index].MaterialOverride = CreateMaterial(ToGodot(eyeColor));
        _pupils[index].MaterialOverride = CreateMaterial(new Color("10151c"));
        _upperLids[index].MaterialOverride = eyelidMaterial;
        _lowerLids[index].MaterialOverride = eyelidMaterial;
        _upperLids[index].Position = center + new Vector3(0, 0.068f * _eyeSize, -0.06f);
        _lowerLids[index].Position = center + new Vector3(0, -0.068f * _eyeSize, -0.06f);
        _upperLids[index].Scale = new Vector3(_eyeSize, _eyeSize, _eyeSize);
        _lowerLids[index].Scale = new Vector3(_eyeSize, _eyeSize, _eyeSize);
    }

    private void UpdateEyelids()
    {
        if (_upperLids[0] is null)
            return;
        var expressionClosure = Mathf.Clamp(_expressionLidClosure, -0.35f, 1);
        var closure = Mathf.Lerp(expressionClosure, 1, _blinkAmount);
        EyelidClosure = closure;
        var openDistance = Mathf.Lerp(0.068f, 0.012f, Mathf.Max(closure, 0)) * _eyeSize;
        if (closure < 0)
            openDistance *= 1 - closure * 0.35f;
        for (var index = 0; index < 2; index++)
        {
            var center = _eyePivots[index].Position;
            _upperLids[index].Position = new Vector3(center.X, center.Y + openDistance, center.Z - 0.06f);
            _lowerLids[index].Position = new Vector3(center.X, center.Y - openDistance, center.Z - 0.06f);
        }
    }

    private void UpdateTargetFromScene()
    {
        Vector3? worldPoint = null;
        if (_targetNode is not null)
        {
            if (GodotObject.IsInstanceValid(_targetNode))
                worldPoint = _targetNode.GlobalPosition;
            else
                _targetNode = null;
        }
        else if (_hasWorldTarget)
        {
            worldPoint = _worldTarget;
        }

        if (!worldPoint.HasValue)
            return;
        var direction = worldPoint.Value - GlobalPosition;
        if (direction.LengthSquared() < 0.000001f)
            return;
        var local = GlobalTransform.Basis.Inverse() * direction.Normalized();
        _targetHorizontal = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(local.X, -local.Z)), -MaximumHorizontalGazeDegrees, MaximumHorizontalGazeDegrees);
        _targetVertical = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(local.Y, Mathf.Sqrt(local.X * local.X + local.Z * local.Z))), -MaximumVerticalGazeDegrees, MaximumVerticalGazeDegrees);
    }

    private void ApplyGazeRotation()
    {
        foreach (var pivot in _eyePivots)
        {
            if (pivot is not null)
                pivot.RotationDegrees = new Vector3(_currentVertical, -_currentHorizontal, 0);
        }
    }

    private static MeshInstance3D AddMesh(Node parent, string name, Mesh mesh)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh };
        parent.AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D CreateMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 0.65f
    };

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
