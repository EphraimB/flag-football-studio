using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Presentation;

public sealed class FootballAnimationQualityLayer
{
    private const float SharpTurnDegreesPerFrame = 7.5f;
    private const float CurvedTurnDegreesPerFrame = 1.5f;
    private const float AccelerationThreshold = 0.85f;

    public FootballAnimationTimeline Build(PlaySimulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (simulation.Frames.Count == 0)
            throw new ArgumentException("An animation timeline requires simulation frames.", nameof(simulation));

        var assignments = simulation.Assignments.ToDictionary(item => item.PlayerId);
        var context = AnimationContext.From(simulation);
        var frames = new List<FootballAnimationFrame>(simulation.Frames.Count);
        for (var index = 0; index < simulation.Frames.Count; index++)
        {
            var simulationFrame = simulation.Frames[index];
            var cues = new Dictionary<Guid, HumanoidAnimationCue>();
            foreach (var state in simulationFrame.Players.Values)
            {
                var assignment = assignments[state.PlayerId];
                var speed = KinematicSpeed(simulation, index, state.PlayerId);
                var acceleration = KinematicAcceleration(simulation, index, state.PlayerId);
                var turnDegrees = SignedTurnDegrees(simulation, index, state.PlayerId);
                var maximumSpeed = Math.Max(0.1f, assignment.MaximumSpeed);
                var normalizedSpeed = Math.Clamp(speed / maximumSpeed, 0, 1.15f);
                var animationState = ResolveState(simulation, context, assignment, state,
                    simulationFrame.TimeSeconds, speed, acceleration, turnDegrees);
                var strideFrequency = ResolveStrideFrequency(animationState, speed, normalizedSpeed);
                var lean = ResolveBodyLean(animationState, acceleration, normalizedSpeed);
                var headMotion = ResolveHeadMotion(animationState, normalizedSpeed);
                cues[state.PlayerId] = new HumanoidAnimationCue(animationState, speed, normalizedSpeed,
                    acceleration, Math.Abs(turnDegrees), Math.Sign(turnDegrees), strideFrequency, lean, headMotion);
            }
            frames.Add(new FootballAnimationFrame(simulationFrame.TimeSeconds,
                new ReadOnlyDictionary<Guid, HumanoidAnimationCue>(cues)));
        }
        return new FootballAnimationTimeline(frames.AsReadOnly(), simulation.FixedStepSeconds);
    }

    private static HumanoidAnimationState ResolveState(
        PlaySimulation simulation,
        AnimationContext context,
        PlayerAssignment assignment,
        PlayerSimulationState state,
        double time,
        float speed,
        float acceleration,
        float signedTurnDegrees)
    {
        if (context.TouchdownPlayerId == state.PlayerId &&
            time >= Math.Max(context.ArrivalTime + 0.45, context.TouchdownTime - 0.35))
            return HumanoidAnimationState.TouchdownCelebration;
        if (context.FlagPullerId == state.PlayerId && time >= context.FlagAttemptTime - 0.03 &&
            time <= context.FlagPulledTime + 0.12)
            return HumanoidAnimationState.FlagPull;
        if (context.QuarterbackId == state.PlayerId && context.ThrowTime.HasValue &&
            time >= context.ThrowTime.Value - 0.16 && time <= context.ThrowTime.Value + 0.38)
            return HumanoidAnimationState.Throw;
        if (context.InterceptorId == state.PlayerId &&
            time >= context.ArrivalTime - 0.12 && time <= context.ArrivalTime + 0.34)
            return HumanoidAnimationState.InterceptionCatch;
        if (context.ReceiverId == state.PlayerId && context.Outcome == PlayOutcomeKind.DroppedPass &&
            time >= context.ArrivalTime - 0.1 && time <= context.ArrivalTime + 0.46)
            return HumanoidAnimationState.DroppedCatch;
        if (context.ReceiverId == state.PlayerId && context.HasReceiverCatch &&
            time >= context.ArrivalTime - 0.08 && time <= context.ArrivalTime + 0.34)
            return HumanoidAnimationState.Catch;
        if (context.ReceiverId == state.PlayerId && context.HasPass &&
            time >= context.CatchWindowTime && time < context.ArrivalTime - 0.08)
            return HumanoidAnimationState.CatchPrepare;
        if (context.QuarterbackId == state.PlayerId)
        {
            if (time < context.SnapTime)
                return HumanoidAnimationState.PreSnapReady;
            if (time >= context.SnapReceivedTime - 0.11 && time <= context.SnapReceivedTime + 0.12)
                return HumanoidAnimationState.Catch;
            if (time > context.SnapReceivedTime && time < context.QuarterbackSetTime)
                return HumanoidAnimationState.QuarterbackDropback;
            if (time >= context.QuarterbackSetTime && (!context.ThrowTime.HasValue || time < context.ThrowTime.Value - 0.16))
                return HumanoidAnimationState.QuarterbackSet;
        }
        if (time < context.SnapTime)
            return HumanoidAnimationState.PreSnapReady;
        if (context.ReceiverId == state.PlayerId && context.HasReceiverCatch &&
            time > context.ArrivalTime + 0.34 && time < simulation.DurationSeconds - 0.02)
            return HumanoidAnimationState.PostCatchRun;
        if (state.MotionState == SimulationMotionState.FlagPull)
            return HumanoidAnimationState.FlagPull;

        var isRouted = assignment is OffensiveAssignment offensive && offensive.Waypoints.Count > 0;
        if (isRouted && speed > 0.6f && Math.Abs(signedTurnDegrees) >= SharpTurnDegreesPerFrame)
            return HumanoidAnimationState.RouteCut;
        if (speed > 0.6f && Math.Abs(signedTurnDegrees) >= CurvedTurnDegreesPerFrame)
            return HumanoidAnimationState.CurvedTurn;
        if (speed > 0.35f && acceleration >= AccelerationThreshold)
            return HumanoidAnimationState.Acceleration;
        if (speed > 0.2f && acceleration <= -AccelerationThreshold)
            return HumanoidAnimationState.Deceleration;
        if (state.MotionState == SimulationMotionState.Turn)
            return HumanoidAnimationState.RouteCut;
        if (speed > assignment.MaximumSpeed * 0.76f || state.MotionState == SimulationMotionState.Sprint)
            return HumanoidAnimationState.Sprint;
        if (speed > 0.15f || state.MotionState == SimulationMotionState.Jog)
            return HumanoidAnimationState.Jog;
        return HumanoidAnimationState.Idle;
    }

    private static float KinematicSpeed(PlaySimulation simulation, int index, Guid playerId)
    {
        if (simulation.Frames.Count == 1) return 0;
        var before = simulation.Frames[Math.Max(0, index - 1)];
        var after = simulation.Frames[Math.Min(simulation.Frames.Count - 1, index + 1)];
        var elapsed = Math.Max(simulation.FixedStepSeconds, after.TimeSeconds - before.TimeSeconds);
        return before.Players[playerId].Position.DistanceTo(after.Players[playerId].Position) / (float)elapsed;
    }

    private static float KinematicAcceleration(PlaySimulation simulation, int index, Guid playerId)
    {
        if (index == 0) return 0;
        var current = KinematicSpeed(simulation, index, playerId);
        var previous = KinematicSpeed(simulation, index - 1, playerId);
        return (current - previous) / (float)simulation.FixedStepSeconds;
    }

    private static float SignedTurnDegrees(PlaySimulation simulation, int index, Guid playerId)
    {
        if (index == 0) return 0;
        var previous = simulation.Frames[index - 1].Players[playerId].FacingDirection;
        var current = simulation.Frames[index].Players[playerId].FacingDirection;
        var dot = Math.Clamp(previous.X * current.X + previous.Z * current.Z, -1, 1);
        var cross = previous.X * current.Z - previous.Z * current.X;
        return MathF.Acos(dot) * 180f / MathF.PI * MathF.Sign(cross == 0 ? 1 : cross);
    }

    private static float ResolveStrideFrequency(HumanoidAnimationState state, float speed, float normalizedSpeed)
    {
        if (state is HumanoidAnimationState.Idle or HumanoidAnimationState.PreSnapReady or
            HumanoidAnimationState.QuarterbackSet or HumanoidAnimationState.Throw or
            HumanoidAnimationState.CatchPrepare or HumanoidAnimationState.Catch or
            HumanoidAnimationState.DroppedCatch or HumanoidAnimationState.InterceptionCatch or
            HumanoidAnimationState.FlagPull or HumanoidAnimationState.TouchdownCelebration)
            return 0;
        return Math.Clamp(1.25f + speed * 0.23f + normalizedSpeed * 0.28f, 1.3f, 3.15f);
    }

    private static float ResolveBodyLean(HumanoidAnimationState state, float acceleration, float normalizedSpeed)
    {
        if (state == HumanoidAnimationState.Deceleration)
            return Math.Clamp(acceleration * 0.018f, -0.16f, -0.04f);
        if (state == HumanoidAnimationState.Acceleration)
            return Math.Clamp(0.12f + acceleration * 0.012f, 0.14f, 0.3f);
        if (state is HumanoidAnimationState.Sprint or HumanoidAnimationState.PostCatchRun)
            return 0.12f + normalizedSpeed * 0.12f;
        if (state is HumanoidAnimationState.Jog or HumanoidAnimationState.QuarterbackDropback)
            return 0.04f + normalizedSpeed * 0.09f;
        return 0;
    }

    private static float ResolveHeadMotion(HumanoidAnimationState state, float normalizedSpeed) => state switch
    {
        HumanoidAnimationState.Sprint or HumanoidAnimationState.PostCatchRun => 0.4f + normalizedSpeed * 0.2f,
        HumanoidAnimationState.Jog or HumanoidAnimationState.Acceleration or HumanoidAnimationState.Deceleration => 0.32f,
        HumanoidAnimationState.RouteCut or HumanoidAnimationState.CurvedTurn => 0.22f,
        _ => 0.12f
    };

    private sealed record AnimationContext(
        Guid QuarterbackId,
        Guid ReceiverId,
        Guid? InterceptorId,
        Guid? FlagPullerId,
        Guid? TouchdownPlayerId,
        double SnapTime,
        double SnapReceivedTime,
        double QuarterbackSetTime,
        double? ThrowTime,
        double CatchWindowTime,
        double ArrivalTime,
        double FlagAttemptTime,
        double FlagPulledTime,
        double TouchdownTime,
        PlayOutcomeKind Outcome)
    {
        public bool HasPass => ThrowTime.HasValue;
        public bool HasReceiverCatch => Outcome is PlayOutcomeKind.Completion or
            PlayOutcomeKind.FlagPullAfterCatch or PlayOutcomeKind.Touchdown or PlayOutcomeKind.OutOfBounds;

        public static AnimationContext From(PlaySimulation simulation)
        {
            SimulationEvent Required(SimulationEventType type) =>
                simulation.Events.First(item => item.Type == type);
            SimulationEvent? Optional(SimulationEventType type) =>
                simulation.Events.FirstOrDefault(item => item.Type == type);

            var snap = Required(SimulationEventType.SnapStarted);
            var received = Required(SimulationEventType.SnapReceived);
            var set = Required(SimulationEventType.QuarterbackSet);
            var release = Optional(SimulationEventType.ThrowReleased);
            var window = Optional(SimulationEventType.CatchWindowOpened);
            var arrival = simulation.Events.FirstOrDefault(item => item.Type is SimulationEventType.PassCompleted
                or SimulationEventType.PassIncomplete or SimulationEventType.PassDropped or SimulationEventType.Intercepted);
            var flagAttempt = Optional(SimulationEventType.FlagPullAttempted);
            var flagPulled = Optional(SimulationEventType.FlagPulled);
            var touchdown = Optional(SimulationEventType.Touchdown);
            return new AnimationContext(
                received.PlayerId!.Value,
                release?.RelatedPlayerId ?? window?.PlayerId ?? simulation.Outcome.BallCarrierId ?? Guid.Empty,
                Optional(SimulationEventType.Intercepted)?.PlayerId,
                flagAttempt?.PlayerId,
                touchdown?.PlayerId,
                snap.TimeSeconds,
                received.TimeSeconds,
                set.TimeSeconds,
                release?.TimeSeconds,
                window?.TimeSeconds ?? double.PositiveInfinity,
                arrival?.TimeSeconds ?? simulation.DurationSeconds,
                flagAttempt?.TimeSeconds ?? double.PositiveInfinity,
                flagPulled?.TimeSeconds ?? double.NegativeInfinity,
                touchdown?.TimeSeconds ?? double.PositiveInfinity,
                simulation.Outcome.Kind);
        }
    }
}

public sealed record FootballAnimationFrame(
    double TimeSeconds,
    IReadOnlyDictionary<Guid, HumanoidAnimationCue> Players);

public sealed class FootballAnimationTimeline
{
    public FootballAnimationTimeline(IReadOnlyList<FootballAnimationFrame> frames, double fixedStepSeconds)
    {
        Frames = frames;
        FixedStepSeconds = fixedStepSeconds;
    }

    public IReadOnlyList<FootballAnimationFrame> Frames { get; }
    public double FixedStepSeconds { get; }

    public FootballAnimationFrame FrameAt(double timeSeconds)
    {
        var index = Math.Clamp((int)Math.Round(timeSeconds / FixedStepSeconds), 0, Frames.Count - 1);
        return Frames[index];
    }
}
