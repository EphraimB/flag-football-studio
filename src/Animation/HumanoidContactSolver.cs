using System;
using Godot;

namespace FlagFootballStudio.Presentation;

public readonly record struct GroundContactSample(Vector3 Position, Vector3 Normal, bool IsValid);

public interface IGroundSurfaceSampler
{
    GroundContactSample Sample(Vector3 worldPosition);
}

public sealed class FlatGroundSurfaceSampler(float height = 0) : IGroundSurfaceSampler
{
    public GroundContactSample Sample(Vector3 worldPosition) =>
        new(new Vector3(worldPosition.X, height, worldPosition.Z), Vector3.Up, true);
}

public enum FootballInteractionMode
{
    None,
    QuarterbackHold,
    ThrowingHand,
    CatchHands,
    Carry
}

public partial class HumanoidContactSolver : Node
{
    private const float SoleOffset = 0.11f;
    private const float MaximumFootCorrection = 0.42f;
    private const float MaximumHandCorrection = 0.82f;
    private Skeleton3D _skeleton = null!;
    private Node3D _rigRoot = null!;
    private Node3D _leftSole = null!;
    private Node3D _rightSole = null!;
    private Node3D _leftHand = null!;
    private Node3D _rightHand = null!;
    private Node3D _quarterbackHoldAnchor = null!;
    private Node3D _throwAnchor = null!;
    private Node3D _catchAnchor = null!;
    private Node3D _carryAnchor = null!;
    private IGroundSurfaceSampler _ground = new FlatGroundSurfaceSampler();
    private HumanoidAnimationCue _cue = HumanoidAnimationCue.ForState(HumanoidAnimationState.Idle);
    private FootballInteractionMode _interactionMode;
    private float _leftWeight;
    private float _rightWeight;
    private float _rootHeightOffset;
    private bool _leftLocked;
    private bool _rightLocked;
    private Vector3 _leftLockPoint;
    private Vector3 _rightLockPoint;
    private float _stateElapsed;
    private HumanoidAnimationState _lastState = HumanoidAnimationState.Idle;

    public float LeftFootWeight => _leftWeight;
    public float RightFootWeight => _rightWeight;
    public bool LeftFootLocked => _leftLocked;
    public bool RightFootLocked => _rightLocked;
    public float LeftFootContactError { get; private set; }
    public float RightFootContactError { get; private set; }
    public float LeftFootLockDrift { get; private set; }
    public float RightFootLockDrift { get; private set; }
    public FootballInteractionMode InteractionMode => _interactionMode;
    public float RootHeightOffset => _rootHeightOffset;

    public void Configure(
        Skeleton3D skeleton,
        Node3D rigRoot,
        Node3D leftSole,
        Node3D rightSole,
        Node3D leftHand,
        Node3D rightHand,
        Node3D quarterbackHoldAnchor,
        Node3D throwAnchor,
        Node3D catchAnchor,
        Node3D carryAnchor,
        IGroundSurfaceSampler? ground = null)
    {
        _skeleton = skeleton;
        _rigRoot = rigRoot;
        _leftSole = leftSole;
        _rightSole = rightSole;
        _leftHand = leftHand;
        _rightHand = rightHand;
        _quarterbackHoldAnchor = quarterbackHoldAnchor;
        _throwAnchor = throwAnchor;
        _catchAnchor = catchAnchor;
        _carryAnchor = carryAnchor;
        _ground = ground ?? new FlatGroundSurfaceSampler();
        ProcessPriority = 50;
        SetProcess(true);
    }

    public void ApplyCue(HumanoidAnimationCue cue)
    {
        _cue = cue;
        if (_lastState != cue.State)
        {
            _lastState = cue.State;
            _stateElapsed = 0;
        }
        _interactionMode = cue.State switch
        {
            HumanoidAnimationState.QuarterbackDropback or HumanoidAnimationState.QuarterbackSet =>
                FootballInteractionMode.QuarterbackHold,
            HumanoidAnimationState.Throw => FootballInteractionMode.ThrowingHand,
            HumanoidAnimationState.CatchPrepare or HumanoidAnimationState.Catch or
                HumanoidAnimationState.InterceptionCatch or HumanoidAnimationState.DroppedCatch =>
                FootballInteractionMode.CatchHands,
            HumanoidAnimationState.PostCatchRun or HumanoidAnimationState.TouchdownCelebration =>
                FootballInteractionMode.Carry,
            _ => FootballInteractionMode.None
        };
    }

    public void SetInteractionMode(FootballInteractionMode mode) => _interactionMode = mode;

    public override void _Process(double delta)
    {
        if (_skeleton is null)
            return;
        _stateElapsed += (float)delta;
        var blend = 1f - MathF.Exp(-13f * (float)delta);
        _leftWeight = Mathf.Lerp(_leftWeight, _cue.LeftFootPlantWeight, blend);
        _rightWeight = Mathf.Lerp(_rightWeight, _cue.RightFootPlantWeight, blend);
        SolveRootHeight(blend);
        SolveFoot(true, _leftWeight, ref _leftLocked, ref _leftLockPoint);
        SolveFoot(false, _rightWeight, ref _rightLocked, ref _rightLockPoint);
        SolveHands(blend);
        UpdateBallAnchors();
        UpdateMetrics();
    }

    private void SolveRootHeight(float blend)
    {
        var totalWeight = _leftWeight + _rightWeight;
        if (totalWeight < 0.05f)
            return;
        var leftPosition = SoleWorld(true);
        var rightPosition = SoleWorld(false);
        var left = _ground.Sample(leftPosition);
        var right = _ground.Sample(rightPosition);
        var correction = 0f;
        var divisor = 0f;
        if (left.IsValid)
        {
            correction += (left.Position.Y - leftPosition.Y) * _leftWeight;
            divisor += _leftWeight;
        }
        if (right.IsValid)
        {
            correction += (right.Position.Y - rightPosition.Y) * _rightWeight;
            divisor += _rightWeight;
        }
        if (divisor <= 0) return;
        _rootHeightOffset = Math.Clamp(_rootHeightOffset + correction / divisor * blend, -0.32f, 0.08f);
        _skeleton.SetBonePosePosition(_skeleton.FindBone(HumanoidSkeletonDefinition.Root),
            new Vector3(0, _rootHeightOffset, 0));
    }

    private void SolveFoot(bool left, float weight, ref bool locked, ref Vector3 lockPoint)
    {
        var markerPosition = SoleWorld(left);
        var sample = _ground.Sample(markerPosition);
        if (!sample.IsValid)
        {
            locked = false;
            return;
        }
        if (weight >= 0.48f && !locked)
        {
            locked = true;
            lockPoint = sample.Position;
        }
        else if (weight < 0.18f)
        {
            locked = false;
        }
        if (!locked)
        {
            lockPoint = sample.Position;
            var relaxedBone = _skeleton.FindBone(
                left ? HumanoidSkeletonDefinition.LeftFoot : HumanoidSkeletonDefinition.RightFoot);
            var currentOffset = _skeleton.GetBonePosePosition(relaxedBone);
            _skeleton.SetBonePosePosition(relaxedBone,
                currentOffset.Lerp(Vector3.Zero, 0.18f + (1 - Math.Clamp(weight, 0, 1)) * 0.22f));
            return;
        }

        var boneName = left ? HumanoidSkeletonDefinition.LeftFoot : HumanoidSkeletonDefinition.RightFoot;
        var index = _skeleton.FindBone(boneName);
        var parent = _skeleton.GetBoneParent(index);
        var soleWorldOffset = SoleOffset * _rigRoot.GlobalBasis.Y.Length();
        var desiredWorld = lockPoint + sample.Normal * soleWorldOffset;
        var desiredSkeleton = _skeleton.GlobalTransform.AffineInverse() * desiredWorld;
        var desiredParent = _skeleton.GetBoneGlobalPose(parent).AffineInverse() * desiredSkeleton;
        var restPosition = _skeleton.GetBoneRest(index).Origin;
        var correction = desiredParent - restPosition;
        correction.Y = 0;
        correction = correction.LimitLength(MaximumFootCorrection);
        var current = _skeleton.GetBonePosePosition(index);
        _skeleton.SetBonePosePosition(index, current.Lerp(correction, Math.Clamp(weight, 0, 1)));
    }

    private void SolveHands(float blend)
    {
        var target = HandTarget();
        var weight = HandIkWeight();
        if (_interactionMode == FootballInteractionMode.None && weight <= 0)
        {
            ApplyHandOffset(HumanoidSkeletonDefinition.LeftHand, Vector3.Zero, blend);
            ApplyHandOffset(HumanoidSkeletonDefinition.RightHand, Vector3.Zero, blend);
            return;
        }

        var separation = _interactionMode == FootballInteractionMode.Carry ? 0.06f : 0.105f;
        if (_cue.State == HumanoidAnimationState.DroppedCatch && _stateElapsed > 0.2f)
            separation = Mathf.Lerp(separation, 0.34f, Math.Clamp((_stateElapsed - 0.2f) / 0.3f, 0, 1));
        var leftTarget = target + Vector3.Left * separation;
        var rightTarget = target + Vector3.Right * separation;
        var leftWeight = _interactionMode == FootballInteractionMode.Carry ? weight * 0.2f : weight;
        ApplyHandTarget(HumanoidSkeletonDefinition.LeftHand, leftTarget, leftWeight * blend);
        ApplyHandTarget(HumanoidSkeletonDefinition.RightHand, rightTarget, weight * blend);
    }

    private Vector3 HandTarget() => _interactionMode switch
    {
        FootballInteractionMode.QuarterbackHold => new Vector3(0, 1.28f, -0.36f),
        FootballInteractionMode.ThrowingHand => new Vector3(0.3f, 1.5f, -0.52f),
        FootballInteractionMode.CatchHands => new Vector3(0, 1.3f, -0.52f),
        FootballInteractionMode.Carry => new Vector3(0.22f, 1.12f, -0.25f),
        _ => Vector3.Zero
    };

    private float HandIkWeight() => _interactionMode switch
    {
        FootballInteractionMode.QuarterbackHold => 0.88f,
        FootballInteractionMode.ThrowingHand => 0.72f,
        FootballInteractionMode.CatchHands => 0.9f,
        FootballInteractionMode.Carry => 0.78f,
        _ => 0
    };

    private void ApplyHandTarget(string boneName, Vector3 desiredSkeleton, float weight)
    {
        var index = _skeleton.FindBone(boneName);
        var parent = _skeleton.GetBoneParent(index);
        var desiredParent = _skeleton.GetBoneGlobalPose(parent).AffineInverse() * desiredSkeleton;
        var restPosition = _skeleton.GetBoneRest(index).Origin;
        var correction = (desiredParent - restPosition).LimitLength(MaximumHandCorrection);
        ApplyHandOffset(boneName, correction, weight);
    }

    private void ApplyHandOffset(string boneName, Vector3 correction, float weight)
    {
        var index = _skeleton.FindBone(boneName);
        var current = _skeleton.GetBonePosePosition(index);
        _skeleton.SetBonePosePosition(index, current.Lerp(correction, Math.Clamp(weight, 0, 1)));
    }

    private void UpdateBallAnchors()
    {
        var left = HandWorld(true);
        var right = HandWorld(false);
        SetAnchorGlobalPosition(_leftHand, left);
        SetAnchorGlobalPosition(_rightHand, right);
        SetAnchorGlobalPosition(_quarterbackHoldAnchor, left.Lerp(right, 0.5f));
        SetAnchorGlobalPosition(_throwAnchor, right);
        SetAnchorGlobalPosition(_catchAnchor, left.Lerp(right, 0.5f));
        var carry = right.Lerp(_rigRoot.ToGlobal(new Vector3(0.18f, 1.12f, -0.2f)), 0.35f);
        SetAnchorGlobalPosition(_carryAnchor, carry);
    }

    private static void SetAnchorGlobalPosition(Node3D anchor, Vector3 position) =>
        anchor.GlobalPosition = position;

    private void UpdateMetrics()
    {
        var leftPosition = SoleWorld(true);
        var rightPosition = SoleWorld(false);
        var leftSample = _ground.Sample(leftPosition);
        var rightSample = _ground.Sample(rightPosition);
        LeftFootContactError = leftSample.IsValid ? Math.Abs(leftPosition.Y - leftSample.Position.Y) : 0;
        RightFootContactError = rightSample.IsValid ? Math.Abs(rightPosition.Y - rightSample.Position.Y) : 0;
        LeftFootLockDrift = _leftLocked ? PlanarDistance(leftPosition, _leftLockPoint) : 0;
        RightFootLockDrift = _rightLocked ? PlanarDistance(rightPosition, _rightLockPoint) : 0;
    }

    private Vector3 SoleWorld(bool left) => BoneMarkerWorld(
        left ? HumanoidSkeletonDefinition.LeftFoot : HumanoidSkeletonDefinition.RightFoot,
        new Vector3(0, -SoleOffset, 0));

    private Vector3 HandWorld(bool left) => BoneMarkerWorld(
        left ? HumanoidSkeletonDefinition.LeftHand : HumanoidSkeletonDefinition.RightHand,
        new Vector3(0, -0.04f, 0));

    private Vector3 BoneMarkerWorld(string boneName, Vector3 markerOffset)
    {
        var pose = _skeleton.GetBoneGlobalPose(_skeleton.FindBone(boneName));
        return _skeleton.GlobalTransform * (pose * markerOffset);
    }

    private static float PlanarDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();
}
