using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FootballView : Node3D
{
    public override void _Ready()
    {
        var football = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f },
            Scale = new Vector3(0.72f, 0.72f, 1.25f),
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("7b3f20"),
                Roughness = 0.9f
            }
        };
        AddChild(football);
    }
}
