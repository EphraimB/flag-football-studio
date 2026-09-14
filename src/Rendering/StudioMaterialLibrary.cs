using System;
using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum StudioMaterialKind
{
    Skin,
    Hair,
    JerseyFabric,
    ShortsFabric,
    Turf,
    EndZone,
    FootballLeather,
    FootballLaces,
    FlagFabric,
    Shoes,
    FieldMarking,
    SpectatorSkin,
    SpectatorClothing,
    Generic
}

public static class StudioMaterialLibrary
{
    private readonly record struct MaterialKey(
        StudioMaterialKind Kind, byte R, byte G, byte B, byte A, bool Transparent);

    private static readonly Dictionary<MaterialKey, StandardMaterial3D> Materials = [];
    private static readonly Texture2D FabricNormal = BuildNormalTexture(32, 0.12f, true);
    private static readonly Texture2D OrganicNormal = BuildNormalTexture(32, 0.08f, false);
    private static readonly Texture2D TurfNormal = BuildNormalTexture(48, 0.22f, false);
    private static readonly Texture2D RoughnessVariation = BuildRoughnessTexture(32);

    public static PresentationQualityPreset Quality { get; private set; } = PresentationQualityPreset.Preview;
    public static int CachedMaterialCount => Materials.Count;

    public static StandardMaterial3D Skin(Color color) => Get(StudioMaterialKind.Skin, color);
    public static StandardMaterial3D Hair(Color color) => Get(StudioMaterialKind.Hair, color);
    public static StandardMaterial3D Jersey(Color color) => Get(StudioMaterialKind.JerseyFabric, color);
    public static StandardMaterial3D Shorts(Color color) => Get(StudioMaterialKind.ShortsFabric, color);
    public static StandardMaterial3D Turf(Color color) => Get(StudioMaterialKind.Turf, color);
    public static StandardMaterial3D EndZone(Color color) => Get(StudioMaterialKind.EndZone, color);
    public static StandardMaterial3D FootballLeather(Color color) => Get(StudioMaterialKind.FootballLeather, color);
    public static StandardMaterial3D FootballLaces(Color color) => Get(StudioMaterialKind.FootballLaces, color);
    public static StandardMaterial3D Flag(Color color) => Get(StudioMaterialKind.FlagFabric, color);
    public static StandardMaterial3D Shoes(Color color) => Get(StudioMaterialKind.Shoes, color);
    public static StandardMaterial3D FieldMarking(Color color) => Get(StudioMaterialKind.FieldMarking, color);
    public static StandardMaterial3D SpectatorSkin() => Get(StudioMaterialKind.SpectatorSkin, Colors.White);
    public static StandardMaterial3D SpectatorClothing() => Get(StudioMaterialKind.SpectatorClothing, Colors.White);
    public static StandardMaterial3D Generic(Color color, bool transparent = false) =>
        Get(StudioMaterialKind.Generic, color, transparent);

    public static StandardMaterial3D Get(StudioMaterialKind kind, Color color, bool transparent = false)
    {
        var key = new MaterialKey(kind, Channel(color.R), Channel(color.G), Channel(color.B),
            Channel(color.A), transparent);
        if (Materials.TryGetValue(key, out var existing))
            return existing;

        var material = new StandardMaterial3D
        {
            AlbedoColor = Color.Color8(key.R, key.G, key.B, key.A),
            Transparency = transparent
                ? BaseMaterial3D.TransparencyEnum.Alpha
                : BaseMaterial3D.TransparencyEnum.Disabled
        };
        Configure(material, kind, Quality);
        Materials.Add(key, material);
        return material;
    }

    public static void SetQuality(PresentationQualityPreset quality)
    {
        Quality = quality;
        foreach (var entry in Materials)
            Configure(entry.Value, entry.Key.Kind, quality);
    }

    private static void Configure(
        StandardMaterial3D material,
        StudioMaterialKind kind,
        PresentationQualityPreset quality)
    {
        material.Metallic = 0;
        material.VertexColorUseAsAlbedo = kind is StudioMaterialKind.SpectatorSkin or
            StudioMaterialKind.SpectatorClothing;
        material.MetallicSpecular = kind switch
        {
            StudioMaterialKind.Hair => 0.34f,
            StudioMaterialKind.Shoes => 0.42f,
            StudioMaterialKind.FootballLeather => 0.3f,
            _ => 0.24f
        };
        material.Roughness = kind switch
        {
            StudioMaterialKind.Skin => 0.62f,
            StudioMaterialKind.Hair => 0.76f,
            StudioMaterialKind.JerseyFabric => 0.86f,
            StudioMaterialKind.ShortsFabric => 0.9f,
            StudioMaterialKind.Turf => 0.94f,
            StudioMaterialKind.EndZone => 0.88f,
            StudioMaterialKind.FootballLeather => 0.76f,
            StudioMaterialKind.FootballLaces => 0.82f,
            StudioMaterialKind.FlagFabric => 0.84f,
            StudioMaterialKind.Shoes => 0.48f,
            StudioMaterialKind.FieldMarking => 0.9f,
            StudioMaterialKind.SpectatorSkin => 0.68f,
            StudioMaterialKind.SpectatorClothing => 0.88f,
            _ => 0.78f
        };

        var detailed = quality != PresentationQualityPreset.Preview;
        var normal = kind switch
        {
            StudioMaterialKind.JerseyFabric or StudioMaterialKind.ShortsFabric or
                StudioMaterialKind.FlagFabric => FabricNormal,
            StudioMaterialKind.Turf or StudioMaterialKind.EndZone => TurfNormal,
            StudioMaterialKind.Skin or StudioMaterialKind.Hair or
                StudioMaterialKind.FootballLeather => OrganicNormal,
            _ => null
        };
        material.NormalEnabled = detailed && normal is not null;
        material.NormalTexture = detailed ? normal : null;
        material.NormalScale = quality == PresentationQualityPreset.Final ? 0.72f : 0.42f;
        material.RoughnessTexture = detailed && kind is not StudioMaterialKind.FieldMarking and
            not StudioMaterialKind.FootballLaces and not StudioMaterialKind.Generic
            ? RoughnessVariation
            : null;
        material.Uv1Scale = kind switch
        {
            StudioMaterialKind.Turf or StudioMaterialKind.EndZone => new Vector3(18, 18, 18),
            StudioMaterialKind.JerseyFabric or StudioMaterialKind.ShortsFabric or
                StudioMaterialKind.FlagFabric => new Vector3(9, 9, 9),
            _ => new Vector3(4, 4, 4)
        };
        material.TextureFilter = quality == PresentationQualityPreset.Final
            ? BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            : BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;

        material.SubsurfScatterEnabled =
            (kind is StudioMaterialKind.Skin or StudioMaterialKind.SpectatorSkin) &&
            quality != PresentationQualityPreset.Preview;
        material.SubsurfScatterStrength = quality == PresentationQualityPreset.Final ? 0.16f : 0.1f;

        material.RimEnabled = kind is StudioMaterialKind.Skin or StudioMaterialKind.Hair or
            StudioMaterialKind.SpectatorSkin;
        material.Rim = kind is StudioMaterialKind.Skin or StudioMaterialKind.SpectatorSkin ? 0.08f : 0.12f;
        material.RimTint = kind is StudioMaterialKind.Skin or StudioMaterialKind.SpectatorSkin ? 0.28f : 0.5f;
    }

    private static Texture2D BuildNormalTexture(int size, float strength, bool weave)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var first = weave ? MathF.Sin(x * 1.57f) : MathF.Sin(x * 0.73f + y * 0.31f);
            var second = weave ? MathF.Cos(y * 1.57f) : MathF.Cos(y * 0.61f - x * 0.27f);
            var nx = Math.Clamp(first * strength, -0.45f, 0.45f);
            var ny = Math.Clamp(second * strength, -0.45f, 0.45f);
            image.SetPixel(x, y, new Color(nx * 0.5f + 0.5f, ny * 0.5f + 0.5f, 1, 1));
        }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D BuildRoughnessTexture(int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var grain = 0.82f + MathF.Sin(x * 1.13f + y * 0.71f) * 0.08f +
                MathF.Cos(y * 0.43f - x * 0.37f) * 0.05f;
            image.SetPixel(x, y, new Color(grain, grain, grain, 1));
        }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    private static byte Channel(float value) =>
        (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);
}
