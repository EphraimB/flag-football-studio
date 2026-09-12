using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HumanoidAnimator : Node
{
    private const float BlendDuration = 0.18f;
    private readonly Dictionary<string, int> _bones = [];
    private readonly Dictionary<string, Vector3> _blendFrom = [];
    private readonly Dictionary<string, Vector3> _lastPose = [];
    private Skeleton3D _skeleton = null!;
    private float _stateElapsed;
    private float _blendElapsed = BlendDuration;

    public HumanoidAnimationState State { get; private set; } = HumanoidAnimationState.Idle;

    public void Configure(Skeleton3D skeleton)
    {
        _skeleton = skeleton;
        _bones.Clear();
        for (var index = 0; index < skeleton.GetBoneCount(); index++)
            _bones[skeleton.GetBoneName(index)] = index;
        SetProcess(true);
        ApplyPose(SamplePose(State, 0));
    }

    public void SetState(HumanoidAnimationState state, bool restart = false)
    {
        if (State == state && !restart)
            return;

        _blendFrom.Clear();
        foreach (var pose in _lastPose)
            _blendFrom[pose.Key] = pose.Value;
        State = state;
        _stateElapsed = 0;
        _blendElapsed = 0;
    }

    public override void _Process(double delta)
    {
        if (_skeleton is null)
            return;

        _stateElapsed += (float)delta;
        _blendElapsed += (float)delta;
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
                var breath = Mathf.Sin(time * 2.2f);
                pose[HumanoidSkeletonDefinition.Spine] = new Vector3(breath * 0.018f, 0, 0);
                pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(0, 0, -0.045f - breath * 0.012f);
                pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(0, 0, 0.045f + breath * 0.012f);
                break;
            case HumanoidAnimationState.Jog:
                AddLocomotionPose(pose, time, 7.5f, 0.52f, 0.08f);
                break;
            case HumanoidAnimationState.Sprint:
                AddLocomotionPose(pose, time, 10.5f, 0.82f, 0.2f);
                break;
            case HumanoidAnimationState.Turn:
                var turn = Mathf.Sin(Mathf.Clamp(time / 0.42f, 0, 1) * Mathf.Pi);
                pose[HumanoidSkeletonDefinition.Hips] = new Vector3(0, turn * 0.28f, 0);
                pose[HumanoidSkeletonDefinition.Chest] = new Vector3(0, turn * 0.48f, 0);
                pose[HumanoidSkeletonDefinition.Head] = new Vector3(0, -turn * 0.18f, 0);
                break;
            case HumanoidAnimationState.Throw:
                AddThrowPose(pose, Mathf.Clamp(time / 0.62f, 0, 1));
                break;
            case HumanoidAnimationState.Catch:
                AddCatchPose(pose, Mathf.Clamp(time / 0.48f, 0, 1));
                break;
            case HumanoidAnimationState.FlagPull:
                AddFlagPullPose(pose, Mathf.Clamp(time / 0.55f, 0, 1));
                break;
        }
        return pose;
    }

    private static void AddLocomotionPose(Dictionary<string, Vector3> pose, float time, float cadence, float stride, float lean)
    {
        var swing = Mathf.Sin(time * cadence) * stride;
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-lean, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperLeg] = new Vector3(swing, 0, 0);
        pose[HumanoidSkeletonDefinition.RightUpperLeg] = new Vector3(-swing, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftLowerLeg] = new Vector3(Mathf.Max(0, -swing) * 0.8f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerLeg] = new Vector3(Mathf.Max(0, swing) * 0.8f, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-swing * 0.9f, 0, -0.06f);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(swing * 0.9f, 0, 0.06f);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.28f - Mathf.Max(0, swing) * 0.25f, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.28f - Mathf.Max(0, -swing) * 0.25f, 0, 0);
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

    private static void AddCatchPose(Dictionary<string, Vector3> pose, float progress)
    {
        var reach = Mathf.Sin(Mathf.Min(progress, 0.75f) / 0.75f * Mathf.Pi * 0.5f);
        pose[HumanoidSkeletonDefinition.Spine] = new Vector3(-0.12f * reach, 0, 0);
        pose[HumanoidSkeletonDefinition.LeftUpperArm] = new Vector3(-1.05f * reach, 0, -0.3f * reach);
        pose[HumanoidSkeletonDefinition.RightUpperArm] = new Vector3(-1.05f * reach, 0, 0.3f * reach);
        pose[HumanoidSkeletonDefinition.LeftLowerArm] = new Vector3(-0.35f * reach, 0, 0);
        pose[HumanoidSkeletonDefinition.RightLowerArm] = new Vector3(-0.35f * reach, 0, 0);
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

    private static Vector3 LerpAngles(Vector3 from, Vector3 to, float weight) => new(
        Mathf.LerpAngle(from.X, to.X, weight),
        Mathf.LerpAngle(from.Y, to.Y, weight),
        Mathf.LerpAngle(from.Z, to.Z, weight));
}
