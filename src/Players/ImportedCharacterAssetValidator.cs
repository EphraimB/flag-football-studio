using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FlagFootballStudio.Presentation;

public sealed record ImportedCharacterAssetReport(
    string AssetPath,
    bool AssetExists,
    bool Loadable,
    bool SkeletonFound,
    int SkinnedMeshCount,
    IReadOnlyList<string> MissingBones,
    IReadOnlyList<string> MissingModules,
    IReadOnlyList<string> MissingAnchors,
    IReadOnlyList<string> MissingBlendShapes,
    IReadOnlyList<string> Warnings)
{
    public bool IsCompatible => AssetExists && Loadable && SkeletonFound && SkinnedMeshCount > 0 &&
        MissingBones.Count == 0 && MissingModules.Count == 0 && MissingAnchors.Count == 0 &&
        MissingBlendShapes.Count == 0;

    public string Summary => IsCompatible
        ? $"Imported character asset is compatible: {AssetPath}"
        : !AssetExists
            ? $"Imported character asset is missing: {AssetPath}"
            : $"Imported character asset is incompatible: {AssetPath}; " +
              $"bones {MissingBones.Count}, modules {MissingModules.Count}, anchors {MissingAnchors.Count}, " +
              $"blend shapes {MissingBlendShapes.Count}.";
}

/// <summary>Scene-tree inspection only. This validator never edits or synthesizes an asset.</summary>
public static class ImportedCharacterAssetValidator
{
    public static ImportedCharacterAssetReport Validate(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return Missing(assetPath ?? string.Empty, "No imported character asset path was configured.");
        if (!assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) &&
            !assetPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
            return Missing(assetPath, "The imported character contract accepts glTF 2.0 .glb or .gltf assets.", true);
        if (!ResourceLoader.Exists(assetPath))
            return Missing(assetPath, "No imported asset exists; procedural legacy rendering remains active.");

        var packed = GD.Load<PackedScene>(assetPath);
        if (packed is null)
            return Missing(assetPath, "Godot could not load the imported asset as a PackedScene.", true);

        Node? root = null;
        try
        {
            root = packed.Instantiate();
            var nodes = Descendants(root).Prepend(root).ToArray();
            var skeleton = nodes.OfType<Skeleton3D>()
                .FirstOrDefault(candidate => candidate.Name == ImportedCharacterAssetContract.RenderSkeleton)
                ?? nodes.OfType<Skeleton3D>().FirstOrDefault();
            var meshes = nodes.OfType<MeshInstance3D>().ToArray();
            var skinnedMeshes = meshes.Count(mesh => mesh.Skin is not null || !mesh.Skeleton.IsEmpty);
            var nodeNames = nodes.Select(node => node.Name.ToString()).ToHashSet(StringComparer.Ordinal);
            var bones = skeleton is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : Enumerable.Range(0, skeleton.GetBoneCount())
                    .Select(index => skeleton.GetBoneName(index).ToString())
                    .ToHashSet(StringComparer.Ordinal);
            var blendShapes = meshes.Where(mesh => mesh.Mesh is ArrayMesh)
                .SelectMany(mesh =>
                {
                    var arrayMesh = (ArrayMesh)mesh.Mesh;
                    return Enumerable.Range(0, arrayMesh.GetBlendShapeCount())
                        .Select(index => arrayMesh.GetBlendShapeName(index).ToString());
                })
                .ToHashSet(StringComparer.Ordinal);
            var warnings = new List<string>();
            foreach (var recommended in ImportedCharacterAssetContract.RecommendedDeformationBones)
                if (!bones.Contains(recommended)) warnings.Add($"Recommended deformation bone missing: {recommended}");
            if (meshes.Any(mesh => mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off))
                warnings.Add("One or more imported modules have shadow casting disabled.");

            return new ImportedCharacterAssetReport(
                assetPath, true, true, skeleton is not null, skinnedMeshes,
                ImportedCharacterAssetContract.RequiredRenderBones.Where(name => !bones.Contains(name)).ToArray(),
                ImportedCharacterAssetContract.RequiredModules.Where(name => !nodeNames.Contains(name)).ToArray(),
                ImportedCharacterAssetContract.RequiredAnchors.Where(name => !nodeNames.Contains(name)).ToArray(),
                ImportedCharacterAssetContract.RequiredBlendShapes.Where(name => !blendShapes.Contains(name)).ToArray(),
                warnings);
        }
        catch (Exception exception)
        {
            return Missing(assetPath, $"Asset inspection failed: {exception.Message}", true);
        }
        finally
        {
            root?.Free();
        }
    }

    private static ImportedCharacterAssetReport Missing(string path, string warning, bool exists = false) =>
        new(path, exists, false, false, 0,
            ImportedCharacterAssetContract.RequiredRenderBones,
            ImportedCharacterAssetContract.RequiredModules,
            ImportedCharacterAssetContract.RequiredAnchors,
            ImportedCharacterAssetContract.RequiredBlendShapes,
            [warning]);

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
