using System;
using System.Collections.Generic;

namespace FlagFootballStudio.Domain;

public enum PassArcPreset
{
    Short,
    Medium,
    Deep
}

public enum ThrowTriggerMode
{
    Time,
    RouteMilestone
}

public enum PlayOutcomeKind
{
    Completion,
    Incompletion,
    Interception,
    DroppedPass,
    FlagPullAfterCatch,
    Touchdown,
    OutOfBounds
}

public enum RouteClassification
{
    Slant,
    Out,
    In,
    Go,
    Curl,
    Corner,
    Post,
    CustomWaypoint
}

public enum DefensiveCoverageType
{
    Man,
    Zone,
    Rusher
}

public enum SimulationMotionState
{
    Idle,
    Jog,
    Sprint,
    Turn,
    Throw,
    Catch,
    FlagPull
}

public enum BallPhase
{
    Ready,
    Snap,
    HeldByQuarterback,
    PassFlight,
    Caught,
    Intercepted,
    Dropped,
    Incomplete
}

public enum PossessionPhase
{
    PreSnap,
    Snap,
    Quarterback,
    PassInFlight,
    Receiver,
    Defender,
    Loose,
    Ended
}

public enum SimulationEventType
{
    PlayStarted,
    SnapStarted,
    SnapReceived,
    RouteReleased,
    DefenderReacted,
    RusherReleased,
    QuarterbackSet,
    ThrowReleased,
    CatchWindowOpened,
    PassCompleted,
    PassIncomplete,
    PassDropped,
    Intercepted,
    FlagPullAttempted,
    FlagPulled,
    Touchdown,
    OutOfBounds,
    PlayEnded
}

public readonly record struct PlaySimulationSettings(
    double PreSnapDelay,
    double ThrowTime,
    ThrowTriggerMode ThrowTrigger,
    float ThrowRouteProgress,
    PassArcPreset PassArc,
    PlayOutcomeKind IntendedOutcome,
    double DefensiveReactionDelay,
    double RusherDelay,
    float ThrowSpeed,
    float TargetLead,
    double CatchWindow)
{
    public static PlaySimulationSettings Default => new(
        1.0, 0.75, ThrowTriggerMode.Time, 0.6f, PassArcPreset.Short, PlayOutcomeKind.FlagPullAfterCatch,
        0.25, 0.65, 14, 0.18f, 0.4);

    public PlaySimulationSettings Validated() => new(
        Math.Clamp(PreSnapDelay, 0, 5),
        Math.Clamp(ThrowTime, 0.35, 8),
        ThrowTrigger,
        Math.Clamp(ThrowRouteProgress, 0.05f, 1),
        PassArc,
        IntendedOutcome,
        Math.Clamp(DefensiveReactionDelay, 0, 3),
        Math.Clamp(RusherDelay, 0, 5),
        Math.Clamp(ThrowSpeed, 5, 40),
        Math.Clamp(TargetLead, 0, 3),
        Math.Clamp(CatchWindow, 0.1, 2));
}

public readonly record struct SimulationVector3(float X, float Y, float Z)
{
    public static SimulationVector3 Lerp(SimulationVector3 from, SimulationVector3 to, float weight) =>
        new(from.X + (to.X - from.X) * weight,
            from.Y + (to.Y - from.Y) * weight,
            from.Z + (to.Z - from.Z) * weight);

    public float DistanceTo(SimulationVector3 other)
    {
        var x = other.X - X;
        var y = other.Y - Y;
        var z = other.Z - Z;
        return MathF.Sqrt(x * x + y * y + z * z);
    }
}

public abstract record PlayerAssignment(Guid PlayerId, float MaximumSpeed, float Acceleration);

public sealed record OffensiveAssignment(
    Guid PlayerId,
    float MaximumSpeed,
    float Acceleration,
    double ReleaseTime,
    RouteClassification RouteType,
    IReadOnlyList<PlayPoint> Waypoints)
    : PlayerAssignment(PlayerId, MaximumSpeed, Acceleration);

public sealed record DefensiveAssignment(
    Guid PlayerId,
    float MaximumSpeed,
    float Acceleration,
    DefensiveCoverageType CoverageType,
    Guid? TargetPlayerId,
    PlayPoint ZoneLandmark,
    double ReactionTime)
    : PlayerAssignment(PlayerId, MaximumSpeed, Acceleration);

public sealed record RouteProgress(
    Guid PlayerId,
    int SegmentIndex,
    float SegmentProgress,
    float DistanceTravelled,
    bool Complete);

public sealed record PlayerSimulationState(
    Guid PlayerId,
    SimulationVector3 Position,
    SimulationVector3 FacingDirection,
    float Speed,
    SimulationMotionState MotionState,
    RouteProgress? RouteProgress = null);

public sealed record BallState(
    BallPhase Phase,
    SimulationVector3 Position,
    Guid? PossessingPlayerId,
    Guid? TargetPlayerId);

public sealed record PossessionState(
    PossessionPhase Phase,
    Guid TeamId,
    Guid? PlayerId);

public sealed record SimulationEvent(
    double TimeSeconds,
    SimulationEventType Type,
    Guid? PlayerId = null,
    Guid? RelatedPlayerId = null,
    string Description = "");

public sealed record SimulationFrame(
    double TimeSeconds,
    IReadOnlyDictionary<Guid, PlayerSimulationState> Players,
    BallState Ball,
    PossessionState Possession);

public sealed record PlayOutcome(
    PlayOutcomeKind Kind,
    int YardsGained,
    bool PossessionChanges,
    bool Touchdown,
    double EndTimeSeconds,
    Guid? BallCarrierId,
    string Description);

public sealed class PlaySimulation
{
    public PlaySimulation(
        IReadOnlyList<PlayerAssignment> assignments,
        IReadOnlyList<SimulationFrame> frames,
        IReadOnlyList<SimulationEvent> events,
        PlayOutcome outcome,
        double fixedStepSeconds)
    {
        Assignments = assignments;
        Frames = frames;
        Events = events;
        Outcome = outcome;
        FixedStepSeconds = fixedStepSeconds;
    }

    public IReadOnlyList<PlayerAssignment> Assignments { get; }
    public IReadOnlyList<SimulationFrame> Frames { get; }
    public IReadOnlyList<SimulationEvent> Events { get; }
    public PlayOutcome Outcome { get; }
    public double FixedStepSeconds { get; }
    public double DurationSeconds => Outcome.EndTimeSeconds;

    public SimulationFrame FrameAt(double timeSeconds)
    {
        var index = Math.Clamp((int)Math.Round(timeSeconds / FixedStepSeconds), 0, Frames.Count - 1);
        return Frames[index];
    }
}

public sealed class SimulationClock
{
    public SimulationClock(double fixedStepSeconds = 1d / 30d)
    {
        if (fixedStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds));
        FixedStepSeconds = fixedStepSeconds;
    }

    public double FixedStepSeconds { get; }
    public double TimeSeconds { get; private set; }
    public void Reset() => TimeSeconds = 0;
    public double Advance()
    {
        TimeSeconds += FixedStepSeconds;
        return TimeSeconds;
    }
}
