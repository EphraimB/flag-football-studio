using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerPawn : Node3D
{
    private PlayerAppearance _appearance = null!;
    private UniformDefinition _uniform = null!;
    private HumanoidRig _rig = null!;
    private float _formationRotationY;

    public Player? Player { get; private set; }
    public Node3D EyeAnchor => _rig.EyeAnchor;
    public Node3D CatchAnchor => _rig.CatchAnchor;
    public HumanoidAnimationState AnimationState => _rig.AnimationState;
    public FacialExpressionState FacialExpression => _rig.FacialExpression;
    public FacialExpressionPose FacialPose => _rig.FacialPose;
    public float BlinkAmount => _rig.BlinkAmount;
    public int BlinkCount => _rig.BlinkCount;
    public float HorizontalGazeDegrees => _rig.HorizontalGazeDegrees;
    public float VerticalGazeDegrees => _rig.VerticalGazeDegrees;
    public float TargetHorizontalGazeDegrees => _rig.TargetHorizontalGazeDegrees;
    public float TargetVerticalGazeDegrees => _rig.TargetVerticalGazeDegrees;

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

    public void SetFormationFacing(float rotationY)
    {
        _formationRotationY = rotationY;
        Rotation = new Vector3(0, rotationY, 0);
    }

    public void ResetPresentationPose()
    {
        Rotation = new Vector3(0, _formationRotationY, 0);
        SetAnimationState(HumanoidAnimationState.Idle);
    }

    public void FaceToward(Vector3 globalTarget)
    {
        var target = new Vector3(globalTarget.X, GlobalPosition.Y, globalTarget.Z);
        if (GlobalPosition.DistanceSquaredTo(target) > 0.0001f)
            LookAt(target, Vector3.Up);
    }

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
}
