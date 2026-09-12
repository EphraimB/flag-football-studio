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
