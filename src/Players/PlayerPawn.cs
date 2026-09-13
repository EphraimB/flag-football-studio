using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerPawn : Node3D
{
    private PlayerAppearance _appearance = null!;
    private UniformDefinition _uniform = null!;
    private HumanoidRig _rig = null!;
    private float _formationRotationY;
    private Tween? _facingTween;

    public Player? Player { get; private set; }
    public Node3D EyeAnchor => _rig.EyeAnchor;
    public Node3D CatchAnchor => _rig.CatchAnchor;
    public Node3D MouthAudioAnchor => _rig.MouthAudioAnchor;
    public HumanoidAnimationState AnimationState => _rig.AnimationState;
    public FacialExpressionState FacialExpression => _rig.FacialExpression;
    public FacialExpressionPose FacialPose => _rig.FacialPose;
    public float BlinkAmount => _rig.BlinkAmount;
    public int BlinkCount => _rig.BlinkCount;
    public float HorizontalGazeDegrees => _rig.HorizontalGazeDegrees;
    public float VerticalGazeDegrees => _rig.VerticalGazeDegrees;
    public float TargetHorizontalGazeDegrees => _rig.TargetHorizontalGazeDegrees;
    public float TargetVerticalGazeDegrees => _rig.TargetVerticalGazeDegrees;
    public bool HasSceneGazeTarget => _rig.HasSceneGazeTarget;
    public SpeechMouthShape MouthShape => _rig.MouthShape;
    public SpeechMouthPose MouthPose => _rig.MouthPose;
    public bool IsSpeechShapeCycling => _rig.IsSpeechShapeCycling;
    public Vector3 ForwardDirection => -GlobalBasis.Z.Normalized();
    public bool FirstPersonViewActive => _rig.FirstPersonViewActive;

    public void Configure(Player player, PlayerAppearance appearance, UniformDefinition uniform)
    {
        Player = player;
        _appearance = appearance;
        _uniform = uniform;
    }

    public override void _Ready()
    {
        _rig = new HumanoidRig { Name = "HumanoidRig" };
        AddChild(_rig);
        ApplyCurrentPresentation();
    }

    public void SetFormationFacingDirection(Vector3 worldDirection)
    {
        _facingTween?.Kill();
        _formationRotationY = YawFor(worldDirection);
        Rotation = new Vector3(0, _formationRotationY, 0);
    }

    public void ResetPresentationPose()
    {
        _facingTween?.Kill();
        Rotation = new Vector3(0, _formationRotationY, 0);
        SetAnimationState(HumanoidAnimationState.Idle);
    }

    public void FaceToward(Vector3 globalTarget, float turnSeconds = 0.16f)
    {
        var target = new Vector3(globalTarget.X, GlobalPosition.Y, globalTarget.Z);
        var direction = target - GlobalPosition;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        var targetYaw = YawFor(direction);
        _facingTween?.Kill();
        if (turnSeconds <= 0)
        {
            Rotation = new Vector3(0, targetYaw, 0);
            return;
        }

        var startYaw = Rotation.Y;
        var endYaw = startYaw + Mathf.AngleDifference(startYaw, targetYaw);
        _facingTween = CreateTween();
        _facingTween.TweenProperty(this, "rotation:y", endYaw, turnSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    public float FacingAlignment(Vector3 worldDirection)
    {
        var planar = new Vector3(worldDirection.X, 0, worldDirection.Z);
        return planar.LengthSquared() <= 0.0001f ? 1 : ForwardDirection.Dot(planar.Normalized());
    }

    public void SetFirstPersonView(bool active) => _rig.SetFirstPersonView(active);
    public bool HeadGeometryVisibleTo(Camera3D camera) => _rig.HeadGeometryVisibleTo(camera);
    public bool BodyGeometryVisibleTo(Camera3D camera) => _rig.BodyGeometryVisibleTo(camera);
    public Aabb BodyBounds => _rig.BodyBounds;

    public void SetAnimationState(HumanoidAnimationState state, bool restart = false) => _rig.SetAnimationState(state, restart);
    public void SetFacialExpression(FacialExpressionState expression, float blendSeconds = FacialExpressionController.DefaultBlendSeconds) =>
        _rig.SetFacialExpression(expression, blendSeconds);
    public void SetEyebrowControl(float raise, float tilt) => _rig.SetEyebrowControl(raise, tilt);
    public void TriggerBlink(float durationSeconds = FacialExpressionController.DefaultBlinkSeconds) => _rig.TriggerBlink(durationSeconds);
    public void SetAutomaticBlink(bool enabled, float intervalSeconds = FacialExpressionController.DefaultAutomaticBlinkInterval) =>
        _rig.SetAutomaticBlink(enabled, intervalSeconds);
    public void SetManualGaze(float horizontal, float vertical) => _rig.SetManualGaze(horizontal, vertical);
    public void LookAtPlayer(PlayerPawn player) => _rig.LookAtGazeTarget(player);
    public void LookAtFootball(Node3D football) => _rig.LookAtGazeTarget(football);
    public void LookAtWorldPoint(Vector3 worldPoint) => _rig.LookAtWorldPoint(worldPoint);
    public void ClearGazeTarget() => _rig.ClearGazeTarget();
    public void SetMouthShape(SpeechMouthShape shape, float blendSeconds = SpeechMouthController.DefaultBlendSeconds) =>
        _rig.SetMouthShape(shape, blendSeconds);
    public void SetMouthShapeWeighted(SpeechMouthShape shape, float strength, float blendSeconds = SpeechMouthController.DefaultBlendSeconds) =>
        _rig.SetMouthShapeWeighted(shape, strength, blendSeconds);
    public void SetMouthControls(float jawOpen, float width, float lipFullness, float upperLip, float lowerLip) =>
        _rig.SetMouthControls(jawOpen, width, lipFullness, upperLip, lowerLip);
    public void StartSpeechShapeCycle(float holdSeconds = SpeechMouthController.DefaultCycleHoldSeconds) =>
        _rig.StartSpeechShapeCycle(holdSeconds);

    public void ApplyAppearance(PlayerAppearance appearance)
    {
        _appearance = appearance;
        ApplyCurrentPresentation();
    }

    public void ApplyUniform(UniformDefinition uniform)
    {
        _uniform = uniform;
        ApplyCurrentPresentation();
    }

    public void ApplyPresentation(PlayerAppearance appearance, UniformDefinition uniform)
    {
        _appearance = appearance;
        _uniform = uniform;
        ApplyCurrentPresentation();
    }

    private void ApplyCurrentPresentation()
    {
        if (IsNodeReady() && Player is not null)
            _rig.Apply(Player, _appearance, _uniform);
    }

    private static float YawFor(Vector3 worldDirection)
    {
        var direction = new Vector3(worldDirection.X, 0, worldDirection.Z).Normalized();
        return Mathf.Atan2(-direction.X, -direction.Z);
    }
}
