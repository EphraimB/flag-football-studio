using System.Collections.Generic;

namespace FlagFootballStudio.Presentation;

/// <summary>
/// Runtime names for the first production-quality modular human. The source GLB may use a
/// different authoring rig only when an explicit retarget profile maps every required role.
/// </summary>
public static class ImportedCharacterAssetContract
{
    public const string DefaultAssetPath = "res://assets/characters/imported/base_player.glb";
    public const string RenderSkeleton = "RenderSkeleton";

    public static IReadOnlyList<string> RequiredModules { get; } =
    [
        "Body", "Head", "Eyes", "Teeth", "Jersey", "Shorts", "Socks", "Shoes",
        "FlagBelt", "Flags", "HeadVisuals"
    ];

    public static IReadOnlyList<string> RequiredAnchors { get; } =
    [
        "EyeAnchor", "MouthAudioAnchor", "LeftHandAnchor", "RightHandAnchor",
        "CatchAnchor", "QuarterbackHoldAnchor", "ThrowAnchor", "CarryAnchor",
        "LeftSoleAnchor", "RightSoleAnchor", "HairMount", "AccessoryMount"
    ];

    public static IReadOnlyList<string> RequiredRenderBones { get; } =
    [
        "Root", "Pelvis", "Spine01", "Spine02", "Chest", "Neck", "Head",
        "LeftClavicle", "LeftUpperArm", "LeftForearm", "LeftHand",
        "RightClavicle", "RightUpperArm", "RightForearm", "RightHand",
        "LeftThigh", "LeftCalf", "LeftFoot", "LeftToe",
        "RightThigh", "RightCalf", "RightFoot", "RightToe",
        "LeftThumb01", "LeftThumb02", "LeftThumb03",
        "LeftIndex01", "LeftIndex02", "LeftIndex03",
        "LeftMiddle01", "LeftMiddle02", "LeftMiddle03",
        "LeftRing01", "LeftRing02", "LeftRing03",
        "LeftPinky01", "LeftPinky02", "LeftPinky03",
        "RightThumb01", "RightThumb02", "RightThumb03",
        "RightIndex01", "RightIndex02", "RightIndex03",
        "RightMiddle01", "RightMiddle02", "RightMiddle03",
        "RightRing01", "RightRing02", "RightRing03",
        "RightPinky01", "RightPinky02", "RightPinky03"
    ];

    public static IReadOnlyList<string> RecommendedDeformationBones { get; } =
    [
        "LeftForearmTwist", "RightForearmTwist", "LeftThighTwist", "RightThighTwist",
        "LeftCalfTwist", "RightCalfTwist", "Jaw"
    ];

    public static IReadOnlyList<string> RequiredBlendShapes { get; } =
    [
        "identity_head_width", "identity_head_height", "identity_jaw_width",
        "identity_jaw_height", "identity_chin_width", "identity_chin_projection",
        "identity_cheekbone_width", "identity_cheek_fullness", "identity_forehead_height",
        "identity_eye_spacing", "identity_eye_size", "identity_eye_vertical",
        "identity_brow_height", "identity_nose_width", "identity_nose_length",
        "identity_nose_projection", "identity_mouth_width", "identity_lip_fullness",
        "identity_ear_size", "identity_ear_position",
        "expression_smile", "expression_focused", "expression_concerned",
        "expression_surprised", "expression_frustrated", "blink_left", "blink_right",
        "viseme_a", "viseme_e", "viseme_i", "viseme_o", "viseme_u", "viseme_mbp",
        "viseme_fv", "viseme_l", "viseme_wq", "jaw_open"
    ];

    public static IReadOnlyDictionary<string, string> LegacyControlToRenderBone { get; } =
        new Dictionary<string, string>
        {
            [HumanoidSkeletonDefinition.Root] = "Root",
            [HumanoidSkeletonDefinition.Hips] = "Pelvis",
            [HumanoidSkeletonDefinition.Spine] = "Spine01",
            [HumanoidSkeletonDefinition.Chest] = "Chest",
            [HumanoidSkeletonDefinition.Neck] = "Neck",
            [HumanoidSkeletonDefinition.Head] = "Head",
            [HumanoidSkeletonDefinition.LeftUpperArm] = "LeftUpperArm",
            [HumanoidSkeletonDefinition.LeftLowerArm] = "LeftForearm",
            [HumanoidSkeletonDefinition.LeftHand] = "LeftHand",
            [HumanoidSkeletonDefinition.RightUpperArm] = "RightUpperArm",
            [HumanoidSkeletonDefinition.RightLowerArm] = "RightForearm",
            [HumanoidSkeletonDefinition.RightHand] = "RightHand",
            [HumanoidSkeletonDefinition.LeftUpperLeg] = "LeftThigh",
            [HumanoidSkeletonDefinition.LeftLowerLeg] = "LeftCalf",
            [HumanoidSkeletonDefinition.LeftFoot] = "LeftFoot",
            [HumanoidSkeletonDefinition.RightUpperLeg] = "RightThigh",
            [HumanoidSkeletonDefinition.RightLowerLeg] = "RightCalf",
            [HumanoidSkeletonDefinition.RightFoot] = "RightFoot"
        };
}
