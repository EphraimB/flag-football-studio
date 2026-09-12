using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidRig : Node3D
{
    private readonly Dictionary<string, BoneAttachment3D> _attachments = [];
    private readonly List<Node> _generatedVisuals = [];
    private Skeleton3D _skeleton = null!;
    private HumanoidAnimator _animator = null!;
    private Node3D _eyeAnchor = null!;
    private Node3D _catchAnchor = null!;

    public Node3D EyeAnchor => _eyeAnchor;
    public Node3D CatchAnchor => _catchAnchor;
    public HumanoidAnimationState AnimationState => _animator.State;

    public override void _Ready()
    {
        BuildSkeleton();
        _animator = new HumanoidAnimator { Name = "Animator" };
        AddChild(_animator);
        _animator.Configure(_skeleton);
    }

    public void Apply(Player player, PlayerAppearance appearance, UniformDefinition uniform)
    {
        if (!IsNodeReady())
            throw new InvalidOperationException("The humanoid rig must be in the scene tree before it is configured.");

        ClearGeneratedVisuals();
        var buildScale = BodyBuildScale(appearance.BodyBuild);
        Scale = new Vector3(buildScale, appearance.HeightMeters / 1.8f, buildScale);

        var primary = CreateMaterial(ToGodot(uniform.PrimaryColor));
        var secondary = CreateMaterial(ToGodot(uniform.SecondaryColor));
        var accent = CreateMaterial(ToGodot(uniform.AccentColor));
        var jersey = CreateMaterial(ToGodot(uniform.JerseyBaseColor));
        var sleeveTrim = CreateMaterial(ToGodot(uniform.SleeveTrimColor));
        var collarTrim = CreateMaterial(ToGodot(uniform.CollarTrimColor));
        var shorts = CreateMaterial(ToGodot(uniform.ShortsColor));
        var skin = CreateMaterial(ToGodot(appearance.SkinTone));
        var hair = CreateMaterial(ToGodot(appearance.HairColor));
        var flag = CreateMaterial(ToGodot(uniform.FlagColor));

        AddMesh(HumanoidSkeletonDefinition.Hips, "Shorts", new BoxMesh { Size = new Vector3(0.64f, 0.35f, 0.42f) }, new Vector3(0, -0.08f, 0), shorts);
        AddMesh(HumanoidSkeletonDefinition.Spine, "JerseyBody", new CapsuleMesh { Radius = 0.37f, Height = 0.78f }, new Vector3(0, 0.16f, 0), jersey, new Vector3(1, 0.9f, 0.72f));
        AddMesh(HumanoidSkeletonDefinition.Chest, "ShoulderPad", new BoxMesh { Size = new Vector3(1.02f, 0.24f, 0.48f) }, new Vector3(0, 0.08f, 0), primary);
        AddMesh(HumanoidSkeletonDefinition.Chest, "AccentStripe", new BoxMesh { Size = new Vector3(0.1f, 0.55f, 0.035f) }, new Vector3(0, -0.12f, -0.37f), accent);
        AddMesh(HumanoidSkeletonDefinition.Chest, "Collar", new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.24f, Height = 0.08f }, new Vector3(0, 0.27f, 0), collarTrim);

        AddLimb(HumanoidSkeletonDefinition.LeftUpperArm, "LeftUpperArmMesh", 0.14f, 0.48f, jersey);
        AddLimb(HumanoidSkeletonDefinition.RightUpperArm, "RightUpperArmMesh", 0.14f, 0.48f, jersey);
        AddMesh(HumanoidSkeletonDefinition.LeftUpperArm, "LeftSleeveTrim", new CylinderMesh { TopRadius = 0.155f, BottomRadius = 0.155f, Height = 0.11f }, new Vector3(0, -0.38f, 0), sleeveTrim);
        AddMesh(HumanoidSkeletonDefinition.RightUpperArm, "RightSleeveTrim", new CylinderMesh { TopRadius = 0.155f, BottomRadius = 0.155f, Height = 0.11f }, new Vector3(0, -0.38f, 0), sleeveTrim);
        AddLimb(HumanoidSkeletonDefinition.LeftLowerArm, "LeftForearm", 0.115f, 0.42f, skin);
        AddLimb(HumanoidSkeletonDefinition.RightLowerArm, "RightForearm", 0.115f, 0.42f, skin);
        AddMesh(HumanoidSkeletonDefinition.LeftHand, "LeftHandMesh", new SphereMesh { Radius = 0.14f, Height = 0.28f }, Vector3.Zero, skin);
        AddMesh(HumanoidSkeletonDefinition.RightHand, "RightHandMesh", new SphereMesh { Radius = 0.14f, Height = 0.28f }, Vector3.Zero, skin);

        AddLimb(HumanoidSkeletonDefinition.LeftUpperLeg, "LeftShortLeg", 0.18f, 0.52f, shorts);
        AddLimb(HumanoidSkeletonDefinition.RightUpperLeg, "RightShortLeg", 0.18f, 0.52f, shorts);
        AddLimb(HumanoidSkeletonDefinition.LeftLowerLeg, "LeftLowerLegMesh", 0.135f, 0.46f, skin);
        AddLimb(HumanoidSkeletonDefinition.RightLowerLeg, "RightLowerLegMesh", 0.135f, 0.46f, skin);
        AddMesh(HumanoidSkeletonDefinition.LeftFoot, "LeftShoe", new BoxMesh { Size = new Vector3(0.27f, 0.18f, 0.46f) }, new Vector3(0, 0, -0.1f), secondary);
        AddMesh(HumanoidSkeletonDefinition.RightFoot, "RightShoe", new BoxMesh { Size = new Vector3(0.27f, 0.18f, 0.46f) }, new Vector3(0, 0, -0.1f), secondary);

        AddMesh(HumanoidSkeletonDefinition.Head, "HeadMesh", new SphereMesh { Radius = 0.32f, Height = 0.64f }, new Vector3(0, 0.13f, 0), skin);
        AddMesh(HumanoidSkeletonDefinition.Hips, "Belt", new BoxMesh { Size = new Vector3(0.7f, 0.1f, 0.45f) }, new Vector3(0, 0.12f, 0), secondary);
        AddMesh(HumanoidSkeletonDefinition.Hips, "LeftFlag", new BoxMesh { Size = new Vector3(0.1f, 0.5f, 0.055f) }, new Vector3(-0.42f, -0.08f, 0), flag);
        AddMesh(HumanoidSkeletonDefinition.Hips, "RightFlag", new BoxMesh { Size = new Vector3(0.1f, 0.5f, 0.055f) }, new Vector3(0.42f, -0.08f, 0), flag);

        AddHair(appearance.HairStyle, hair);
        AddAccessories(appearance.Accessories, secondary);
        AddUniformLabels(player, uniform);
    }

    public void SetAnimationState(HumanoidAnimationState state, bool restart = false) => _animator.SetState(state, restart);

    private void BuildSkeleton()
    {
        _skeleton = new Skeleton3D { Name = "HumanoidSkeleton" };
        AddChild(_skeleton);
        var indices = new Dictionary<string, int>();
        foreach (var bone in HumanoidSkeletonDefinition.Bones)
        {
            var index = _skeleton.GetBoneCount();
            _skeleton.AddBone(bone.Name);
            indices[bone.Name] = index;
            if (bone.Parent is not null)
                _skeleton.SetBoneParent(index, indices[bone.Parent]);
            _skeleton.SetBoneRest(index, new Transform3D(Basis.Identity, bone.RestPosition));

            var attachment = new BoneAttachment3D
            {
                Name = $"{bone.Name}Attachment",
                BoneName = bone.Name
            };
            _skeleton.AddChild(attachment);
            _attachments[bone.Name] = attachment;
        }

        _eyeAnchor = new Node3D
        {
            Name = "EyeAnchor",
            Position = new Vector3(0, 0.19f, -0.29f)
        };
        _attachments[HumanoidSkeletonDefinition.Head].AddChild(_eyeAnchor);

        _catchAnchor = new Node3D
        {
            Name = "CatchAnchor",
            Position = new Vector3(0, -0.05f, -0.12f)
        };
        _attachments[HumanoidSkeletonDefinition.RightHand].AddChild(_catchAnchor);
    }

    private void AddLimb(string boneName, string meshName, float radius, float height, Material material) =>
        AddMesh(boneName, meshName, new CapsuleMesh { Radius = radius, Height = height }, new Vector3(0, -height * 0.48f, 0), material);

    private void AddHair(HairStyle style, Material material)
    {
        switch (style)
        {
            case HairStyle.None:
                return;
            case HairStyle.Short:
                AddMesh(HumanoidSkeletonDefinition.Head, "ShortHair", new SphereMesh { Radius = 0.33f, Height = 0.66f }, new Vector3(0, 0.3f, 0.02f), material, new Vector3(1.03f, 0.48f, 1.03f));
                break;
            case HairStyle.Curly:
                for (var index = 0; index < 6; index++)
                {
                    var angle = Mathf.Tau * index / 6f;
                    AddMesh(HumanoidSkeletonDefinition.Head, $"Curl{index}", new SphereMesh { Radius = 0.13f, Height = 0.26f }, new Vector3(Mathf.Cos(angle) * 0.2f, 0.36f, Mathf.Sin(angle) * 0.18f), material);
                }
                break;
            case HairStyle.Mohawk:
                AddMesh(HumanoidSkeletonDefinition.Head, "Mohawk", new BoxMesh { Size = new Vector3(0.14f, 0.35f, 0.65f) }, new Vector3(0, 0.42f, 0), material);
                break;
            case HairStyle.Bun:
                AddMesh(HumanoidSkeletonDefinition.Head, "HairCap", new SphereMesh { Radius = 0.33f, Height = 0.66f }, new Vector3(0, 0.29f, 0.04f), material, new Vector3(1.02f, 0.5f, 1.02f));
                AddMesh(HumanoidSkeletonDefinition.Head, "Bun", new SphereMesh { Radius = 0.19f, Height = 0.38f }, new Vector3(0, 0.3f, 0.34f), material);
                break;
        }
    }

    private void AddAccessories(PlayerAccessories accessories, Material secondary)
    {
        if (accessories.HasFlag(PlayerAccessories.Headband))
            AddMesh(HumanoidSkeletonDefinition.Head, "Headband", new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.34f, Height = 0.09f }, new Vector3(0, 0.17f, 0), secondary);
        if (accessories.HasFlag(PlayerAccessories.Wristbands))
        {
            AddMesh(HumanoidSkeletonDefinition.LeftLowerArm, "LeftWristband", new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.1f }, new Vector3(0, -0.34f, 0), secondary);
            AddMesh(HumanoidSkeletonDefinition.RightLowerArm, "RightWristband", new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.1f }, new Vector3(0, -0.34f, 0), secondary);
        }
        if (accessories.HasFlag(PlayerAccessories.Visor))
            AddMesh(HumanoidSkeletonDefinition.Head, "Visor", new BoxMesh { Size = new Vector3(0.48f, 0.16f, 0.055f) }, new Vector3(0, 0.16f, -0.29f), CreateMaterial(new Color(0.2f, 0.7f, 0.95f, 0.72f), true));
        if (accessories.HasFlag(PlayerAccessories.ArmSleeves))
        {
            AddLimb(HumanoidSkeletonDefinition.LeftLowerArm, "LeftArmSleeve", 0.125f, 0.32f, secondary);
            AddLimb(HumanoidSkeletonDefinition.RightLowerArm, "RightArmSleeve", 0.125f, 0.32f, secondary);
        }
    }

    private void AddUniformLabels(Player player, UniformDefinition uniform)
    {
        AddLabel("JerseyNumber", player.JerseyNumber.ToString(), new Vector3(0, 0, -0.39f), 44, uniform.NumberColor, uniform.NumberOutlineColor, 7);
        AddLabel("Wordmark", uniform.TeamWordmark, new Vector3(0, 0.2f, -0.39f), 19, uniform.NumberColor);
        if (uniform.ShowPlayerNameOnBack)
        {
            var label = AddLabel("PlayerName", player.Name.ToUpperInvariant(), new Vector3(0, 0.2f, 0.39f), 16, uniform.NumberColor);
            label.RotationDegrees = new Vector3(0, 180, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        }
    }

    private Label3D AddLabel(string name, string text, Vector3 position, int fontSize, AppearanceColor color, AppearanceColor? outline = null, int outlineSize = 0)
    {
        var label = new Label3D
        {
            Name = name,
            Text = text,
            Position = position,
            FontSize = fontSize,
            OutlineSize = outlineSize,
            Modulate = ToGodot(color),
            OutlineModulate = outline.HasValue ? ToGodot(outline.Value) : Colors.Transparent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        _attachments[HumanoidSkeletonDefinition.Chest].AddChild(label);
        _generatedVisuals.Add(label);
        return label;
    }

    private void AddMesh(string boneName, string nodeName, Mesh mesh, Vector3 position, Material material, Vector3? scale = null)
    {
        var instance = new MeshInstance3D
        {
            Name = nodeName,
            Mesh = mesh,
            Position = position,
            Scale = scale ?? Vector3.One,
            MaterialOverride = material
        };
        _attachments[boneName].AddChild(instance);
        _generatedVisuals.Add(instance);
    }

    private void ClearGeneratedVisuals()
    {
        foreach (var visual in _generatedVisuals)
        {
            visual.GetParent()?.RemoveChild(visual);
            visual.QueueFree();
        }
        _generatedVisuals.Clear();
    }

    private static float BodyBuildScale(BodyBuild build) => build switch
    {
        BodyBuild.Slim => 0.82f,
        BodyBuild.Stocky => 1.2f,
        _ => 1f
    };

    private static StandardMaterial3D CreateMaterial(Color color, bool transparent = false) => new()
    {
        AlbedoColor = color,
        Roughness = 0.8f,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
    };

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
