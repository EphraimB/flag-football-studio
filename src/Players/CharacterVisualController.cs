using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

/// <summary>
/// Stable presentation adapter between player data/controllers and the concrete character visual.
/// The legacy rig remains the only activatable implementation until a contract-complete GLB exists.
/// </summary>
public partial class CharacterVisualController : Node3D
{
    private static readonly HashSet<string> ReportedFallbacks = [];
    private CharacterVisualRequest _request = CharacterVisualRequest.Default;
    private HumanoidRig _legacy = null!;

    public CharacterVisualBackend RequestedBackend => Status.RequestedBackend;
    public CharacterVisualBackend ActiveBackend => Status.ActiveBackend;
    public CharacterVisualStatus Status { get; private set; } = new(
        CharacterVisualBackend.ProceduralLegacy,
        CharacterVisualBackend.ProceduralLegacy,
        ImportedCharacterAssetContract.DefaultAssetPath,
        false,
        "Procedural legacy visual selected.");
    public ImportedCharacterAssetReport? ImportedAssetReport { get; private set; }

    public Node3D EyeAnchor => _legacy.EyeAnchor;
    public Node3D CatchAnchor => _legacy.CatchAnchor;
    public Node3D QuarterbackHoldAnchor => _legacy.QuarterbackHoldAnchor;
    public Node3D ThrowAnchor => _legacy.ThrowAnchor;
    public Node3D CarryAnchor => _legacy.CarryAnchor;
    public Node3D LeftHandAnchor => _legacy.LeftHandAnchor;
    public Node3D RightHandAnchor => _legacy.RightHandAnchor;
    public Node3D LeftSoleAnchor => _legacy.LeftSoleAnchor;
    public Node3D RightSoleAnchor => _legacy.RightSoleAnchor;
    public Node3D MouthAudioAnchor => _legacy.MouthAudioAnchor;
    public Node3D HeadAnchor => _legacy.HeadAnchor;
    public Node3D ChestAnchor => _legacy.ChestAnchor;
    public Node3D ShoulderAnchor => _legacy.ShoulderAnchor;
    public HumanoidAnimationState AnimationState => _legacy.AnimationState;
    public float AnimationMovementSpeed => _legacy.AnimationMovementSpeed;
    public float AnimationStrideFrequency => _legacy.AnimationStrideFrequency;
    public float AnimationBodyLean => _legacy.AnimationBodyLean;
    public float AnimationTurnDegrees => _legacy.AnimationTurnDegrees;
    public float AnimationBlendProgress => _legacy.AnimationBlendProgress;
    public float LeftFootIkWeight => _legacy.LeftFootIkWeight;
    public float RightFootIkWeight => _legacy.RightFootIkWeight;
    public bool LeftFootLocked => _legacy.LeftFootLocked;
    public bool RightFootLocked => _legacy.RightFootLocked;
    public float LeftFootContactError => _legacy.LeftFootContactError;
    public float RightFootContactError => _legacy.RightFootContactError;
    public float LeftFootLockDrift => _legacy.LeftFootLockDrift;
    public float RightFootLockDrift => _legacy.RightFootLockDrift;
    public FootballInteractionMode FootballInteractionMode => _legacy.FootballInteractionMode;
    public float ContactRootHeightOffset => _legacy.ContactRootHeightOffset;
    public FacialExpressionState FacialExpression => _legacy.FacialExpression;
    public FacialExpressionPose FacialPose => _legacy.FacialPose;
    public float BlinkAmount => _legacy.BlinkAmount;
    public int BlinkCount => _legacy.BlinkCount;
    public float HorizontalGazeDegrees => _legacy.HorizontalGazeDegrees;
    public float VerticalGazeDegrees => _legacy.VerticalGazeDegrees;
    public float TargetHorizontalGazeDegrees => _legacy.TargetHorizontalGazeDegrees;
    public float TargetVerticalGazeDegrees => _legacy.TargetVerticalGazeDegrees;
    public bool HasSceneGazeTarget => _legacy.HasSceneGazeTarget;
    public SpeechMouthShape MouthShape => _legacy.MouthShape;
    public SpeechMouthPose MouthPose => _legacy.MouthPose;
    public bool IsSpeechShapeCycling => _legacy.IsSpeechShapeCycling;
    public bool FirstPersonViewActive => _legacy.FirstPersonViewActive;
    public Aabb BodyBounds => _legacy.BodyBounds;

    public void Configure(CharacterVisualRequest request)
    {
        if (IsNodeReady()) throw new InvalidOperationException("Configure the character visual before adding it to the scene tree.");
        _request = request;
    }

    public override void _Ready()
    {
        if (_request == CharacterVisualRequest.Default)
            _request = CharacterVisualRequest.FromCommandLine();

        if (_request.Backend == CharacterVisualBackend.ImportedModular)
        {
            ImportedAssetReport = ImportedCharacterAssetValidator.Validate(_request.ImportedAssetPath);
            var reason = ImportedAssetReport.IsCompatible
                ? "Asset contract passed, but the render-skeleton retarget binder remains intentionally gated until a real asset integration milestone."
                : ImportedAssetReport.Summary;
            Status = new CharacterVisualStatus(_request.Backend, CharacterVisualBackend.ProceduralLegacy,
                _request.ImportedAssetPath, true, reason);
            if (ReportedFallbacks.Add(reason)) GD.Print($"Character visual fallback: {reason}");
        }
        else
        {
            Status = new CharacterVisualStatus(_request.Backend, CharacterVisualBackend.ProceduralLegacy,
                _request.ImportedAssetPath, false, "Procedural legacy visual selected.");
        }

        _legacy = new HumanoidRig { Name = "HumanoidRig" };
        AddChild(_legacy);
    }

    public void Apply(Player player, PlayerAppearance appearance, UniformDefinition uniform) =>
        _legacy.Apply(player, appearance, uniform);
    public void SetAnimationState(HumanoidAnimationState state, bool restart = false) =>
        _legacy.SetAnimationState(state, restart);
    public void ApplyAnimationCue(HumanoidAnimationCue cue, bool restart = false) =>
        _legacy.ApplyAnimationCue(cue, restart);
    public void SetFootballInteractionMode(FootballInteractionMode mode) => _legacy.SetFootballInteractionMode(mode);
    public void SetFacialExpression(FacialExpressionState expression, float blendSeconds = FacialExpressionController.DefaultBlendSeconds) =>
        _legacy.SetFacialExpression(expression, blendSeconds);
    public void SetEyebrowControl(float raise, float tilt) => _legacy.SetEyebrowControl(raise, tilt);
    public void TriggerBlink(float durationSeconds = FacialExpressionController.DefaultBlinkSeconds) => _legacy.TriggerBlink(durationSeconds);
    public void SetAutomaticBlink(bool enabled, float intervalSeconds = FacialExpressionController.DefaultAutomaticBlinkInterval) =>
        _legacy.SetAutomaticBlink(enabled, intervalSeconds);
    public void SetManualGaze(float horizontal, float vertical) => _legacy.SetManualGaze(horizontal, vertical);
    public void LookAtGazeTarget(Node3D target) => _legacy.LookAtGazeTarget(target);
    public void LookAtWorldPoint(Vector3 worldPoint) => _legacy.LookAtWorldPoint(worldPoint);
    public void ClearGazeTarget() => _legacy.ClearGazeTarget();
    public void SetMouthShape(SpeechMouthShape shape, float blendSeconds = SpeechMouthController.DefaultBlendSeconds) =>
        _legacy.SetMouthShape(shape, blendSeconds);
    public void SetMouthShapeWeighted(SpeechMouthShape shape, float strength, float blendSeconds = SpeechMouthController.DefaultBlendSeconds) =>
        _legacy.SetMouthShapeWeighted(shape, strength, blendSeconds);
    public void SetMouthControls(float jawOpen, float width, float lipFullness, float upperLip, float lowerLip) =>
        _legacy.SetMouthControls(jawOpen, width, lipFullness, upperLip, lowerLip);
    public void StartSpeechShapeCycle(float holdSeconds = SpeechMouthController.DefaultCycleHoldSeconds) =>
        _legacy.StartSpeechShapeCycle(holdSeconds);
    public void SetFirstPersonView(bool active) => _legacy.SetFirstPersonView(active);
    public bool HeadGeometryVisibleTo(Camera3D camera) => _legacy.HeadGeometryVisibleTo(camera);
    public bool BodyGeometryVisibleTo(Camera3D camera) => _legacy.BodyGeometryVisibleTo(camera);
}
