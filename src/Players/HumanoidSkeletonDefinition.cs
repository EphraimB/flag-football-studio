using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public readonly record struct HumanoidBoneDefinition(string Name, string? Parent, Vector3 RestPosition);

public static class HumanoidSkeletonDefinition
{
    public const string Root = "Root";
    public const string Hips = "Hips";
    public const string Spine = "Spine";
    public const string Chest = "Chest";
    public const string Neck = "Neck";
    public const string Head = "Head";
    public const string LeftUpperArm = "LeftUpperArm";
    public const string LeftLowerArm = "LeftLowerArm";
    public const string LeftHand = "LeftHand";
    public const string RightUpperArm = "RightUpperArm";
    public const string RightLowerArm = "RightLowerArm";
    public const string RightHand = "RightHand";
    public const string LeftUpperLeg = "LeftUpperLeg";
    public const string LeftLowerLeg = "LeftLowerLeg";
    public const string LeftFoot = "LeftFoot";
    public const string RightUpperLeg = "RightUpperLeg";
    public const string RightLowerLeg = "RightLowerLeg";
    public const string RightFoot = "RightFoot";

    public static IReadOnlyList<HumanoidBoneDefinition> Bones { get; } =
    [
        new(Root, null, Vector3.Zero),
        new(Hips, Root, new Vector3(0, 0.88f, 0)),
        new(Spine, Hips, new Vector3(0, 0.28f, 0)),
        new(Chest, Spine, new Vector3(0, 0.36f, 0)),
        new(Neck, Chest, new Vector3(0, 0.3f, 0)),
        new(Head, Neck, new Vector3(0, 0.18f, 0)),
        new(LeftUpperArm, Chest, new Vector3(-0.48f, 0.16f, 0)),
        new(LeftLowerArm, LeftUpperArm, new Vector3(0, -0.46f, 0)),
        new(LeftHand, LeftLowerArm, new Vector3(0, -0.4f, 0)),
        new(RightUpperArm, Chest, new Vector3(0.48f, 0.16f, 0)),
        new(RightLowerArm, RightUpperArm, new Vector3(0, -0.46f, 0)),
        new(RightHand, RightLowerArm, new Vector3(0, -0.4f, 0)),
        new(LeftUpperLeg, Hips, new Vector3(-0.21f, -0.06f, 0)),
        new(LeftLowerLeg, LeftUpperLeg, new Vector3(0, -0.5f, 0)),
        new(LeftFoot, LeftLowerLeg, new Vector3(0, -0.44f, -0.05f)),
        new(RightUpperLeg, Hips, new Vector3(0.21f, -0.06f, 0)),
        new(RightLowerLeg, RightUpperLeg, new Vector3(0, -0.5f, 0)),
        new(RightFoot, RightLowerLeg, new Vector3(0, -0.44f, -0.05f))
    ];
}
