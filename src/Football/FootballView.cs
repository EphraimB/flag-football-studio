using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FootballView : Node3D
{
    public override void _Ready()
    {
        var football = new MeshInstance3D
        {
            Name = "LeatherShell",
            Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f },
            Scale = new Vector3(0.7f, 0.7f, 1.28f),
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = StudioMaterialLibrary.FootballLeather(new Color("713719"))
        };
        AddChild(football);

        var seam = new MeshInstance3D
        {
            Name = "LeatherSeam",
            Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.018f, 0.018f) },
            Position = new Vector3(0, 0.17f, -0.015f),
            MaterialOverride = StudioMaterialLibrary.FootballLeather(new Color("3d1d10"))
        };
        AddChild(seam);

        var laceMaterial = StudioMaterialLibrary.FootballLaces(new Color("e9dfc8"));
        for (var index = 0; index < 5; index++)
        {
            var lace = new MeshInstance3D
            {
                Name = $"Lace{index + 1}",
                Mesh = new BoxMesh { Size = new Vector3(0.018f, 0.018f, 0.105f) },
                Position = new Vector3(-0.11f + index * 0.055f, 0.18f, -0.015f),
                MaterialOverride = laceMaterial
            };
            AddChild(lace);
        }
    }
}
