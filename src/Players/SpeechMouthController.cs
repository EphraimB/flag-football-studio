using System;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class SpeechMouthController : Node
{
    public const float DefaultBlendSeconds = 0.14f;
    public const float DefaultCycleHoldSeconds = 0.24f;

    private static readonly SpeechMouthShape[] CycleShapes = Enum.GetValues<SpeechMouthShape>();
    private SpeechMouthPose _blendFrom = SpeechMouthPose.Rest;
    private SpeechMouthPose _blendTarget = SpeechMouthPose.Rest;
    private SpeechMouthPose _basePose = SpeechMouthPose.Rest;
    private SpeechMouthPose _currentPose = SpeechMouthPose.Rest;
    private float _blendElapsed = DefaultBlendSeconds;
    private float _blendDuration = DefaultBlendSeconds;
    private float _manualJawOpen;
    private float _manualWidth = 1;
    private float _manualLipFullness = 1;
    private float _manualUpperLip;
    private float _manualLowerLip;
    private float _cycleElapsed;
    private float _cycleHoldSeconds = DefaultCycleHoldSeconds;
    private int _cycleIndex;

    public event Action<SpeechMouthPose>? PoseChanged;

    public SpeechMouthShape Shape { get; private set; } = SpeechMouthShape.Rest;
    public SpeechMouthPose CurrentPose => _currentPose;
    public bool IsCycling { get; private set; }
    public int CompletedCycleCount { get; private set; }

    public void SetShape(SpeechMouthShape shape, float blendSeconds = DefaultBlendSeconds)
    {
        IsCycling = false;
        SetShapeInternal(shape, blendSeconds, 1);
    }

    public void SetWeightedShape(SpeechMouthShape shape, float strength, float blendSeconds = DefaultBlendSeconds)
    {
        IsCycling = false;
        SetShapeInternal(shape, blendSeconds, Mathf.Clamp(strength, 0, 1));
    }

    public void SetManualControls(float jawOpen, float width, float lipFullness, float upperLip, float lowerLip)
    {
        _manualJawOpen = Mathf.Clamp(jawOpen, 0, 1);
        _manualWidth = Mathf.Clamp(width, 0.75f, 1.25f);
        _manualLipFullness = Mathf.Clamp(lipFullness, 0.75f, 1.25f);
        _manualUpperLip = Mathf.Clamp(upperLip, -1, 1);
        _manualLowerLip = Mathf.Clamp(lowerLip, -1, 1);
        ApplyPose(_basePose);
    }

    public void StartCycle(float holdSeconds = DefaultCycleHoldSeconds, float blendSeconds = DefaultBlendSeconds)
    {
        _cycleHoldSeconds = Mathf.Max(0.12f, holdSeconds);
        _cycleIndex = 0;
        _cycleElapsed = 0;
        IsCycling = true;
        SetShapeInternal(CycleShapes[_cycleIndex], blendSeconds, 1);
    }

    public override void _Process(double delta)
    {
        var frameSeconds = (float)delta;
        UpdateBlend(frameSeconds);
        UpdateCycle(frameSeconds);
    }

    private void SetShapeInternal(SpeechMouthShape shape, float blendSeconds, float strength)
    {
        if (!Enum.IsDefined(shape))
            throw new ArgumentOutOfRangeException(nameof(shape));
        Shape = shape;
        _blendFrom = _basePose;
        _blendTarget = SpeechMouthPose.Lerp(SpeechMouthPose.Rest, SpeechMouthPose.For(shape), strength);
        _blendDuration = Mathf.Max(0, blendSeconds);
        _blendElapsed = 0;
        if (_blendDuration == 0)
        {
            _basePose = _blendTarget;
            ApplyPose(_basePose);
        }
    }

    private void UpdateBlend(float delta)
    {
        if (_blendElapsed >= _blendDuration)
            return;
        _blendElapsed = Mathf.Min(_blendElapsed + delta, _blendDuration);
        var weight = _blendDuration <= 0 ? 1 : Mathf.SmoothStep(0, 1, _blendElapsed / _blendDuration);
        _basePose = SpeechMouthPose.Lerp(_blendFrom, _blendTarget, weight);
        ApplyPose(_basePose);
    }

    private void UpdateCycle(float delta)
    {
        if (!IsCycling)
            return;
        _cycleElapsed += delta;
        if (_cycleElapsed < _cycleHoldSeconds)
            return;
        _cycleElapsed -= _cycleHoldSeconds;
        _cycleIndex++;
        if (_cycleIndex >= CycleShapes.Length)
        {
            IsCycling = false;
            CompletedCycleCount++;
            SetShapeInternal(SpeechMouthShape.Rest, DefaultBlendSeconds, 1);
            return;
        }
        SetShapeInternal(CycleShapes[_cycleIndex], DefaultBlendSeconds, 1);
    }

    private void ApplyPose(SpeechMouthPose pose)
    {
        var effective = pose.WithManualControls(
            _manualJawOpen,
            _manualWidth,
            _manualLipFullness,
            _manualUpperLip,
            _manualLowerLip);
        if (_currentPose.IsEqualApprox(effective))
            return;
        _currentPose = effective;
        PoseChanged?.Invoke(_currentPose);
    }
}
