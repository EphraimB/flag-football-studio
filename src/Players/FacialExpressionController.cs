using System;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FacialExpressionController : Node
{
    public const float DefaultBlendSeconds = 0.24f;
    public const float DefaultBlinkSeconds = 0.18f;
    public const float DefaultAutomaticBlinkInterval = 3.2f;

    private FacialExpressionPose _blendFrom;
    private FacialExpressionPose _blendTarget;
    private FacialExpressionPose _basePose;
    private FacialExpressionPose _currentPose;
    private float _blendElapsed = DefaultBlendSeconds;
    private float _blendDuration = DefaultBlendSeconds;
    private float _browRaiseOffset;
    private float _browTiltOffset;
    private float _blinkElapsed = -1;
    private float _blinkDuration = DefaultBlinkSeconds;
    private float _automaticBlinkElapsed;
    private float _automaticBlinkInterval = DefaultAutomaticBlinkInterval;
    private float _blinkAmount;

    public event Action<FacialExpressionPose>? PoseChanged;
    public event Action<float>? BlinkChanged;

    public FacialExpressionState Expression { get; private set; } = FacialExpressionState.Neutral;
    public FacialExpressionPose CurrentPose => _currentPose;
    public float BlinkAmount => _blinkAmount;
    public int BlinkCount { get; private set; }
    public bool AutomaticBlinkEnabled { get; private set; }

    public override void _Ready()
    {
        _basePose = FacialExpressionPose.For(Expression);
        _blendTarget = _basePose;
        ApplyPose(_basePose);
    }

    public void SetExpression(FacialExpressionState expression, float blendSeconds = DefaultBlendSeconds)
    {
        Expression = expression;
        _blendFrom = _basePose;
        _blendTarget = FacialExpressionPose.For(expression);
        _blendDuration = Mathf.Max(0, blendSeconds);
        _blendElapsed = 0;
        if (_blendDuration == 0)
        {
            _basePose = _blendTarget;
            ApplyPose(_basePose);
        }
    }

    public void SetEyebrowControl(float raise, float tilt)
    {
        _browRaiseOffset = Mathf.Clamp(raise, -1, 1);
        _browTiltOffset = Mathf.Clamp(tilt, -1, 1);
        ApplyPose(_basePose);
    }

    public void TriggerBlink(float durationSeconds = DefaultBlinkSeconds)
    {
        _blinkDuration = Mathf.Max(0.08f, durationSeconds);
        _blinkElapsed = 0;
        BlinkCount++;
    }

    public void SetAutomaticBlink(bool enabled, float intervalSeconds = DefaultAutomaticBlinkInterval)
    {
        AutomaticBlinkEnabled = enabled;
        _automaticBlinkInterval = Mathf.Max(0.2f, intervalSeconds);
        _automaticBlinkElapsed = 0;
    }

    public override void _Process(double delta)
    {
        var frameSeconds = (float)delta;
        UpdateExpression(frameSeconds);
        UpdateBlink(frameSeconds);
    }

    private void UpdateExpression(float delta)
    {
        if (_blendElapsed >= _blendDuration)
            return;

        _blendElapsed = Mathf.Min(_blendElapsed + delta, _blendDuration);
        var weight = _blendDuration <= 0
            ? 1
            : Mathf.SmoothStep(0, 1, _blendElapsed / _blendDuration);
        _basePose = FacialExpressionPose.Lerp(_blendFrom, _blendTarget, weight);
        ApplyPose(_basePose);
    }

    private void UpdateBlink(float delta)
    {
        if (AutomaticBlinkEnabled)
        {
            _automaticBlinkElapsed += delta;
            if (_automaticBlinkElapsed >= _automaticBlinkInterval && _blinkElapsed < 0)
            {
                _automaticBlinkElapsed -= _automaticBlinkInterval;
                TriggerBlink();
            }
        }

        var amount = 0f;
        if (_blinkElapsed >= 0)
        {
            _blinkElapsed += delta;
            var progress = Mathf.Clamp(_blinkElapsed / _blinkDuration, 0, 1);
            amount = Mathf.Sin(progress * Mathf.Pi);
            if (progress >= 1)
                _blinkElapsed = -1;
        }

        if (!Mathf.IsEqualApprox(amount, _blinkAmount))
        {
            _blinkAmount = amount;
            BlinkChanged?.Invoke(_blinkAmount);
        }
    }

    private void ApplyPose(FacialExpressionPose pose)
    {
        var effective = pose.WithBrowOffsets(_browRaiseOffset, _browTiltOffset);
        if (_currentPose.IsEqualApprox(effective))
            return;
        _currentPose = effective;
        PoseChanged?.Invoke(_currentPose);
    }
}
