using System;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FaceFoundationValidator : Node
{
    public async Task RunAsync()
    {
        ValidateParameterLimits();
        ValidateExtremeCombinations();
        ValidatePersistence();
        ValidateTopology();
        await ValidateRigIntegrationAsync();
    }

    private static void ValidateParameterLimits()
    {
        var face = new FaceAppearance();
        face.SetParameters(
            -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
            -10, -10, -10, -10, -10, -10, -10, -10, -10, -10);

        Require(face.HeadWidth == FaceAppearance.MinimumScale, "Head width did not clamp to its minimum.");
        Require(face.HeadHeight == FaceAppearance.MinimumScale, "Head height did not clamp to its minimum.");
        Require(face.JawWidth == FaceAppearance.MinimumScale, "Jaw width did not clamp to its minimum.");
        Require(face.ChinWidth == FaceAppearance.MinimumScale, "Chin width did not clamp to its minimum.");
        Require(face.CheekboneWidth == FaceAppearance.MinimumScale, "Cheekbone width did not clamp to its minimum.");
        Require(face.EyeSize == FaceAppearance.MinimumScale, "Eye size did not clamp to its minimum.");
        Require(face.EarSize == FaceAppearance.MinimumScale, "Ear size did not clamp to its minimum.");
        Require(face.ChinProjection == FaceAppearance.MinimumOffset, "Chin projection did not clamp to its minimum.");
        Require(face.EyeVerticalPosition == FaceAppearance.MinimumOffset, "Eye position did not clamp to its minimum.");
        Require(face.NoseProjection == FaceAppearance.MinimumOffset, "Nose projection did not clamp to its minimum.");
        Require(face.EarPosition == FaceAppearance.MinimumOffset, "Ear position did not clamp to its minimum.");

        face.SetParameters(
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10);

        Require(face.HeadWidth == FaceAppearance.MaximumScale, "Head width did not clamp to its maximum.");
        Require(face.HeadHeight == FaceAppearance.MaximumScale, "Head height did not clamp to its maximum.");
        Require(face.JawHeight == FaceAppearance.MaximumScale, "Jaw height did not clamp to its maximum.");
        Require(face.ForeheadHeight == FaceAppearance.MaximumScale, "Forehead height did not clamp to its maximum.");
        Require(face.LipFullness == FaceAppearance.MaximumScale, "Lip fullness did not clamp to its maximum.");
        Require(face.ChinProjection == FaceAppearance.MaximumOffset, "Chin projection did not clamp to its maximum.");
        Require(face.EyeVerticalPosition == FaceAppearance.MaximumOffset, "Eye position did not clamp to its maximum.");
        Require(face.NoseProjection == FaceAppearance.MaximumOffset, "Nose projection did not clamp to its maximum.");
        Require(face.EarPosition == FaceAppearance.MaximumOffset, "Ear position did not clamp to its maximum.");
    }

    private static void ValidateExtremeCombinations()
    {
        var face = new FaceAppearance();
        face.SetParameters(
            FaceAppearance.MinimumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumOffset,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumOffset,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumOffset,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumScale,
            FaceAppearance.MaximumOffset);

        Require(face.JawWidth <= face.HeadWidth + 0.12f, "Jaw/head safety constraint failed.");
        Require(face.ChinWidth <= face.JawWidth + 0.03f, "Chin/jaw safety constraint failed.");
        Require(face.CheekboneWidth <= face.HeadWidth + 0.1f, "Cheek/head safety constraint failed.");
        Require(face.EyeSpacing <= face.HeadWidth + 0.08f, "Eye-spacing/head safety constraint failed.");
        Require(face.EyeSize <= 0.65f + face.EyeSpacing * 0.45f, "Eye-size/spacing safety constraint failed.");
        Require(face.NoseWidth <= face.EyeSpacing + 0.08f, "Nose/eye safety constraint failed.");
        Require(face.MouthWidth <= face.JawWidth + 0.08f, "Mouth/jaw safety constraint failed.");

        var mesh = HumanoidFaceMesh.Create(face);
        ValidateAabb(mesh.GetAabb(), "combined facial extremes");
    }

    private static void ValidatePersistence()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var face = project.AppearanceFor(player.Id).Face;
        face.SetEyeColor(new AppearanceColor(37, 116, 164));
        face.SetParameters(
            0.82f, 1.18f, 0.87f, 1.13f, 0.83f, 0.14f, 0.9f, 1.17f, 1.12f, 0.91f,
            0.94f, -0.11f, 1.09f, 0.88f, 1.16f, 0.12f, 0.93f, 1.19f, 1.11f, -0.13f);

        var serializer = new ProjectJsonSerializer();
        var restored = serializer.DeserializeProject(serializer.SerializeProject(project)).AppearanceFor(player.Id).Face;
        RequireFacesEqual(face, restored);
    }

    private static void ValidateTopology()
    {
        var defaultFace = new FaceAppearance();
        var minimumFace = UniformFace(FaceAppearance.MinimumScale, FaceAppearance.MinimumOffset);
        var maximumFace = UniformFace(FaceAppearance.MaximumScale, FaceAppearance.MaximumOffset);
        var meshes = new[]
        {
            HumanoidFaceMesh.Create(defaultFace),
            HumanoidFaceMesh.Create(minimumFace),
            HumanoidFaceMesh.Create(maximumFace)
        };

        var signature = HumanoidFaceMesh.TopologySignature(meshes[0]);
        Require(!string.IsNullOrWhiteSpace(signature), "The facial topology signature is missing.");
        foreach (var mesh in meshes)
        {
            Require(mesh.GetSurfaceCount() == Enum.GetValues<HumanoidFaceSurface>().Length, "A face mesh is missing a material surface.");
            Require(HumanoidFaceMesh.TopologySignature(mesh) == signature, "Facial customization changed the shared topology layout.");
            ValidateAabb(mesh.GetAabb(), "facial topology sample");
        }
    }

    private async Task ValidateRigIntegrationAsync()
    {
        var team = new Team(Guid.NewGuid(), "Face Validation");
        var player = new Player(Guid.NewGuid(), "Face Test", 18, PlayerPosition.Receiver);
        team.AddPlayer(player);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);
        appearance.SetHair(HairStyle.Bun, new AppearanceColor(35, 24, 18));
        appearance.SetAccessories(PlayerAccessories.Headband | PlayerAccessories.Visor);

        var rig = new HumanoidRig { Name = "FaceValidationRig", Visible = false };
        AddChild(rig);
        rig.Apply(player, appearance, uniform);
        await NextFrame();

        var bodyMesh = rig.BodyMeshResource;
        var eyeLocal = rig.EyeAnchor.Position;
        var handBoneBefore = rig.BoneGlobalPose(HumanoidSkeletonDefinition.RightHand).Origin;
        var initialSignature = HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource);

        appearance.Face.SetParameters(
            0.75f, 1.25f, 1.25f, 1.25f, 1.25f, 0.2f, 1.25f, 1.25f, 1.25f, 1.25f,
            1.25f, 0.2f, 1.25f, 1.25f, 1.25f, 0.2f, 1.25f, 1.25f, 1.25f, 0.2f);
        rig.Apply(player, appearance, uniform);
        var handBoneAfter = rig.BoneGlobalPose(HumanoidSkeletonDefinition.RightHand).Origin;
        await NextFrame();

        Require(ReferenceEquals(bodyMesh, rig.BodyMeshResource), "Facial morphing replaced the shared skinned body resource.");
        Require(HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource) == initialSignature, "Facial morphing changed runtime topology.");
        Require(rig.EyeAnchor.Position.IsEqualApprox(eyeLocal), "Facial morphing moved the stable eye/POV anchor.");
        Require(handBoneAfter.DistanceTo(handBoneBefore) < 0.001f,
            "Facial morphing changed the hand-bone transform.");
        ValidateVector(rig.EyeAnchor.GlobalPosition, "POV anchor after facial extremes");
        ValidateVector(rig.CatchAnchor.GlobalPosition, "hand anchor after facial extremes");

        var camera = new Camera3D { Name = "FaceValidationPovCamera" };
        AddChild(camera);
        camera.Reparent(rig.EyeAnchor, false);
        Require(camera.GetParent() == rig.EyeAnchor, "Player POV camera did not attach after facial morphing.");
        ValidateVector(camera.GlobalPosition, "Player POV camera after facial morphing");

        rig.SetAnimationState(HumanoidAnimationState.Jog, true);
        await WaitSeconds(0.1);
        rig.SetAnimationState(HumanoidAnimationState.Throw, true);
        await WaitSeconds(0.1);
        Require(HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource) == initialSignature, "Animation changed facial topology.");
        ValidateVector(rig.EyeAnchor.GlobalPosition, "POV anchor during animation");

        camera.QueueFree();
        rig.QueueFree();
    }

    private static FaceAppearance UniformFace(float scale, float offset)
    {
        var face = new FaceAppearance();
        face.SetParameters(
            scale, scale, scale, scale, scale, offset, scale, scale, scale, scale,
            scale, offset, scale, scale, scale, offset, scale, scale, scale, offset);
        return face;
    }

    private static void RequireFacesEqual(FaceAppearance expected, FaceAppearance actual)
    {
        Require(expected.HeadWidth == actual.HeadWidth && expected.HeadHeight == actual.HeadHeight, "Head dimensions did not survive JSON round trip.");
        Require(expected.JawWidth == actual.JawWidth && expected.JawHeight == actual.JawHeight, "Jaw dimensions did not survive JSON round trip.");
        Require(expected.ChinWidth == actual.ChinWidth && expected.ChinProjection == actual.ChinProjection, "Chin parameters did not survive JSON round trip.");
        Require(expected.CheekboneWidth == actual.CheekboneWidth && expected.CheekFullness == actual.CheekFullness, "Cheek parameters did not survive JSON round trip.");
        Require(expected.ForeheadHeight == actual.ForeheadHeight, "Forehead height did not survive JSON round trip.");
        Require(expected.EyeSpacing == actual.EyeSpacing && expected.EyeSize == actual.EyeSize && expected.EyeVerticalPosition == actual.EyeVerticalPosition, "Eye parameters did not survive JSON round trip.");
        Require(expected.EyebrowHeight == actual.EyebrowHeight, "Eyebrow height did not survive JSON round trip.");
        Require(expected.NoseWidth == actual.NoseWidth && expected.NoseLength == actual.NoseLength && expected.NoseProjection == actual.NoseProjection, "Nose parameters did not survive JSON round trip.");
        Require(expected.MouthWidth == actual.MouthWidth && expected.LipFullness == actual.LipFullness, "Mouth parameters did not survive JSON round trip.");
        Require(expected.EarSize == actual.EarSize && expected.EarPosition == actual.EarPosition, "Ear parameters did not survive JSON round trip.");
        Require(expected.EyeColor == actual.EyeColor, "Eye color did not survive JSON round trip.");
    }

    private static void ValidateAabb(Aabb bounds, string context)
    {
        ValidateVector(bounds.Position, $"{context} bounds position");
        ValidateVector(bounds.Size, $"{context} bounds size");
        Require(bounds.Size.X > 0 && bounds.Size.Y > 0 && bounds.Size.Z > 0, $"{context} produced empty geometry.");
    }

    private static void ValidateVector(Vector3 vector, string label) =>
        Require(float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z), $"{label} is not finite.");

    private async Task NextFrame() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
