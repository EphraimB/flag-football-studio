using System;
using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidAnimator : Node
{
    private const float BlendDuration = 0.2f;
    private readonly Dictionary<string, int> _bones = [];
    private readonly Dictionary<string, Vector3> _blendFrom = [];
    private readonly Dictionary<string, Vector3> _lastPose = [];
    private Skeleton3D _skeleton = null!;
    private float _stateElapsed;
    private float _blendElapsed = BlendDuration;
    private HumanoidAnimationCue _targetCue = HumanoidAnimationCue.ForState(HumanoidAnimationState.Idle);
    private float _movementSpeed;
    private float _normalizedSpeed;
    private float _strideFrequency;
    private float _bodyLean;
    private float _turnDegrees;
    private float _turnDirection;
    private float _headMotionScale = 0.2f;

    public HumanoidAnimationState State { get; private set; } = HumanoidAnimationState.Idle;
    public float MovementSpeed => _movementSpeed;
    public float StrideFrequency => _strideFrequency;
    public float BodyLean => _bodyLean;
    public float TurnDegrees => _turnDegrees;
    public float BlendProgress => Math.Clamp(_blendElapsed / BlendDuration, 0, 1);

    public void Configure(Skeleton3D skeleton)
    {
        _skeleton = skeleton;
        _bones.Clear();
        for (var index = 0; index < skeleton.GetBoneCount(); index++)
            _bones[skeleton.GetBoneName(index)] = index;
        SetProcess(true);
        ApplyPose(SamplePose(State, 0));
    }

    public void SetState(HumanoidAnimationState state, bool restart = false) =>
        ApplyCue(HumanoidAnimationCue.ForState(state), restart);

    public void ApplyCue(HumanoidAnimationCue cue, bool restart = false)
    {
        _targetCue = cue;
        if (State == cue.State && !restart)
            return;

        _blendFrom.Clear();
        foreach (var pose in _lastPose)
            _blendFrom[pose.Key] = pose.Value;
        State = cue.State;
        _stateElapsed = 0;
        _blendElapsed = 0;
    }

    public override void _Process(double delta)
    {
        if (_skeleton is null)
            return;

        _stateElapsed += (float)delta;
        _blendElapsed += (float)delta;
        var parameterWeight = 1f - MathF.Exp(-10f * (float)delta);
        _movementSpeed = Mathf.Lerp(_movementSpeed, _targetCue.MovementSpeed, parameterWeight);
        _normalizedSpeed = Mathf.Lerp(_normalizedSpeed, _targetCue.NormalizedSpeed, parameterWeight);
        _strideFrequency = Mathf.Lerp(_strideFrequency, _targetCue.StrideFrequency, parameterWeight);
        _bodyLean = Mathf.Lerp(_bodyLean, _targetCue.BodyLean, parameterWeight);
        _turnDegrees = Mathf.Lerp(_turnDegrees, _targetCue.TurnDegrees, parameterWeight);
        _turnDirection = Mathf.Lerp(_turnDirection, _targetCue.TurnDirection, parameterWeight);
        _headMotionScale = Mathf.Lerp(_headMotionScale, _targetCue.HeadMotionScale, parameterWeight);
        var target = SamplePose(State, _stateElapsed);
        if (_blendElapsed < BlendDuration)
        {
            var weight = Mathf.SmoothStep(0, 1, _blendElapsed / BlendDuration);
            foreach (var bone in HumanoidSkeletonDefinition.Bones)
            {
                var from = _blendFrom.GetValueOrDefault(bone.Name, Vector3.Zero);
                var to = target.GetValueOrDefault(bone.Name, Vector3.Zero);
                target[bone.Name] = LerpAngles(from, to, weight);
            }
        }
        ApplyPose(target);
    }

    private void ApplyPose(Dictionary<string, Vector3> pose)
    {
        _lastPose.Clear();
        foreach (var bone in HumanoidSkeletonDefinition.Bones)
        {
            var euler = pose.GetValueOrDefault(bone.Name, Vector3.Zero);
            _lastPose[bone.Name] = euler;
            _skeleton.SetBonePoseRotation(_bones[bone.Name], Quaternion.FromEuler(euler));
        }
    }

    private Dictionary<string, Vector3> SamplePose(HumanoidAnimationState state, float time)
    {
        var pose = new Dictionary<string, Vector3>();
        switch (state)
        {
            case HumanoidAnimationState.Idle:
                AddIdlePose(pose, time);
                break;
            case HumanoidAnimationState.PreSnapReady:
                AddReadyPose(pose, time);
                break;
            case HumanoidAnimationState.Acceleration:
                AddLocomotionPose(pose, time, 0.72f + _normalizedSpeed * 0.28f, _bodyLean + 0.05f, true);
                break;
            case HumanoidAnimationState.Jog:
                AddLocomotionPose(pose, time, 0.48f + _normalizedSpeed * 0.22f, _bodyLean, false);
                break;
            case HumanoidAnimationState.Sprint:
            case HumanoidAnimationState.PostCatchRun:
                AddLocomotionPose(pose, time, 0.75f + _normalizedSpeed * 0.18f, _bodyLean, true,
                    state == HumanoidAnimationState.PostCatchRun);
                break;
            case HumanoidAnimationState.Deceleration:
                AddDecelerationPose(pose, time);
                break;
            case HumanoidAnimationState.RouteCut:
                AddRouteCutPose(pose, time, false);
                break;
            case HumanoidAnimationState.CurvedTurn:
                AddRouteCutPose(pose, time, true);
                break;
            case HumanoidAnimationState.QuarterbackDropback:
                AddQuarterbackDropbackPose(pose, time);
                break;
            case HumanoidAnimationState.QuarterbackSet:
                AddQuarterbackSetPose(pose, time);
                break;
            case HumanoidAnimationState.Throw:
                AddThrowPose(pose, Mathf.Clamp(time / 0.62f, 0, 1));
                break;
            case HumanoidAnimationState.CatchPrepare:
                AddCatchPose(pose, Mathf.Clamp(time / 0.38f, 0, 0.68f), false);
                break;
            case HumanoidAnimationState.Catch:
                AddCatchPose(pose, Mathf.Clamp(time / 0.48f, 0, 1), false);
                break;
            case HumanoidAnimationState.DroppedCatch:
                AddCatchPose(pose, Mathf.Clamp(time / 0.5f, 0, 1), true);
                break;
            case HumanoidAnimationState.InterceptionCatch:
                AddInterceptionCatchPose(pose, Mathf.Clamp(time / 0.52f, 0, 1));
                break;
            case HumanoidAnimationState.FlagPull:
                AddFlagPullPose(pose, Mathf.Clamp(time / 0.55f, 0, 1));
                break;
            case HumanoidAnimationState.TouchdownCelebration:
                AddCelebrationPose(pose, Mathf.Clamp(time / 0.75f, 0, 1));
                break;
        }
        return pose;
    }

    private void AddIdlePose(Dictionary<string, Vector3> pose, float time)
    {
        var breath = Mathf.Sin(time * 2.2f);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(breath * 0.018f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(0, 0, -0.045f - breath * 0.012f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(0, 0, 0.045f + breath * 0.012f);
    }

    private static void AddReadyPose(Dictionary<string, Vector3> pose, float time)
    {
        var shift = Mathf.Sin(time * 2.7f) * 0.018f;
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0.11f, shift, 0);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-0.14f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(-0.12f, 0, -0.035f);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-0.12f, 0, 0.035f);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(0.24f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(0.24f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-0.25f, 0, -0.12f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-0.25f, 0, 0.12f);
    }

    private void AddLocomotionPose(
        Dictionary<string, Vector3> pose, float time, float stride, float lean,
        bool sprint, bool protectBall = false)
    {
        var cadence = MathF.Max(0.5f, _strideFrequency) * Mathf.Tau;
        var swing = Mathf.Sin(time * cadence) * stride;
        var vertical = Mathf.Abs(Mathf.Sin(time * cadence)) * 0.025f * _headMotionScale;
        var armScale = sprint ? 1.0f : 0.82f;
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(vertical, 0, 0);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-lean, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(swing, 0, 0);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-swing, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(Mathf.Max(0, -swing) * (sprint ? 1.05f : 0.82f), 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(Mathf.Max(0, swing) * (sprint ? 1.05f : 0.82f), 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-swing * armScale, 0, -0.06f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = protectBall
            ? new Vector3(-0.72f, -0.1f, 0.2f)
            : new Vector3(swing * armScale, 0, 0.06f);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.28f - Mathf.Max(0, swing) * 0.25f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = protectBall
            ? new Vector3(-1.05f, 0, 0)
            : new Vector3(-0.28f - Mathf.Max(0, -swing) * 0.25f, 0, 0);
    }

    private void AddDecelerationPose(Dictionary<string, Vector3> pose, float time)
    {
        AddLocomotionPose(pose, time, 0.42f, _bodyLean, false);
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(-0.08f, 0, 0);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(0.1f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-0.25f, 0, -0.38f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-0.25f, 0, 0.38f);
    }

    private void AddRouteCutPose(Dictionary<string, Vector3> pose, float time, bool curved)
    {
        var plant = Mathf.Sin(Mathf.Clamp(time / (curved ? 0.5f : 0.34f), 0, 1) * Mathf.Pi);
        var side = _turnDirection == 0 ? 1 : _turnDirection;
        var severity = curved ? Math.Clamp(_turnDegrees / 18f, 0.25f, 0.65f) : Math.Clamp(_turnDegrees / 35f, 0.55f, 1);
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0.12f * plant, side * 0.22f * severity, -side * 0.12f * severity);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-0.12f, side * 0.16f * severity, side * 0.16f * severity);
        pose[HumanoidSkeletonDefinition.Chest] = new Vector3(-0.08f, side * 0.28f * severity, side * 0.12f * severity);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(-0.22f, 0, side > 0 ? -0.18f : 0.04f);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-0.22f, 0, side < 0 ? 0.18f : -0.04f);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(0.5f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(0.5f, 0, 0);
        pose[HumanoidSkeletonDefinition.Head] = new Vector3(0, -side * 0.08f * severity, 0);
    }

    private void AddQuarterbackDropbackPose(Dictionary<string, Vector3> pose, float time)
    {
        var cadence = MathF.Max(1.5f, _strideFrequency) * Mathf.Tau;
        var step = Mathf.Sin(time * cadence) * 0.34f;
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(-0.08f, 0, 0);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(0.06f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(step, 0, -0.08f);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-step, 0, 0.08f);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(Mathf.Max(0, -step) * 0.8f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(Mathf.Max(0, step) * 0.8f, 0, 0);
        AddQuarterbackHands(pose, 0.78f);
    }

    private static void AddQuarterbackSetPose(Dictionary<string, Vector3> pose, float time)
    {
        var bounce = Mathf.Sin(time * 5.2f) * 0.025f;
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0.08f + bounce, -0.05f, 0);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-0.05f, 0.08f, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(-0.12f, 0, -0.05f);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-0.12f, 0, 0.05f);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(0.24f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(0.24f, 0, 0);
        AddQuarterbackHands(pose, 1);
    }

    private static void AddQuarterbackHands(Dictionary<string, Vector3> pose, float amount)
    {
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-0.56f * amount, 0, -0.22f * amount);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-0.56f * amount, 0, 0.22f * amount);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.82f * amount, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.82f * amount, 0, 0);
    }

    private static void AddThrowPose(Dictionary<string, Vector3> pose, float progress)
    {
        var windup = Mathf.Sin(progress * Mathf.Pi);
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0, -windup * 0.3f, 0);
        pose[HumanoidSkeletonDefinition.Chest] = new Vector3(-0.08f, windup * 0.5f, 0);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-1.45f * windup, -0.28f * windup, 0.18f);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.85f * (1 - progress), 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-0.4f * windup, 0, -0.25f);
    }

    private static void AddCatchPose(Dictionary<string, Vector3> pose, float progress, bool dropped)
    {
        var reach = Mathf.Sin(Mathf.Min(progress, 0.75f) / 0.75f * Mathf.Pi * 0.5f);
        var fail = dropped ? Mathf.SmoothStep(0, 1, (progress - 0.55f) / 0.45f) : 0;
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-0.12f * reach, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-1.05f * reach + fail * 0.5f, 0, -0.3f * reach - fail * 0.2f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-1.05f * reach + fail * 0.35f, 0, 0.3f * reach + fail * 0.32f);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.35f * reach, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.35f * reach, 0, 0);
    }

    private static void AddInterceptionCatchPose(Dictionary<string, Vector3> pose, float progress)
    {
        AddCatchPose(pose, progress, false);
        var secure = Mathf.SmoothStep(0, 1, (progress - 0.45f) / 0.55f);
        pose[HumanoidSkeletonDefinition.Chest] = new Vector3(-0.14f, -0.24f * secure, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] += new Vector3(0.28f * secure, 0, 0.12f * secure);
        pose[HumanoidSkeletonDefinition.RightUpperArm] += new Vector3(0.28f * secure, 0, -0.12f * secure);
    }

    private static void AddFlagPullPose(Dictionary<string, Vector3> pose, float progress)
    {
        var reach = Mathf.Sin(progress * Mathf.Pi);
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0.18f * reach, 0.2f * reach, 0);
        pose[HumanoidSkeletonDefinition.Chest] = new Vector3(-0.3f * reach, 0.38f * reach, -0.08f);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-1.15f * reach, 0, -0.45f * reach);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-1.15f * reach, 0, 0.45f * reach);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.35f * reach, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.35f * reach, 0, 0);
    }

    private static void AddCelebrationPose(Dictionary<string, Vector3> pose, float progress)
    {
        var lift = Mathf.SmoothStep(0, 1, progress);
        var bounce = Mathf.Sin(progress * Mathf.Pi * 3) * (1 - progress) * 0.08f;
        pose[HumanoidSkeletonDefinition.Hips] = new Vector3(bounce, 0, 0);
        pose[HumanoidSkeletonDefinition.Chest] = new Vector3(0.08f * lift, 0.16f * lift, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-2.45f * lift, 0, -0.3f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-2.45f * lift, 0, 0.3f);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.2f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.2f, 0, 0);
    }

    private static Vector3 LerpAngles(Vector3 from, Vector3 to, float weight) => new(
        Mathf.LerpAngle(from.X, to.X, weight),
        Mathf.LerpAngle(from.Y, to.Y, weight),
        Mathf.LerpAngle(from.Z, to.Z, weight));
}
