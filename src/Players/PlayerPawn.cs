using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerPawn : Node3D
{
    private Color _teamColor = Colors.White;

    public Player? Player { get; private set; }

    public void Configure(Player player, Color teamColor)
    {
        Player = player;
        _teamColor = teamColor;
    }

    public override void _Ready()
    {
        var uniform = CreateMaterial(_teamColor);
        var skin = CreateMaterial(new Color("d6a77a"));
        var dark = CreateMaterial(new Color("172033"));
        var flag = CreateMaterial(new Color("ff4f64"));

        AddMesh("Body", new CapsuleMesh { Radius = 0.48f, Height = 1.55f }, new Vector3(0, 1.25f, 0), uniform);
        AddMesh("Head", new SphereMesh { Radius = 0.34f, Height = 0.68f }, new Vector3(0, 2.35f, 0), skin);
        AddMesh("LeftLeg", new CapsuleMesh { Radius = 0.16f, Height = 0.8f }, new Vector3(-0.22f, 0.45f, 0), dark);
        AddMesh("RightLeg", new CapsuleMesh { Radius = 0.16f, Height = 0.8f }, new Vector3(0.22f, 0.45f, 0), dark);
        AddMesh("LeftFlag", new BoxMesh { Size = new Vector3(0.12f, 0.55f, 0.08f) }, new Vector3(-0.55f, 1.15f, 0), flag);
        AddMesh("RightFlag", new BoxMesh { Size = new Vector3(0.12f, 0.55f, 0.08f) }, new Vector3(0.55f, 1.15f, 0), flag);

        var number = new Label3D
        {
            Text = Player?.JerseyNumber.ToString() ?? "?",
            Position = new Vector3(0, 1.45f, -0.49f),
            FontSize = 48,
            OutlineSize = 8,
            Modulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(number);
    }

    private void AddMesh(string nodeName, Mesh mesh, Vector3 position, Material material)
    {
        var meshInstance = new MeshInstance3D
        {
            Name = nodeName,
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        };
        AddChild(meshInstance);
    }

    private static StandardMaterial3D CreateMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 0.8f
    };
}
