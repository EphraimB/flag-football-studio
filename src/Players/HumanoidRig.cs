using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidRig : Node3D
{
    private readonly Dictionary<string, BoneAttachment3D> _attachments = [];
    private readonly List<Node> _generatedDetails = [];
    private Skeleton3D _skeleton = null!;
    private MeshInstance3D _bodyMesh = null!;
    private MeshInstance3D _faceMesh = null!;
    private HumanoidAnimator _animator = null!;
    private Node3D _eyeAnchor = null!;
    private Node3D _catchAnchor = null!;

    public Node3D EyeAnchor => _eyeAnchor;
    public Node3D CatchAnchor => _catchAnchor;
    public HumanoidAnimationState AnimationState => _animator.State;
    public Mesh BodyMeshResource => _bodyMesh.Mesh;
    public ArrayMesh FaceMeshResource => (ArrayMesh)_faceMesh.Mesh;
    public string TopologySignature => HumanoidSkinnedMesh.TopologySignature;
    public int SkeletonBoneCount => _skeleton.GetBoneCount();

    public override void _Ready()
    {
        BuildSkeleton();
        BuildSkinnedBody();
        _animator = new HumanoidAnimator { Name = "Animator" };
        AddChild(_animator);
        _animator.Configure(_skeleton);
    }

    public void Apply(Player player, PlayerAppearance appearance, UniformDefinition uniform)
    {
        if (!IsNodeReady())
            throw new InvalidOperationException("The humanoid rig must be in the scene tree before it is configured.");

        ClearGeneratedDetails();
        ApplyProportions(appearance);

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

        _bodyMesh.SetSurfaceOverrideMaterial((int)HumanoidMeshSurface.Skin, skin);
        _bodyMesh.SetSurfaceOverrideMaterial((int)HumanoidMeshSurface.Jersey, jersey);
        _bodyMesh.SetSurfaceOverrideMaterial((int)HumanoidMeshSurface.Primary, primary);
        _bodyMesh.SetSurfaceOverrideMaterial((int)HumanoidMeshSurface.Shorts, shorts);
        _bodyMesh.SetSurfaceOverrideMaterial((int)HumanoidMeshSurface.Shoes, secondary);

        AddDetail(HumanoidSkeletonDefinition.Chest, "AccentStripe", new BoxMesh { Size = new Vector3(0.1f, 0.53f, 0.035f) }, new Vector3(0, -0.12f, -0.3f), accent);
        AddDetail(HumanoidSkeletonDefinition.Chest, "Collar", new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.24f, Height = 0.08f }, new Vector3(0, 0.27f, 0), collarTrim);
        AddDetail(HumanoidSkeletonDefinition.LeftUpperArm, "LeftSleeveTrim", new CylinderMesh { TopRadius = 0.155f, BottomRadius = 0.155f, Height = 0.11f }, new Vector3(0, -0.38f, 0), sleeveTrim);
        AddDetail(HumanoidSkeletonDefinition.RightUpperArm, "RightSleeveTrim", new CylinderMesh { TopRadius = 0.155f, BottomRadius = 0.155f, Height = 0.11f }, new Vector3(0, -0.38f, 0), sleeveTrim);
        AddDetail(HumanoidSkeletonDefinition.Hips, "Belt", new BoxMesh { Size = new Vector3(0.7f, 0.1f, 0.45f) }, new Vector3(0, 0.12f, 0), secondary);
        AddDetail(HumanoidSkeletonDefinition.Hips, "LeftFlag", new BoxMesh { Size = new Vector3(0.1f, 0.5f, 0.055f) }, new Vector3(-0.42f, -0.08f, 0), flag);
        AddDetail(HumanoidSkeletonDefinition.Hips, "RightFlag", new BoxMesh { Size = new Vector3(0.1f, 0.5f, 0.055f) }, new Vector3(0.42f, -0.08f, 0), flag);

        AddFace(appearance.Face, skin, hair);
        AddHair(appearance.HairStyle, hair, appearance.Face);
        AddAccessories(appearance.Accessories, secondary, appearance.Face);
        AddUniformLabels(player, uniform);
    }

    public void SetAnimationState(HumanoidAnimationState state, bool restart = false) => _animator.SetState(state, restart);

    public Transform3D BoneGlobalPose(string boneName)
    {
        var index = _skeleton.FindBone(boneName);
        if (index < 0)
            throw new ArgumentException($"Unknown humanoid bone {boneName}.", nameof(boneName));
        return _skeleton.GetBoneGlobalPose(index);
    }

    public Quaternion BonePoseRotation(string boneName)
    {
        var index = _skeleton.FindBone(boneName);
        if (index < 0)
            throw new ArgumentException($"Unknown humanoid bone {boneName}.", nameof(boneName));
        return _skeleton.GetBonePoseRotation(index);
    }

    public Vector3 BonePoseScale(string boneName)
    {
        var index = _skeleton.FindBone(boneName);
        if (index < 0)
            throw new ArgumentException($"Unknown humanoid bone {boneName}.", nameof(boneName));
        return _skeleton.GetBonePoseScale(index);
    }

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
            _skeleton.ResetBonePose(index);

            var attachment = new BoneAttachment3D { Name = $"{bone.Name}Attachment" };
            _skeleton.AddChild(attachment);
            attachment.BoneIdx = index;
            attachment.OverridePose = false;
            _attachments[bone.Name] = attachment;
        }

        _eyeAnchor = new Node3D { Name = "EyeAnchor", Position = new Vector3(0, 0.19f, -0.29f) };
        _attachments[HumanoidSkeletonDefinition.Head].AddChild(_eyeAnchor);

        _catchAnchor = new Node3D { Name = "CatchAnchor", Position = new Vector3(0, -0.05f, -0.12f) };
        _attachments[HumanoidSkeletonDefinition.RightHand].AddChild(_catchAnchor);
    }

    private void BuildSkinnedBody()
    {
        _bodyMesh = new MeshInstance3D
        {
            Name = "SkinnedBody",
            Mesh = HumanoidSkinnedMesh.SharedMesh,
            Skin = HumanoidSkinnedMesh.SharedSkin
        };
        AddChild(_bodyMesh);
        _bodyMesh.Skeleton = _bodyMesh.GetPathTo(_skeleton);
    }

    private void ApplyProportions(PlayerAppearance appearance)
    {
        Scale = new Vector3(1, appearance.HeightMeters / 1.8f, 1);
        var build = appearance.BodyBuild switch
        {
            BodyBuild.Slim => 0.86f,
            BodyBuild.Average => 0.96f,
            BodyBuild.Heavy => 1.18f,
            _ => 1.04f
        };

        foreach (var bone in HumanoidSkeletonDefinition.Bones)
        {
            var restPosition = bone.RestPosition;
            if (bone.Name == HumanoidSkeletonDefinition.Hips)
                restPosition.Y *= appearance.LegLength;
            if (bone.Name is HumanoidSkeletonDefinition.LeftLowerLeg or HumanoidSkeletonDefinition.RightLowerLeg or HumanoidSkeletonDefinition.LeftFoot or HumanoidSkeletonDefinition.RightFoot)
                restPosition.Y *= appearance.LegLength;
            if (bone.Name is HumanoidSkeletonDefinition.LeftLowerArm or HumanoidSkeletonDefinition.RightLowerArm or HumanoidSkeletonDefinition.LeftHand or HumanoidSkeletonDefinition.RightHand)
                restPosition.Y *= appearance.ArmLength;
            if (bone.Name is HumanoidSkeletonDefinition.LeftUpperArm or HumanoidSkeletonDefinition.RightUpperArm)
                restPosition.X *= appearance.ShoulderWidth / appearance.ChestWidth;

            var index = _skeleton.FindBone(bone.Name);
            _skeleton.SetBoneRest(index, new Transform3D(Basis.Identity, restPosition));
            _skeleton.ResetBonePose(index);
        }

        SetBoneScale(HumanoidSkeletonDefinition.Hips, new Vector3(build * appearance.HipWidth, 1, build));
        SetBoneScale(HumanoidSkeletonDefinition.Spine, new Vector3(appearance.WaistWidth / appearance.HipWidth, 1, 1));
        SetBoneScale(HumanoidSkeletonDefinition.Chest, new Vector3(appearance.ChestWidth / appearance.WaistWidth, 1, 1));
    }

    private void SetBoneScale(string boneName, Vector3 scale) =>
        _skeleton.SetBonePoseScale(_skeleton.FindBone(boneName), scale);

    private void AddFace(FaceAppearance face, Material skin, Material hair)
    {
        _faceMesh = new MeshInstance3D
        {
            Name = "ProceduralFace",
            Mesh = HumanoidFaceMesh.Create(face)
        };
        _faceMesh.SetSurfaceOverrideMaterial((int)HumanoidFaceSurface.Skin, skin);
        _faceMesh.SetSurfaceOverrideMaterial((int)HumanoidFaceSurface.Eyes, CreateMaterial(new Color("242b35")));
        _faceMesh.SetSurfaceOverrideMaterial((int)HumanoidFaceSurface.Brows, hair);
        _faceMesh.SetSurfaceOverrideMaterial((int)HumanoidFaceSurface.Lips, CreateMaterial(new Color("914f58")));
        _attachments[HumanoidSkeletonDefinition.Head].AddChild(_faceMesh);
        _generatedDetails.Add(_faceMesh);
    }

    private void AddHair(HairStyle style, Material material, FaceAppearance face)
    {
        var crownY = 0.13f + 0.34f * face.HeadHeight;
        switch (style)
        {
            case HairStyle.None:
                return;
            case HairStyle.Short:
                AddDetail(HumanoidSkeletonDefinition.Head, "ShortHair", new SphereMesh { Radius = 0.33f, Height = 0.66f }, new Vector3(0, crownY - 0.17f, 0.02f), material, new Vector3(1.03f * face.HeadWidth, 0.48f * face.HeadHeight, 1.03f));
                break;
            case HairStyle.Curly:
                for (var index = 0; index < 6; index++)
                {
                    var angle = Mathf.Tau * index / 6f;
                    AddDetail(HumanoidSkeletonDefinition.Head, $"Curl{index}", new SphereMesh { Radius = 0.13f, Height = 0.26f }, new Vector3(Mathf.Cos(angle) * 0.2f * face.HeadWidth, crownY - 0.1f, Mathf.Sin(angle) * 0.18f), material);
                }
                break;
            case HairStyle.Mohawk:
                AddDetail(HumanoidSkeletonDefinition.Head, "Mohawk", new BoxMesh { Size = new Vector3(0.14f, 0.35f, 0.65f) }, new Vector3(0, crownY + 0.02f, 0), material);
                break;
            case HairStyle.Bun:
                AddDetail(HumanoidSkeletonDefinition.Head, "HairCap", new SphereMesh { Radius = 0.33f, Height = 0.66f }, new Vector3(0, crownY - 0.18f, 0.04f), material, new Vector3(1.02f * face.HeadWidth, 0.5f * face.HeadHeight, 1.02f));
                AddDetail(HumanoidSkeletonDefinition.Head, "Bun", new SphereMesh { Radius = 0.19f, Height = 0.38f }, new Vector3(0, crownY - 0.17f, 0.34f), material);
                break;
        }
    }

    private void AddAccessories(PlayerAccessories accessories, Material secondary, FaceAppearance face)
    {
        if (accessories.HasFlag(PlayerAccessories.Headband))
            AddDetail(HumanoidSkeletonDefinition.Head, "Headband", new CylinderMesh { TopRadius = 0.34f * face.HeadWidth, BottomRadius = 0.34f * face.HeadWidth, Height = 0.09f }, new Vector3(0, 0.17f, 0), secondary);
        if (accessories.HasFlag(PlayerAccessories.Wristbands))
        {
            AddDetail(HumanoidSkeletonDefinition.LeftLowerArm, "LeftWristband", new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.1f }, new Vector3(0, -0.34f, 0), secondary);
            AddDetail(HumanoidSkeletonDefinition.RightLowerArm, "RightWristband", new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.1f }, new Vector3(0, -0.34f, 0), secondary);
        }
        if (accessories.HasFlag(PlayerAccessories.Visor))
            AddDetail(HumanoidSkeletonDefinition.Head, "Visor", new BoxMesh { Size = new Vector3(0.48f * face.HeadWidth, 0.16f, 0.055f) }, new Vector3(0, 0.16f, -0.31f), CreateMaterial(new Color(0.2f, 0.7f, 0.95f, 0.72f), true));
        if (accessories.HasFlag(PlayerAccessories.ArmSleeves))
        {
            AddDetail(HumanoidSkeletonDefinition.LeftLowerArm, "LeftArmSleeve", new CapsuleMesh { Radius = 0.125f, Height = 0.32f }, new Vector3(0, -0.16f, 0), secondary);
            AddDetail(HumanoidSkeletonDefinition.RightLowerArm, "RightArmSleeve", new CapsuleMesh { Radius = 0.125f, Height = 0.32f }, new Vector3(0, -0.16f, 0), secondary);
        }
    }

    private void AddUniformLabels(Player player, UniformDefinition uniform)
    {
        AddLabel("JerseyNumber", player.JerseyNumber.ToString(), new Vector3(0, 0, -0.31f), 44, uniform.NumberColor, uniform.NumberOutlineColor, 7);
        AddLabel("Wordmark", uniform.TeamWordmark, new Vector3(0, 0.2f, -0.31f), 19, uniform.NumberColor);
        if (uniform.ShowPlayerNameOnBack)
        {
            var label = AddLabel("PlayerName", player.Name.ToUpperInvariant(), new Vector3(0, 0.2f, 0.31f), 16, uniform.NumberColor);
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
        _generatedDetails.Add(label);
        return label;
    }

    private void AddDetail(string boneName, string nodeName, Mesh mesh, Vector3 position, Material material, Vector3? scale = null)
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
        _generatedDetails.Add(instance);
    }

    private void ClearGeneratedDetails()
    {
        foreach (var detail in _generatedDetails)
        {
            detail.GetParent()?.RemoveChild(detail);
            detail.QueueFree();
        }
        _generatedDetails.Clear();
    }

    private static StandardMaterial3D CreateMaterial(Color color, bool transparent = false) => new()
    {
        AlbedoColor = color,
        Roughness = 0.8f,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
    };

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
