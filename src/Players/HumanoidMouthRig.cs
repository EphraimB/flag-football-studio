using System;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidMouthRig : Node3D
{
    private static readonly SphereMesh InnerMouthMesh = new()
    {
        Radius = 0.5f,
        Height = 1,
        RadialSegments = 12,
        Rings = 6
    };
    private static readonly BoxMesh TeethMesh = new() { Size = Vector3.One };
    private MeshInstance3D _innerMouth = null!;
    private MeshInstance3D _upperTeeth = null!;
    private MeshInstance3D _lowerTeeth = null!;

    public int PieceCount => 3;
    public float Opening { get; private set; }
    public float UpperLipRaise { get; private set; }
    public float LowerLipDrop { get; private set; }

    public override void _Ready()
    {
        _innerMouth = AddPiece("InnerMouth", InnerMouthMesh, CreateMaterial(new Color("351c28")));
        _upperTeeth = AddPiece("UpperTeeth", TeethMesh, CreateMaterial(new Color("eee9da")));
        _lowerTeeth = AddPiece("LowerTeeth", TeethMesh, CreateMaterial(new Color("e5dfcf")));
    }

    public void Configure(FaceAppearance face, FacialExpressionPose expression, SpeechMouthPose speech)
    {
        ArgumentNullException.ThrowIfNull(face);
        Opening = HumanoidFaceMesh.CombinedJawOpen(expression, speech);
        UpperLipRaise = speech.UpperLipRaise;
        LowerLipDrop = speech.LowerLipDrop;
        var center = HumanoidFaceMesh.MouthCenter(face, expression, speech);
        var width = 0.084f * face.MouthWidth * speech.Width;
        var height = 0.008f + Opening * 0.062f;
        var roundWidth = Mathf.Lerp(width, width * 0.72f, speech.Roundness);

        _innerMouth.Position = new Vector3(center.X, center.Y - Opening * 0.012f, center.Z - 0.024f);
        _innerMouth.Scale = new Vector3(roundWidth * 2, height * 2, 0.024f);

        var teethWidth = Mathf.Max(0.025f, roundWidth * 1.45f);
        var teethHeight = Mathf.Lerp(0.002f, 0.018f, speech.TeethExposure);
        _upperTeeth.Position = new Vector3(center.X, center.Y + height * 0.38f, center.Z - 0.04f);
        _lowerTeeth.Position = new Vector3(center.X, center.Y - height * 0.4f, center.Z - 0.04f);
        _upperTeeth.Scale = new Vector3(teethWidth, teethHeight, 0.012f);
        _lowerTeeth.Scale = new Vector3(teethWidth, teethHeight * 0.75f, 0.012f);
        var showTeeth = Opening > 0.035f && speech.TeethExposure > 0.03f;
        _upperTeeth.Visible = showTeeth;
        _lowerTeeth.Visible = showTeeth && Opening > 0.16f;
    }

    public bool HasFiniteGeometry()
    {
        foreach (var piece in new[] { _innerMouth, _upperTeeth, _lowerTeeth })
        {
            if (!Finite(piece.Position) || !Finite(piece.Scale))
                return false;
            var bounds = piece.Mesh.GetAabb();
            if (!Finite(bounds.Position) || !Finite(bounds.Size))
                return false;
        }
        return true;
    }

    private MeshInstance3D AddPiece(string name, Mesh mesh, Material material)
    {
        var piece = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material };
        AddChild(piece);
        return piece;
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static StandardMaterial3D CreateMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 0.78f
    };
}
