using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FieldView : Node3D
{
    private static readonly Color TurfDark = new("1e5f35");
    private static readonly Color TurfLight = new("286f3d");
    private static readonly Color TurfWear = new("4d7540");
    private static readonly Color Marking = new("f1eedc");

    public override void _Ready()
    {
        AddBox("TurfBase", new Vector3(20, 0.12f, 40), new Vector3(0, -0.06f, 0),
            StudioMaterialLibrary.Turf(TurfDark));
        for (var stripe = 0; stripe < 8; stripe++)
        {
            var z = -17.5f + stripe * 5;
            AddBox($"MowingStripe{stripe + 1}", new Vector3(19.96f, 0.012f, 5),
                new Vector3(0, 0.006f, z),
                StudioMaterialLibrary.Turf(stripe % 2 == 0 ? TurfLight : TurfDark.Lightened(0.035f)));
        }

        AddBox("GoldEndZone", new Vector3(20, 0.024f, 3), new Vector3(0, 0.02f, -18.5f),
            StudioMaterialLibrary.EndZone(new Color("b78612")));
        AddBox("NavyEndZone", new Vector3(20, 0.024f, 3), new Vector3(0, 0.02f, 18.5f),
            StudioMaterialLibrary.EndZone(new Color("13284e")));

        AddLine("LeftSideline", new Vector3(0.14f, 0.025f, 40), new Vector3(-9.9f, 0.08f, 0));
        AddLine("RightSideline", new Vector3(0.14f, 0.025f, 40), new Vector3(9.9f, 0.08f, 0));
        AddLine("GoldGoalLine", new Vector3(20, 0.026f, 0.14f), new Vector3(0, 0.08f, -17));
        AddLine("NavyGoalLine", new Vector3(20, 0.026f, 0.14f), new Vector3(0, 0.08f, 17));
        AddLine("Midfield", new Vector3(20, 0.027f, 0.11f), new Vector3(0, 0.082f, 0));

        for (var z = -15; z <= 15; z += 5)
        {
            if (z != 0)
                AddLine($"YardLine{z}", new Vector3(20, 0.025f, 0.085f), new Vector3(0, 0.08f, z));
        }
        for (var z = -16; z <= 16; z++)
        {
            if (z % 5 == 0)
                continue;
            AddLine($"LeftHash{z}", new Vector3(0.55f, 0.024f, 0.055f), new Vector3(-3.1f, 0.078f, z));
            AddLine($"RightHash{z}", new Vector3(0.55f, 0.024f, 0.055f), new Vector3(3.1f, 0.078f, z));
        }

        AddBox("WearPatchLeft", new Vector3(2.8f, 0.008f, 5.2f), new Vector3(-2.2f, 0.015f, -0.8f),
            StudioMaterialLibrary.Turf(TurfWear.Darkened(0.08f)));
        AddBox("WearPatchRight", new Vector3(2.4f, 0.008f, 4.5f), new Vector3(2.5f, 0.016f, 1.1f),
            StudioMaterialLibrary.Turf(TurfWear));

        AddEndZoneWordmark("GoldWordmark", "GOLD", new Vector3(0, 0.055f, -18.5f), new Color("fff4c7"), 0);
        AddEndZoneWordmark("NavyWordmark", "NAVY", new Vector3(0, 0.055f, 18.5f), new Color("e9efff"), 180);
    }

    private void AddLine(string nodeName, Vector3 size, Vector3 position) =>
        AddBox(nodeName, size, position, StudioMaterialLibrary.FieldMarking(Marking));

    private void AddBox(string nodeName, Vector3 size, Vector3 position, Material material)
    {
        var mesh = new MeshInstance3D
        {
            Name = nodeName,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material
        };
        AddChild(mesh);
    }

    private void AddEndZoneWordmark(
        string nodeName, string text, Vector3 position, Color color, float yawDegrees)
    {
        var label = new Label3D
        {
            Name = nodeName,
            Text = text,
            Position = position,
            RotationDegrees = new Vector3(-90, yawDegrees, 0),
            FontSize = 96,
            PixelSize = 0.012f,
            Modulate = color,
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.28f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled
        };
        AddChild(label);
    }
}
