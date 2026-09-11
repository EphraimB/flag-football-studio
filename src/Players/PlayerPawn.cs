using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerPawn : Node3D
{
    private PlayerAppearance _appearance = null!;

    public Player? Player { get; private set; }

    public void Configure(Player player, PlayerAppearance appearance)
    {
        Player = player;
        _appearance = appearance;
    }

    public override void _Ready() => RebuildVisuals();

    public void ApplyAppearance(PlayerAppearance appearance)
    {
        _appearance = appearance;
        if (IsNodeReady())
            RebuildVisuals();
    }

    private void RebuildVisuals()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var heightScale = _appearance.HeightMeters / 1.8f;
        var buildScale = _appearance.BodyBuild switch
        {
            BodyBuild.Slim => 0.82f,
            BodyBuild.Stocky => 1.2f,
            _ => 1f
        };
        var visual = new Node3D
        {
            Name = "Appearance",
            Scale = new Vector3(buildScale, heightScale, buildScale)
        };
        AddChild(visual);

        var primary = CreateMaterial(ToGodot(_appearance.PrimaryUniformColor));
        var secondary = CreateMaterial(ToGodot(_appearance.SecondaryUniformColor));
        var skin = CreateMaterial(ToGodot(_appearance.SkinTone));
        var hair = CreateMaterial(ToGodot(_appearance.HairColor));
        var flag = CreateMaterial(ToGodot(_appearance.FlagColor));

        AddMesh(visual, "Body", new CapsuleMesh { Radius = 0.48f, Height = 1.55f }, new Vector3(0, 1.25f, 0), primary);
        AddMesh(visual, "Belt", new BoxMesh { Size = new Vector3(0.9f, 0.14f, 0.62f) }, new Vector3(0, 0.92f, 0), secondary);
        AddMesh(visual, "Head", new SphereMesh { Radius = 0.34f, Height = 0.68f }, new Vector3(0, 2.35f, 0), skin);
        AddMesh(visual, "LeftLeg", new CapsuleMesh { Radius = 0.16f, Height = 0.8f }, new Vector3(-0.22f, 0.45f, 0), secondary);
        AddMesh(visual, "RightLeg", new CapsuleMesh { Radius = 0.16f, Height = 0.8f }, new Vector3(0.22f, 0.45f, 0), secondary);
        AddMesh(visual, "LeftArm", new CapsuleMesh { Radius = 0.13f, Height = 0.75f }, new Vector3(-0.58f, 1.35f, 0), skin);
        AddMesh(visual, "RightArm", new CapsuleMesh { Radius = 0.13f, Height = 0.75f }, new Vector3(0.58f, 1.35f, 0), skin);
        AddMesh(visual, "LeftFlag", new BoxMesh { Size = new Vector3(0.12f, 0.55f, 0.08f) }, new Vector3(-0.55f, 1.15f, 0), flag);
        AddMesh(visual, "RightFlag", new BoxMesh { Size = new Vector3(0.12f, 0.55f, 0.08f) }, new Vector3(0.55f, 1.15f, 0), flag);
        AddHair(visual, hair);
        AddAccessories(visual, secondary);

        var number = new Label3D
        {
            Text = _appearance.JerseyNumber.ToString(),
            Position = new Vector3(0, 1.45f, -0.49f),
            FontSize = 48,
            OutlineSize = 8,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        visual.AddChild(number);
    }

    private void AddHair(Node parent, Material material)
    {
        switch (_appearance.HairStyle)
        {
            case HairStyle.None:
                return;
            case HairStyle.Short:
                AddMesh(parent, "ShortHair", new SphereMesh { Radius = 0.35f, Height = 0.7f }, new Vector3(0, 2.55f, 0.02f), material, new Vector3(1.03f, 0.48f, 1.03f));
                break;
            case HairStyle.Curly:
                for (var index = 0; index < 6; index++)
                {
                    var angle = Mathf.Tau * index / 6f;
                    AddMesh(parent, $"Curl{index}", new SphereMesh { Radius = 0.14f, Height = 0.28f }, new Vector3(Mathf.Cos(angle) * 0.22f, 2.62f, Mathf.Sin(angle) * 0.2f), material);
                }
                break;
            case HairStyle.Mohawk:
                AddMesh(parent, "Mohawk", new BoxMesh { Size = new Vector3(0.15f, 0.38f, 0.72f) }, new Vector3(0, 2.68f, 0), material);
                break;
            case HairStyle.Bun:
                AddMesh(parent, "HairCap", new SphereMesh { Radius = 0.35f, Height = 0.7f }, new Vector3(0, 2.53f, 0.05f), material, new Vector3(1.02f, 0.5f, 1.02f));
                AddMesh(parent, "Bun", new SphereMesh { Radius = 0.2f, Height = 0.4f }, new Vector3(0, 2.53f, 0.36f), material);
                break;
        }
    }

    private void AddAccessories(Node parent, Material secondary)
    {
        if (_appearance.Accessories.HasFlag(PlayerAccessories.Headband))
            AddMesh(parent, "Headband", new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.1f }, new Vector3(0, 2.39f, 0), secondary);
        if (_appearance.Accessories.HasFlag(PlayerAccessories.Wristbands))
        {
            AddMesh(parent, "LeftWristband", new BoxMesh { Size = new Vector3(0.26f, 0.14f, 0.26f) }, new Vector3(-0.58f, 1.08f, 0), secondary);
            AddMesh(parent, "RightWristband", new BoxMesh { Size = new Vector3(0.26f, 0.14f, 0.26f) }, new Vector3(0.58f, 1.08f, 0), secondary);
        }
        if (_appearance.Accessories.HasFlag(PlayerAccessories.Visor))
            AddMesh(parent, "Visor", new BoxMesh { Size = new Vector3(0.5f, 0.18f, 0.06f) }, new Vector3(0, 2.39f, -0.32f), CreateMaterial(new Color(0.2f, 0.7f, 0.95f, 0.72f), true));
        if (_appearance.Accessories.HasFlag(PlayerAccessories.ArmSleeves))
        {
            AddMesh(parent, "LeftSleeve", new CapsuleMesh { Radius = 0.145f, Height = 0.48f }, new Vector3(-0.58f, 1.38f, 0), secondary);
            AddMesh(parent, "RightSleeve", new CapsuleMesh { Radius = 0.145f, Height = 0.48f }, new Vector3(0.58f, 1.38f, 0), secondary);
        }
    }

    private static void AddMesh(Node parent, string nodeName, Mesh mesh, Vector3 position, Material material, Vector3? scale = null)
    {
        var meshInstance = new MeshInstance3D
        {
            Name = nodeName,
            Mesh = mesh,
            Position = position,
            Scale = scale ?? Vector3.One,
            MaterialOverride = material
        };
        parent.AddChild(meshInstance);
    }

    private static StandardMaterial3D CreateMaterial(Color color, bool transparent = false) => new()
    {
        AlbedoColor = color,
        Roughness = 0.8f,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled
    };

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
