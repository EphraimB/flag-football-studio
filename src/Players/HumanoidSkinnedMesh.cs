using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum HumanoidMeshSurface
{
    Skin,
    Jersey,
    Primary,
    Shorts,
    Shoes
}

public static class HumanoidSkinnedMesh
{
    private static readonly Dictionary<string, int> BoneIndices = HumanoidSkeletonDefinition.Bones
        .Select((bone, index) => (bone.Name, index))
        .ToDictionary(entry => entry.Name, entry => entry.index);
    private static ArrayMesh? _sharedMesh;
    private static Skin? _sharedSkin;

    public static ArrayMesh SharedMesh => _sharedMesh ??= BuildMesh();
    public static Skin SharedSkin => _sharedSkin ??= BuildSkin();
    public static string TopologySignature { get; private set; } = string.Empty;

    private static ArrayMesh BuildMesh()
    {
        var surfaces = Enum.GetValues<HumanoidMeshSurface>()
            .ToDictionary(surface => surface, _ => new SurfaceGeometry());

        BuildSkinSurface(surfaces[HumanoidMeshSurface.Skin]);
        BuildJerseySurface(surfaces[HumanoidMeshSurface.Jersey]);
        BuildPrimarySurface(surfaces[HumanoidMeshSurface.Primary]);
        BuildShortsSurface(surfaces[HumanoidMeshSurface.Shorts]);
        BuildShoesSurface(surfaces[HumanoidMeshSurface.Shoes]);

        var mesh = new ArrayMesh();
        foreach (var surface in Enum.GetValues<HumanoidMeshSurface>())
            AddSurface(mesh, surfaces[surface]);

        TopologySignature = string.Join(
            ":",
            Enum.GetValues<HumanoidMeshSurface>().Select(surface =>
                $"{surface}-{surfaces[surface].Vertices.Count}-{surfaces[surface].Indices.Count}"));
        return mesh;
    }

    private static void BuildSkinSurface(SurfaceGeometry surface)
    {
        surface.AddTube(new Vector3(0, 1.82f, 0), new Vector3(0, 2.01f, 0), new Vector2(0.15f, 0.14f), Bone(HumanoidSkeletonDefinition.Neck), Bone(HumanoidSkeletonDefinition.Head));

        AddMirroredArms(surface, new Vector3(-0.48f, 1.22f, 0), new Vector3(-0.48f, 0.82f, 0), 0.115f,
            HumanoidSkeletonDefinition.LeftLowerArm, HumanoidSkeletonDefinition.LeftHand,
            HumanoidSkeletonDefinition.RightLowerArm, HumanoidSkeletonDefinition.RightHand);
        surface.AddSphere(new Vector3(-0.48f, 0.78f, 0), new Vector3(0.14f, 0.16f, 0.12f), Bone(HumanoidSkeletonDefinition.LeftHand));
        surface.AddSphere(new Vector3(0.48f, 0.78f, 0), new Vector3(0.14f, 0.16f, 0.12f), Bone(HumanoidSkeletonDefinition.RightHand));

        AddMirroredLegs(surface, new Vector3(-0.21f, 0.32f, 0), new Vector3(-0.21f, -0.12f, 0), 0.135f,
            HumanoidSkeletonDefinition.LeftLowerLeg, HumanoidSkeletonDefinition.LeftFoot,
            HumanoidSkeletonDefinition.RightLowerLeg, HumanoidSkeletonDefinition.RightFoot);
    }

    private static void BuildJerseySurface(SurfaceGeometry surface)
    {
        surface.AddTube(new Vector3(0, 0.94f, 0), new Vector3(0, 1.5f, 0), new Vector2(0.32f, 0.24f), new Vector2(0.42f, 0.27f), Bone(HumanoidSkeletonDefinition.Hips), Bone(HumanoidSkeletonDefinition.Chest));
        AddMirroredArms(surface, new Vector3(-0.48f, 1.68f, 0), new Vector3(-0.48f, 1.22f, 0), 0.145f,
            HumanoidSkeletonDefinition.LeftUpperArm, HumanoidSkeletonDefinition.LeftLowerArm,
            HumanoidSkeletonDefinition.RightUpperArm, HumanoidSkeletonDefinition.RightLowerArm);
    }

    private static void BuildPrimarySurface(SurfaceGeometry surface) =>
        surface.AddTube(new Vector3(0, 1.48f, 0), new Vector3(0, 1.73f, 0), new Vector2(0.42f, 0.27f), new Vector2(0.48f, 0.25f), Bone(HumanoidSkeletonDefinition.Chest), Bone(HumanoidSkeletonDefinition.Chest));

    private static void BuildShortsSurface(SurfaceGeometry surface)
    {
        surface.AddTube(new Vector3(0, 0.72f, 0), new Vector3(0, 0.99f, 0), new Vector2(0.34f, 0.24f), Bone(HumanoidSkeletonDefinition.Hips), Bone(HumanoidSkeletonDefinition.Hips));
        AddMirroredLegs(surface, new Vector3(-0.21f, 0.82f, 0), new Vector3(-0.21f, 0.32f, 0), 0.18f,
            HumanoidSkeletonDefinition.LeftUpperLeg, HumanoidSkeletonDefinition.LeftLowerLeg,
            HumanoidSkeletonDefinition.RightUpperLeg, HumanoidSkeletonDefinition.RightLowerLeg);
    }

    private static void BuildShoesSurface(SurfaceGeometry surface)
    {
        surface.AddBox(new Vector3(-0.21f, -0.14f, -0.1f), new Vector3(0.27f, 0.18f, 0.46f), Bone(HumanoidSkeletonDefinition.LeftFoot));
        surface.AddBox(new Vector3(0.21f, -0.14f, -0.1f), new Vector3(0.27f, 0.18f, 0.46f), Bone(HumanoidSkeletonDefinition.RightFoot));
    }

    private static void AddMirroredArms(
        SurfaceGeometry surface,
        Vector3 leftStart,
        Vector3 leftEnd,
        float radius,
        string leftStartBone,
        string leftEndBone,
        string rightStartBone,
        string rightEndBone)
    {
        surface.AddTube(leftStart, leftEnd, new Vector2(radius, radius), Bone(leftStartBone), Bone(leftEndBone));
        surface.AddTube(new Vector3(-leftStart.X, leftStart.Y, leftStart.Z), new Vector3(-leftEnd.X, leftEnd.Y, leftEnd.Z), new Vector2(radius, radius), Bone(rightStartBone), Bone(rightEndBone));
    }

    private static void AddMirroredLegs(
        SurfaceGeometry surface,
        Vector3 leftStart,
        Vector3 leftEnd,
        float radius,
        string leftStartBone,
        string leftEndBone,
        string rightStartBone,
        string rightEndBone) =>
        AddMirroredArms(surface, leftStart, leftEnd, radius, leftStartBone, leftEndBone, rightStartBone, rightEndBone);

    private static int Bone(string name) => BoneIndices[name];

    private static void AddSurface(ArrayMesh mesh, SurfaceGeometry surface)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = surface.Vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = surface.Normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = surface.Uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Bones] = surface.Bones.ToArray();
        arrays[(int)Mesh.ArrayType.Weights] = surface.Weights.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = surface.Indices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private static Skin BuildSkin()
    {
        var skin = new Skin();
        var globalRests = new Dictionary<string, Transform3D>();
        foreach (var bone in HumanoidSkeletonDefinition.Bones)
        {
            var local = new Transform3D(Basis.Identity, bone.RestPosition);
            var global = bone.Parent is null ? local : globalRests[bone.Parent] * local;
            globalRests[bone.Name] = global;
            skin.AddBind(BoneIndices[bone.Name], global.AffineInverse());
        }
        return skin;
    }

    private sealed class SurfaceGeometry
    {
        public List<Vector3> Vertices { get; } = [];
        public List<Vector3> Normals { get; } = [];
        public List<Vector2> Uvs { get; } = [];
        public List<int> Bones { get; } = [];
        public List<float> Weights { get; } = [];
        public List<int> Indices { get; } = [];

        public void AddTube(Vector3 start, Vector3 end, Vector2 radius, int startBone, int endBone) =>
            AddTube(start, end, radius, radius, startBone, endBone);

        public void AddTube(Vector3 start, Vector3 end, Vector2 startRadius, Vector2 endRadius, int startBone, int endBone)
        {
            const int ringCount = 4;
            const int radialSegments = 10;
            var firstVertex = Vertices.Count;
            for (var ring = 0; ring < ringCount; ring++)
            {
                var t = ring / (float)(ringCount - 1);
                var center = start.Lerp(end, t);
                var radius = startRadius.Lerp(endRadius, t);
                for (var side = 0; side < radialSegments; side++)
                {
                    var angle = Mathf.Tau * side / radialSegments;
                    var normal = new Vector3(Mathf.Cos(angle) / radius.X, 0, Mathf.Sin(angle) / radius.Y).Normalized();
                    AddVertex(
                        center + new Vector3(Mathf.Cos(angle) * radius.X, 0, Mathf.Sin(angle) * radius.Y),
                        normal,
                        new Vector2(side / (float)radialSegments, t),
                        startBone,
                        endBone,
                        1 - t,
                        t);
                }
            }

            for (var ring = 0; ring < ringCount - 1; ring++)
            {
                for (var side = 0; side < radialSegments; side++)
                {
                    var next = (side + 1) % radialSegments;
                    var currentRing = firstVertex + ring * radialSegments;
                    var nextRing = currentRing + radialSegments;
                    AddQuad(currentRing + side, currentRing + next, nextRing + next, nextRing + side);
                }
            }
        }

        public void AddSphere(Vector3 center, Vector3 radius, int bone)
        {
            const int latitudeSegments = 7;
            const int radialSegments = 10;
            var firstVertex = Vertices.Count;
            for (var latitude = 0; latitude <= latitudeSegments; latitude++)
            {
                var v = latitude / (float)latitudeSegments;
                var phi = v * Mathf.Pi;
                for (var side = 0; side < radialSegments; side++)
                {
                    var u = side / (float)radialSegments;
                    var theta = u * Mathf.Tau;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    AddVertex(center + new Vector3(unit.X * radius.X, unit.Y * radius.Y, unit.Z * radius.Z), unit, new Vector2(u, v), bone, bone, 1, 0);
                }
            }

            for (var latitude = 0; latitude < latitudeSegments; latitude++)
            {
                for (var side = 0; side < radialSegments; side++)
                {
                    var next = (side + 1) % radialSegments;
                    var currentRing = firstVertex + latitude * radialSegments;
                    var nextRing = currentRing + radialSegments;
                    AddQuad(currentRing + side, currentRing + next, nextRing + next, nextRing + side);
                }
            }
        }

        public void AddBox(Vector3 center, Vector3 size, int bone)
        {
            var half = size * 0.5f;
            AddFace(center, new Vector3(-half.X, -half.Y, -half.Z), new Vector3(half.X, -half.Y, -half.Z), new Vector3(half.X, half.Y, -half.Z), new Vector3(-half.X, half.Y, -half.Z), Vector3.Back, bone);
            AddFace(center, new Vector3(half.X, -half.Y, half.Z), new Vector3(-half.X, -half.Y, half.Z), new Vector3(-half.X, half.Y, half.Z), new Vector3(half.X, half.Y, half.Z), Vector3.Forward, bone);
            AddFace(center, new Vector3(-half.X, -half.Y, half.Z), new Vector3(-half.X, -half.Y, -half.Z), new Vector3(-half.X, half.Y, -half.Z), new Vector3(-half.X, half.Y, half.Z), Vector3.Left, bone);
            AddFace(center, new Vector3(half.X, -half.Y, -half.Z), new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, half.Y, half.Z), new Vector3(half.X, half.Y, -half.Z), Vector3.Right, bone);
            AddFace(center, new Vector3(-half.X, half.Y, -half.Z), new Vector3(half.X, half.Y, -half.Z), new Vector3(half.X, half.Y, half.Z), new Vector3(-half.X, half.Y, half.Z), Vector3.Up, bone);
            AddFace(center, new Vector3(-half.X, -half.Y, half.Z), new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, -half.Y, -half.Z), new Vector3(-half.X, -half.Y, -half.Z), Vector3.Down, bone);
        }

        private void AddFace(Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int bone)
        {
            var first = Vertices.Count;
            AddVertex(center + a, normal, new Vector2(0, 1), bone, bone, 1, 0);
            AddVertex(center + b, normal, new Vector2(1, 1), bone, bone, 1, 0);
            AddVertex(center + c, normal, new Vector2(1, 0), bone, bone, 1, 0);
            AddVertex(center + d, normal, new Vector2(0, 0), bone, bone, 1, 0);
            AddQuad(first, first + 1, first + 2, first + 3);
        }

        private void AddVertex(Vector3 vertex, Vector3 normal, Vector2 uv, int firstBone, int secondBone, float firstWeight, float secondWeight)
        {
            Vertices.Add(vertex);
            Normals.Add(normal);
            Uvs.Add(uv);
            Bones.Add(firstBone);
            Bones.Add(secondBone);
            Bones.Add(0);
            Bones.Add(0);
            Weights.Add(firstWeight);
            Weights.Add(secondWeight);
            Weights.Add(0);
            Weights.Add(0);
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
            Indices.Add(a);
            Indices.Add(c);
            Indices.Add(d);
        }
    }
}
