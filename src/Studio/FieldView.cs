using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FieldView : Node3D
{
    public override void _Ready()
    {
        AddBox("Turf", new Vector3(20, 0.12f, 40), new Vector3(0, -0.06f, 0), new Color("226b3a"));
        AddBox("GoldEndZone", new Vector3(20, 0.02f, 3), new Vector3(0, 0.02f, -18.5f), new Color("c99a16"));
        AddBox("NavyEndZone", new Vector3(20, 0.02f, 3), new Vector3(0, 0.02f, 18.5f), new Color("172d5b"));

        AddLine("LeftSideline", new Vector3(0.12f, 0.025f, 40), new Vector3(-9.9f, 0.08f, 0));
        AddLine("RightSideline", new Vector3(0.12f, 0.025f, 40), new Vector3(9.9f, 0.08f, 0));
        AddLine("GoldGoalLine", new Vector3(20, 0.025f, 0.12f), new Vector3(0, 0.08f, -17));
        AddLine("NavyGoalLine", new Vector3(20, 0.025f, 0.12f), new Vector3(0, 0.08f, 17));

        for (var z = -15; z <= 15; z += 5)
            AddLine($"YardLine{z}", new Vector3(20, 0.025f, 0.07f), new Vector3(0, 0.08f, z));
    }

    private void AddLine(string nodeName, Vector3 size, Vector3 position) =>
        AddBox(nodeName, size, position, new Color("f4f0dc"));

    private void AddBox(string nodeName, Vector3 size, Vector3 position, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Name = nodeName,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = 1f
            }
        };
        AddChild(mesh);
    }
}
