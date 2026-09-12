using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum HumanoidFaceSurface
{
    Skin,
    Eyes,
    Brows,
    Lips
}

public static class HumanoidFaceMesh
{
    private const string TopologyMeta = "face_topology_signature";

    public static ArrayMesh Create(
        FaceAppearance face,
        FacialExpressionPose expression = default,
        SpeechMouthPose? mouth = null)
    {
        ArgumentNullException.ThrowIfNull(face);
        var speech = mouth ?? SpeechMouthPose.Rest;
        var surfaces = Enum.GetValues<HumanoidFaceSurface>()
            .ToDictionary(surface => surface, _ => new FaceSurfaceGeometry());

        BuildHead(surfaces[HumanoidFaceSurface.Skin], face, expression, speech);
        BuildFeatures(surfaces, face, expression, speech);

        var mesh = new ArrayMesh();
        foreach (var surface in Enum.GetValues<HumanoidFaceSurface>())
        {
            surfaces[surface].Validate();
            AddSurface(mesh, surfaces[surface]);
        }

        var signature = string.Join(
            ":",
            Enum.GetValues<HumanoidFaceSurface>().Select(surface =>
                $"{surface}-{surfaces[surface].Vertices.Count}-{surfaces[surface].Indices.Count}"));
        mesh.SetMeta(TopologyMeta, signature);
        return mesh;
    }

    public static string TopologySignature(ArrayMesh mesh) => mesh.GetMeta(TopologyMeta).AsString();

    public static Vector3 EyeCenter(FaceAppearance face, bool left)
    {
        var eyeX = 0.12f * face.EyeSpacing * face.HeadWidth;
        return new Vector3(left ? -eyeX : eyeX, EyeY(face), -0.292f);
    }

    public static float CombinedJawOpen(FacialExpressionPose expression, SpeechMouthPose speech) =>
        Mathf.Clamp(speech.JawOpen + expression.MouthOpen * 0.45f, 0, 1);

    public static Vector3 MouthCenter(FaceAppearance face, FacialExpressionPose expression, SpeechMouthPose speech)
    {
        var jawOpen = CombinedJawOpen(expression, speech);
        var mouthY = -0.005f - (face.JawHeight - 1) * 0.035f - jawOpen * 0.018f;
        return new Vector3(0, mouthY, -0.304f - face.ChinProjection * 0.02f);
    }

    private static void BuildHead(
        FaceSurfaceGeometry surface,
        FaceAppearance face,
        FacialExpressionPose expression,
        SpeechMouthPose speech)
    {
        const int latitudeSegments = 12;
        const int radialSegments = 16;
        var firstVertex = surface.Vertices.Count;
        for (var latitude = 0; latitude <= latitudeSegments; latitude++)
        {
            var v = latitude / (float)latitudeSegments;
            var phi = v * Mathf.Pi;
            for (var side = 0; side < radialSegments; side++)
            {
                var u = side / (float)radialSegments;
                var theta = u * Mathf.Tau;
                var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                surface.AddVertex(DeformHeadVertex(unit, face, expression, speech), unit, new Vector2(u, v));
            }
        }

        for (var latitude = 0; latitude < latitudeSegments; latitude++)
        {
            for (var side = 0; side < radialSegments; side++)
            {
                var next = (side + 1) % radialSegments;
                var currentRing = firstVertex + latitude * radialSegments;
                var nextRing = currentRing + radialSegments;
                surface.AddQuad(currentRing + side, currentRing + next, nextRing + next, nextRing + side);
            }
        }

        var eyeY = EyeY(face);
        var noseDepth = 0.07f + (face.NoseProjection - FaceAppearance.MinimumOffset) * 0.1f;
        surface.AddBox(
            new Vector3(0, eyeY - 0.075f * face.NoseLength, -0.3f - noseDepth * 0.4f),
            new Vector3(0.1f * face.NoseWidth, 0.17f * face.NoseLength, noseDepth));

        var earY = 0.13f + face.EarPosition * 0.25f;
        var earX = 0.32f * face.HeadWidth + 0.025f;
        var earRadius = new Vector3(0.065f, 0.115f, 0.045f) * face.EarSize;
        surface.AddSphere(new Vector3(-earX, earY, 0), earRadius, 6, 8);
        surface.AddSphere(new Vector3(earX, earY, 0), earRadius, 6, 8);
    }

    private static Vector3 DeformHeadVertex(
        Vector3 unit,
        FaceAppearance face,
        FacialExpressionPose expression,
        SpeechMouthPose speech)
    {
        var lowerFace = Mathf.SmoothStep(0, 1, Mathf.Clamp((-unit.Y + 0.05f) / 0.9f, 0, 1));
        var chin = Mathf.SmoothStep(0, 1, Mathf.Clamp((-unit.Y - 0.5f) / 0.45f, 0, 1));
        var cheek = Mathf.Clamp(1 - Mathf.Abs(unit.Y - 0.05f) / 0.48f, 0, 1);
        var forehead = Mathf.SmoothStep(0, 1, Mathf.Clamp((unit.Y - 0.2f) / 0.75f, 0, 1));
        var front = Mathf.Clamp(-unit.Z, 0, 1);

        var width = Mathf.Lerp(face.HeadWidth, face.JawWidth, lowerFace);
        width = Mathf.Lerp(width, face.ChinWidth, chin);
        width += (face.CheekboneWidth - face.HeadWidth) * cheek * 0.65f;

        var verticalScale = unit.Y < 0
            ? face.HeadHeight * Mathf.Lerp(1, face.JawHeight, lowerFace)
            : face.HeadHeight * Mathf.Lerp(1, face.ForeheadHeight, forehead);
        var depth = 0.3f * (1 + (face.CheekFullness - 1) * cheek * front * 0.38f);

        var vertex = new Vector3(
            unit.X * 0.32f * width,
            0.13f + unit.Y * 0.34f * verticalScale,
            unit.Z * depth - face.ChinProjection * 0.08f * chin * front);
        var jawMask = Mathf.SmoothStep(0, 1, Mathf.Clamp((-unit.Y - 0.1f) / 0.72f, 0, 1));
        var jawOpen = CombinedJawOpen(expression, speech);
        vertex.Y -= jawOpen * 0.055f * jawMask;
        vertex.Z += jawOpen * 0.018f * jawMask;
        return vertex;
    }

    private static void BuildFeatures(
        Dictionary<HumanoidFaceSurface, FaceSurfaceGeometry> surfaces,
        FaceAppearance face,
        FacialExpressionPose expression,
        SpeechMouthPose speech)
    {
        var eyeY = EyeY(face);
        var eyeX = EyeCenter(face, false).X;
        var eyeRadius = new Vector3(0.055f, 0.038f, 0.018f) * face.EyeSize;
        surfaces[HumanoidFaceSurface.Eyes].AddSphere(new Vector3(-eyeX, eyeY, -0.292f), eyeRadius, 5, 8);
        surfaces[HumanoidFaceSurface.Eyes].AddSphere(new Vector3(eyeX, eyeY, -0.292f), eyeRadius, 5, 8);

        var browY = eyeY + 0.055f + 0.025f * face.EyebrowHeight + expression.BrowRaise * 0.045f;
        var browSize = new Vector3(0.115f * face.EyeSize, 0.018f, 0.025f);
        surfaces[HumanoidFaceSurface.Brows].AddRotatedBox(new Vector3(-eyeX, browY, -0.305f), browSize, expression.BrowTilt * 0.22f);
        surfaces[HumanoidFaceSurface.Brows].AddRotatedBox(new Vector3(eyeX, browY, -0.305f), browSize, -expression.BrowTilt * 0.22f);

        var jawOpen = CombinedJawOpen(expression, speech);
        var mouthCenter = MouthCenter(face, expression, speech);
        var widthScale = speech.Width * Mathf.Lerp(1, 0.82f, speech.Roundness);
        var mouthRadius = new Vector3(
            0.105f * face.MouthWidth * widthScale,
            0.022f * face.LipFullness * speech.LipFullness + jawOpen * 0.026f,
            0.018f);
        var firstMouthVertex = surfaces[HumanoidFaceSurface.Lips].Vertices.Count;
        surfaces[HumanoidFaceSurface.Lips].AddSphere(mouthCenter, mouthRadius, 4, 10);
        surfaces[HumanoidFaceSurface.Lips].TransformVertices(firstMouthVertex, vertex =>
        {
            var horizontal = Mathf.Clamp(Mathf.Abs(vertex.X - mouthCenter.X) / mouthRadius.X, 0, 1);
            var upper = vertex.Y >= mouthCenter.Y ? 1f : 0f;
            var lower = 1 - upper;
            vertex.Y += (expression.Smile + speech.CornerPull * 0.55f) * horizontal * 0.035f;
            vertex.Y += upper * speech.UpperLipRaise * 0.018f;
            vertex.Y -= lower * (speech.LowerLipDrop * 0.018f + jawOpen * 0.022f);
            return vertex;
        });
    }

    private static float EyeY(FaceAppearance face) => 0.18f + face.EyeVerticalPosition * 0.22f;

    private static void AddSurface(ArrayMesh mesh, FaceSurfaceGeometry surface)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = surface.Vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = surface.Normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = surface.Uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = surface.Indices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private sealed class FaceSurfaceGeometry
    {
        public List<Vector3> Vertices { get; } = [];
        public List<Vector3> Normals { get; } = [];
        public List<Vector2> Uvs { get; } = [];
        public List<int> Indices { get; } = [];

        public void AddSphere(Vector3 center, Vector3 radius, int latitudeSegments, int radialSegments)
        {
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
                    AddVertex(center + new Vector3(unit.X * radius.X, unit.Y * radius.Y, unit.Z * radius.Z), unit, new Vector2(u, v));
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

        public void AddBox(Vector3 center, Vector3 size)
        {
            var half = size * 0.5f;
            AddFace(center, new Vector3(-half.X, -half.Y, -half.Z), new Vector3(half.X, -half.Y, -half.Z), new Vector3(half.X, half.Y, -half.Z), new Vector3(-half.X, half.Y, -half.Z), Vector3.Back);
            AddFace(center, new Vector3(half.X, -half.Y, half.Z), new Vector3(-half.X, -half.Y, half.Z), new Vector3(-half.X, half.Y, half.Z), new Vector3(half.X, half.Y, half.Z), Vector3.Forward);
            AddFace(center, new Vector3(-half.X, -half.Y, half.Z), new Vector3(-half.X, -half.Y, -half.Z), new Vector3(-half.X, half.Y, -half.Z), new Vector3(-half.X, half.Y, half.Z), Vector3.Left);
            AddFace(center, new Vector3(half.X, -half.Y, -half.Z), new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, half.Y, half.Z), new Vector3(half.X, half.Y, -half.Z), Vector3.Right);
            AddFace(center, new Vector3(-half.X, half.Y, -half.Z), new Vector3(half.X, half.Y, -half.Z), new Vector3(half.X, half.Y, half.Z), new Vector3(-half.X, half.Y, half.Z), Vector3.Up);
            AddFace(center, new Vector3(-half.X, -half.Y, half.Z), new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, -half.Y, -half.Z), new Vector3(-half.X, -half.Y, -half.Z), Vector3.Down);
        }

        public void AddRotatedBox(Vector3 center, Vector3 size, float rotation)
        {
            var firstVertex = Vertices.Count;
            AddBox(center, size);
            var cosine = Mathf.Cos(rotation);
            var sine = Mathf.Sin(rotation);
            TransformVertices(firstVertex, vertex =>
            {
                var offset = vertex - center;
                return center + new Vector3(
                    offset.X * cosine - offset.Y * sine,
                    offset.X * sine + offset.Y * cosine,
                    offset.Z);
            });
        }

        public void TransformVertices(int firstVertex, Func<Vector3, Vector3> transform)
        {
            for (var index = firstVertex; index < Vertices.Count; index++)
                Vertices[index] = transform(Vertices[index]);
        }

        public void AddVertex(Vector3 vertex, Vector3 normal, Vector2 uv)
        {
            Vertices.Add(vertex);
            Normals.Add(normal);
            Uvs.Add(uv);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);
            Indices.Add(a);
            Indices.Add(c);
            Indices.Add(d);
        }

        public void Validate()
        {
            if (Vertices.Count == 0 || Indices.Count == 0 || Vertices.Count != Normals.Count || Vertices.Count != Uvs.Count)
                throw new InvalidOperationException("The procedural face surface has invalid topology arrays.");
            if (Vertices.Exists(vertex => !float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z)))
                throw new InvalidOperationException("The procedural face contains a non-finite vertex.");
            if (Indices.Exists(index => index < 0 || index >= Vertices.Count))
                throw new InvalidOperationException("The procedural face contains an invalid triangle index.");
        }

        private void AddFace(Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var first = Vertices.Count;
            AddVertex(center + a, normal, new Vector2(0, 1));
            AddVertex(center + b, normal, new Vector2(1, 1));
            AddVertex(center + c, normal, new Vector2(1, 0));
            AddVertex(center + d, normal, new Vector2(0, 0));
            AddQuad(first, first + 1, first + 2, first + 3);
        }
    }
}
