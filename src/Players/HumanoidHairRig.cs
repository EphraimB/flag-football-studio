using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidHairRig : Node3D
{
    private static readonly SphereMesh UnitSphere = new()
    {
        Radius = 0.5f,
        Height = 1,
        RadialSegments = 12,
        Rings = 6
    };
    private static readonly BoxMesh UnitBox = new() { Size = Vector3.One };
    private static readonly CapsuleMesh UnitCapsule = new()
    {
        Radius = 0.5f,
        Height = 1,
        RadialSegments = 10,
        Rings = 4
    };

    private readonly List<MeshInstance3D> _pieces = [];

    public int PieceCount => _pieces.Count;
    public HairStyle Style { get; private set; }
    public bool AccessoryClearanceApplied { get; private set; }

    public void Configure(HairAppearance hair, FaceAppearance face, PlayerAccessories accessories)
    {
        ArgumentNullException.ThrowIfNull(hair);
        ArgumentNullException.ThrowIfNull(face);
        ClearPieces();
        Style = hair.Style;
        AccessoryClearanceApplied = accessories.HasFlag(PlayerAccessories.Headband) || accessories.HasFlag(PlayerAccessories.Visor);
        if (hair.Style == HairStyle.None)
            return;

        var material = StudioMaterialLibrary.Hair(ToGodot(hair.Color));
        var headWidth = 0.33f * face.HeadWidth;
        var headHeight = 0.34f * face.HeadHeight;
        var headDepth = 0.305f * (1 + (face.CheekFullness - 1) * 0.12f);
        var crownY = 0.13f + headHeight;
        var clearanceY = accessories.HasFlag(PlayerAccessories.Headband) ? 0.035f : 0;
        var visorBackset = accessories.HasFlag(PlayerAccessories.Visor) ? 0.035f : 0;
        var hairlineY = 0.28f + hair.HairlineHeight + clearanceY;

        switch (hair.Style)
        {
            case HairStyle.BuzzCut:
                AddSpherePiece("BuzzCap", new Vector3(0, crownY - 0.075f + clearanceY, 0.025f), new Vector3(headWidth * 1.025f, headHeight * 0.42f, headDepth * 1.025f), material);
                break;
            case HairStyle.Short:
                AddPartedCap(hair, headWidth, headHeight, headDepth, crownY, hairlineY, visorBackset, material, 0.52f);
                AddBoxPiece("ShortBack", new Vector3(0, hairlineY + 0.015f, headDepth - 0.015f), new Vector3(headWidth * 1.78f, 0.12f * hair.Length, 0.055f * hair.Volume), material);
                break;
            case HairStyle.Medium:
                AddPartedCap(hair, headWidth, headHeight, headDepth, crownY, hairlineY, visorBackset, material, 0.62f);
                AddSideFalls(hair, headWidth, headDepth, hairlineY, 0.24f + 0.12f * hair.Length, material);
                AddBackLayers(hair, headWidth, headDepth, hairlineY, 0.28f + 0.14f * hair.Length, 3, material);
                break;
            case HairStyle.Long:
                AddPartedCap(hair, headWidth, headHeight, headDepth, crownY, hairlineY, visorBackset, material, 0.68f);
                AddSideFalls(hair, headWidth, headDepth, hairlineY, 0.52f + 0.28f * hair.Length, material);
                AddBackLayers(hair, headWidth, headDepth, hairlineY, 0.65f + 0.32f * hair.Length, 5, material);
                break;
            case HairStyle.Curly:
                AddCurlyStyle(hair, headWidth, headHeight, headDepth, crownY, material);
                break;
            case HairStyle.Ponytail:
                AddPartedCap(hair, headWidth, headHeight, headDepth, crownY, hairlineY, visorBackset, material, 0.55f);
                AddPonytail(hair, headDepth, crownY, material);
                break;
            case HairStyle.Bun:
                AddPartedCap(hair, headWidth, headHeight, headDepth, crownY, hairlineY, visorBackset, material, 0.55f);
                AddSpherePiece(
                    "Bun",
                    new Vector3(0, crownY - 0.02f, headDepth + 0.18f * hair.BunSize),
                    new Vector3(0.19f, 0.19f, 0.18f) * hair.BunSize * hair.Volume,
                    material);
                break;
            case HairStyle.Mohawk:
                AddBoxPiece("LegacyMohawk", new Vector3(0, crownY + 0.12f, 0.02f), new Vector3(0.13f * hair.Volume, 0.3f * hair.Length, headDepth * 1.75f), material);
                break;
        }
    }

    public bool HasFiniteGeometry()
    {
        foreach (var piece in _pieces)
        {
            var bounds = piece.Mesh.GetAabb();
            if (!Finite(bounds.Position) || !Finite(bounds.Size) || !Finite(piece.Position) || !Finite(piece.Scale))
                return false;
            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0 || bounds.Size.Z <= 0)
                return false;
        }
        return true;
    }

    private void AddPartedCap(
        HairAppearance hair,
        float headWidth,
        float headHeight,
        float headDepth,
        float crownY,
        float hairlineY,
        float visorBackset,
        Material material,
        float verticalCoverage)
    {
        var halfHeight = Mathf.Max(0.11f, (crownY - hairlineY) * verticalCoverage);
        var centerY = crownY - halfHeight * 0.62f;
        var partShift = hair.PartPosition * headWidth * 0.26f;
        var partGap = 0.012f + Mathf.Abs(hair.PartPosition) * 0.012f;
        var halfWidth = headWidth * hair.Volume * 0.53f;
        var halfDepth = headDepth * (0.92f + hair.Volume * 0.09f);
        AddSpherePiece("LeftCap", new Vector3(-halfWidth + partShift - partGap, centerY, 0.025f + visorBackset), new Vector3(halfWidth, halfHeight, halfDepth), material);
        AddSpherePiece("RightCap", new Vector3(halfWidth + partShift + partGap, centerY, 0.025f + visorBackset), new Vector3(halfWidth, halfHeight, halfDepth), material);
    }

    private void AddSideFalls(HairAppearance hair, float headWidth, float headDepth, float hairlineY, float fallLength, Material material)
    {
        var wave = hair.CurlAmount * 0.055f;
        for (var side = -1; side <= 1; side += 2)
        {
            for (var layer = 0; layer < 2; layer++)
            {
                var x = side * (headWidth * hair.Volume + layer * 0.018f);
                var z = -0.015f + layer * (headDepth * 0.85f) + side * wave * 0.25f;
                AddCapsulePiece(
                    $"Side{side}_{layer}",
                    new Vector3(x, hairlineY - fallLength * 0.42f, z),
                    new Vector3(0.075f * hair.Volume, fallLength * 0.55f, 0.065f * hair.Volume),
                    material,
                    new Vector3(0, 0, side * wave));
            }
        }
    }

    private void AddBackLayers(HairAppearance hair, float headWidth, float headDepth, float hairlineY, float length, int layers, Material material)
    {
        for (var layer = 0; layer < layers; layer++)
        {
            var normalized = layers == 1 ? 0 : layer / (float)(layers - 1);
            var x = Mathf.Lerp(-headWidth * 0.72f, headWidth * 0.72f, normalized);
            var wave = Mathf.Sin(layer * 2.1f + hair.PartPosition) * hair.CurlAmount * 0.055f;
            AddCapsulePiece(
                $"BackLayer{layer}",
                new Vector3(x + wave, hairlineY - length * 0.43f, headDepth + 0.01f),
                new Vector3(headWidth * 0.23f * hair.Volume, length * 0.56f, 0.075f * hair.Volume),
                material,
                new Vector3(wave * 0.6f, 0, -wave));
        }
    }

    private void AddCurlyStyle(HairAppearance hair, float headWidth, float headHeight, float headDepth, float crownY, Material material)
    {
        const int curlCount = 14;
        var curlRadius = Mathf.Lerp(0.085f, 0.14f, hair.CurlAmount) * hair.Volume;
        for (var index = 0; index < curlCount; index++)
        {
            var ring = index < 8 ? 0 : 1;
            var ringIndex = ring == 0 ? index : index - 8;
            var ringCount = ring == 0 ? 8 : 6;
            var angle = Mathf.Tau * ringIndex / ringCount + hair.PartPosition * 0.16f;
            var radiusX = headWidth * (ring == 0 ? 0.82f : 0.52f);
            var radiusZ = headDepth * (ring == 0 ? 0.8f : 0.48f);
            var position = new Vector3(
                Mathf.Cos(angle) * radiusX,
                crownY - ring * headHeight * 0.2f + Mathf.Sin(angle * 2) * hair.CurlAmount * 0.025f,
                Mathf.Sin(angle) * radiusZ + 0.025f);
            AddSpherePiece($"Curl{index}", position, new Vector3(curlRadius, curlRadius, curlRadius), material);
        }
    }

    private void AddPonytail(HairAppearance hair, float headDepth, float crownY, Material material)
    {
        const int segmentCount = 4;
        var totalLength = 0.52f * hair.PonytailLength;
        for (var index = 0; index < segmentCount; index++)
        {
            var progress = index / (float)(segmentCount - 1);
            var wave = Mathf.Sin(progress * Mathf.Pi * 1.5f) * hair.CurlAmount * 0.08f;
            AddCapsulePiece(
                $"Ponytail{index}",
                new Vector3(wave, crownY - 0.08f - progress * totalLength, headDepth + 0.13f + progress * 0.08f),
                new Vector3(0.105f, totalLength / segmentCount * 0.78f, 0.09f) * hair.PonytailVolume * hair.Volume,
                material,
                new Vector3(-0.25f + progress * 0.18f, 0, wave));
        }
    }

    private void AddSpherePiece(string name, Vector3 position, Vector3 halfExtents, Material material) =>
        AddPiece(name, UnitSphere, position, halfExtents * 2, material, Vector3.Zero);

    private void AddBoxPiece(string name, Vector3 position, Vector3 size, Material material) =>
        AddPiece(name, UnitBox, position, size, material, Vector3.Zero);

    private void AddCapsulePiece(string name, Vector3 position, Vector3 halfExtents, Material material, Vector3 rotation) =>
        AddPiece(name, UnitCapsule, position, halfExtents * 2, material, rotation);

    private void AddPiece(string name, Mesh mesh, Vector3 position, Vector3 scale, Material material, Vector3 rotation)
    {
        var piece = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            Scale = scale,
            Rotation = rotation,
            MaterialOverride = material
        };
        AddChild(piece);
        _pieces.Add(piece);
    }

    private void ClearPieces()
    {
        foreach (var piece in _pieces)
        {
            RemoveChild(piece);
            piece.QueueFree();
        }
        _pieces.Clear();
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
