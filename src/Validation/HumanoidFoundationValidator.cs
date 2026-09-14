using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidFoundationValidator : Node
{
    public async Task RunAsync()
    {
        ValidateAppearancePersistence();
        var team = new Team(Guid.NewGuid(), "Validation");
        var player = new Player(Guid.NewGuid(), "Rig Test", 42, PlayerPosition.Receiver);
        team.AddPlayer(player);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);

        var primaryRig = new HumanoidRig { Name = "PrimaryValidationRig", Visible = false };
        var comparisonRig = new HumanoidRig { Name = "ComparisonValidationRig", Visible = false };
        AddChild(primaryRig);
        AddChild(comparisonRig);
        primaryRig.Apply(player, appearance, uniform);
        comparisonRig.Apply(player, appearance, uniform);
        await NextFrame();

        Require(ReferenceEquals(primaryRig.BodyMeshResource, comparisonRig.BodyMeshResource), "Players do not share the reusable body mesh resource.");
        Require(primaryRig.SkeletonBoneCount == HumanoidSkeletonDefinition.Bones.Count, "The runtime skeleton does not match the standardized bone hierarchy.");
        Require(HumanoidSkinnedMesh.SharedSkin.GetBindCount() == HumanoidSkeletonDefinition.Bones.Count, "The shared skin does not bind every standardized bone.");
        Require(primaryRig.BodyMeshResource.GetSurfaceCount() == Enum.GetValues<HumanoidMeshSurface>().Length, "The reusable mesh is missing material surfaces.");
        Require(!string.IsNullOrWhiteSpace(primaryRig.TopologySignature), "The reusable mesh topology signature was not generated.");

        appearance.SetHeight(1.4f);
        primaryRig.Apply(player, appearance, uniform);
        await NextFrame();
        var shortEyeHeight = primaryRig.EyeAnchor.GlobalPosition.Y;
        ValidateAnchors(primaryRig, "minimum height");

        appearance.SetHeight(2.3f);
        primaryRig.Apply(player, appearance, uniform);
        await NextFrame();
        var tallEyeHeight = primaryRig.EyeAnchor.GlobalPosition.Y;
        ValidateAnchors(primaryRig, "maximum height");
        Require(tallEyeHeight > shortEyeHeight, "Extreme height changes did not move the eye anchor with the rig.");

        appearance.SetHeight(1.8f);
        var buildWidths = new List<float>();
        foreach (var build in new[] { BodyBuild.Slim, BodyBuild.Average, BodyBuild.Athletic, BodyBuild.Heavy })
        {
            appearance.SetBodyBuild(build);
            primaryRig.Apply(player, appearance, uniform);
            await NextFrame();
            ValidateAnchors(primaryRig, $"{build} build");
            Require(ReferenceEquals(primaryRig.BodyMeshResource, comparisonRig.BodyMeshResource), $"{build} build replaced the shared topology.");
            buildWidths.Add(primaryRig.BonePoseScale(HumanoidSkeletonDefinition.Hips).X);
        }
        Require(buildWidths[0] < buildWidths[1] && buildWidths[1] < buildWidths[2] && buildWidths[2] < buildWidths[3], "Body-build presets do not produce increasing skeletal width.");

        appearance.SetBodyProportions(0.7f, 1.3f, 0.7f, 1.3f, 0.75f, 0.75f);
        primaryRig.Apply(player, appearance, uniform);
        await NextFrame();
        var shortArmAnchor = primaryRig.RightHandAnchor.GlobalPosition;
        var shortLegFoot = primaryRig.BoneGlobalPose(HumanoidSkeletonDefinition.RightFoot).Origin;
        ValidateAnchors(primaryRig, "minimum limb proportions");

        appearance.SetBodyProportions(1.3f, 0.7f, 1.3f, 0.7f, 1.25f, 1.25f);
        primaryRig.Apply(player, appearance, uniform);
        await NextFrame();
        var longArmAnchor = primaryRig.RightHandAnchor.GlobalPosition;
        var longLegFoot = primaryRig.BoneGlobalPose(HumanoidSkeletonDefinition.RightFoot).Origin;
        ValidateAnchors(primaryRig, "maximum limb proportions");
        Require(shortArmAnchor.DistanceTo(longArmAnchor) > 0.05f, $"Arm-length morphing did not move the solved hand anchor: {shortArmAnchor} -> {longArmAnchor}.");
        Require(shortLegFoot.DistanceTo(longLegFoot) > 0.05f, "Leg-length morphing did not move the foot bone.");

        var animatedBones = new Dictionary<HumanoidAnimationState, string>
        {
            [HumanoidAnimationState.Jog] = HumanoidSkeletonDefinition.LeftUpperLeg,
            [HumanoidAnimationState.Sprint] = HumanoidSkeletonDefinition.RightUpperLeg,
            [HumanoidAnimationState.Throw] = HumanoidSkeletonDefinition.RightUpperArm,
            [HumanoidAnimationState.Catch] = HumanoidSkeletonDefinition.LeftUpperArm,
            [HumanoidAnimationState.FlagPull] = HumanoidSkeletonDefinition.Chest
        };
        foreach (var animation in animatedBones)
        {
            primaryRig.SetAnimationState(animation.Key, true);
            await WaitSeconds(0.12);
            var rotation = primaryRig.BonePoseRotation(animation.Value);
            Require(rotation.AngleTo(Quaternion.Identity) > 0.001f, $"{animation.Key} did not deform its expected bone.");
            ValidateAnchors(primaryRig, $"{animation.Key} animation");
        }

        var povCamera = new Camera3D { Name = "ValidationPovCamera" };
        AddChild(povCamera);
        povCamera.Reparent(primaryRig.EyeAnchor, false);
        Require(povCamera.GetParent() == primaryRig.EyeAnchor, "Player POV camera did not attach to the morphed eye anchor.");
        ValidateVector(povCamera.GlobalPosition, "Player POV camera");

        primaryRig.QueueFree();
        comparisonRig.QueueFree();
    }

    private static void ValidateAppearancePersistence()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var appearance = project.AppearanceFor(player.Id);
        appearance.SetHeight(2.3f);
        appearance.SetBodyBuild(BodyBuild.Heavy);
        appearance.SetBodyProportions(1.3f, 0.7f, 1.2f, 0.8f, 1.25f, 0.75f);

        var serializer = new ProjectJsonSerializer();
        var restored = serializer.DeserializeProject(serializer.SerializeProject(project)).AppearanceFor(player.Id);
        Require(restored.HeightMeters == appearance.HeightMeters, "Height did not survive project JSON round trip.");
        Require(restored.BodyBuild == appearance.BodyBuild, "Body build did not survive project JSON round trip.");
        Require(restored.ShoulderWidth == appearance.ShoulderWidth && restored.ChestWidth == appearance.ChestWidth, "Upper-body proportions did not survive project JSON round trip.");
        Require(restored.WaistWidth == appearance.WaistWidth && restored.HipWidth == appearance.HipWidth, "Core proportions did not survive project JSON round trip.");
        Require(restored.ArmLength == appearance.ArmLength && restored.LegLength == appearance.LegLength, "Limb proportions did not survive project JSON round trip.");
    }

    private void ValidateAnchors(HumanoidRig rig, string context)
    {
        ValidateVector(rig.EyeAnchor.GlobalPosition, $"Eye anchor after {context}");
        ValidateVector(rig.CatchAnchor.GlobalPosition, $"Hand anchor after {context}");
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
