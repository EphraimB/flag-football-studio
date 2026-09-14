using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public sealed class FootballPlaySimulator
{
    public const double FixedStepSeconds = 1d / 30d;
    private const double SnapTravelSeconds = 0.34;
    private const float PlayerGroundHeight = 0.08f;
    private const float HeldBallHeight = 1.28f;
    private const float CatchBallHeight = 1.35f;

    public PlaySimulation Simulate(PlayDefinition play, Team offense, Team defense)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(offense);
        ArgumentNullException.ThrowIfNull(defense);
        ValidatePlay(play, offense, defense);

        var settings = play.SimulationSettings.Validated();
        var direction = PlayDirectionResolver.Resolve(play, offense, defense);
        var directionSign = PlayDirectionResolver.Sign(direction);
        var quarterback = offense.Roster.Single(player => player.Id == play.QuarterbackId);
        var receiver = offense.Roster.Single(player => player.Id == play.IntendedReceiverId);
        var center = offense.Roster.FirstOrDefault(player => player.Position == PlayerPosition.Center) ?? quarterback;
        var snapTime = settings.PreSnapDelay;
        var snapReceivedTime = snapTime + SnapTravelSeconds;
        var routeReleaseTime = snapTime + 0.05;
        var (dropDepth, dropDuration) = DropFor(settings.PassArc);
        var quarterbackSetTime = snapReceivedTime + dropDuration;
        var quarterbackStart = ToSimulation(play.StartingPositions[quarterback.Id]);
        var quarterbackSet = quarterbackStart with { Z = quarterbackStart.Z - directionSign * dropDepth };

        var assignments = BuildAssignments(play, offense, defense, routeReleaseTime, snapReceivedTime, settings, direction);
        var receiverAssignment = assignments.OfType<OffensiveAssignment>().Single(item => item.PlayerId == receiver.Id);
        var authoredThrowTime = settings.ThrowTrigger == ThrowTriggerMode.RouteMilestone
            ? ResolveMilestoneThrowTime(receiverAssignment, play.StartingPositions[receiver.Id], settings.ThrowRouteProgress, snapReceivedTime)
            : snapReceivedTime + settings.ThrowTime;
        var throwTime = Math.Max(authoredThrowTime, quarterbackSetTime + 0.12);
        var noPass = settings.IntendedOutcome == PlayOutcomeKind.QuarterbackFlagPull;

        var receiverAtThrow = SampleRoute(receiverAssignment, play.StartingPositions[receiver.Id], throwTime);
        var estimatedDistance = quarterbackSet.DistanceTo(receiverAtThrow.Position);
        var flightSeconds = Math.Clamp(estimatedDistance / settings.ThrowSpeed, 0.38f, 1.75f);
        var arrivalTime = throwTime + flightSeconds;
        var receiverAtArrival = SampleRoute(receiverAssignment, play.StartingPositions[receiver.Id], arrivalTime + settings.TargetLead);
        var catchPoint = receiverAtArrival.Position with { Y = CatchBallHeight };
        var interceptorId = assignments.OfType<DefensiveAssignment>()
            .Where(item => item.CoverageType == DefensiveCoverageType.Man && item.TargetPlayerId == receiver.Id)
            .Select(item => (Guid?)item.PlayerId)
            .FirstOrDefault() ?? defense.Roster[0].Id;
        var flagPullerId = settings.IntendedOutcome == PlayOutcomeKind.QuarterbackFlagPull
            ? assignments.OfType<DefensiveAssignment>().First(item => item.CoverageType == DefensiveCoverageType.Rusher).PlayerId
            : assignments.OfType<DefensiveAssignment>()
                .Where(item => item.TargetPlayerId == receiver.Id)
                .Select(item => (Guid?)item.PlayerId)
                .FirstOrDefault() ?? defense.Roster[0].Id;

        var endTime = noPass
            ? Math.Max(snapReceivedTime + settings.RusherDelay + 1.25, quarterbackSetTime + 0.7)
            : arrivalTime + PostOutcomeDuration(settings.IntendedOutcome);
        var carrierEnd = BuildCarrierEnd(settings.IntendedOutcome, catchPoint, receiverAtArrival.Facing, directionSign);
        var passTarget = BuildPassTarget(settings.IntendedOutcome, catchPoint, receiverAtArrival.Facing, directionSign);
        var outcome = BuildOutcome(settings.IntendedOutcome, play, receiver, interceptorId,
            carrierEnd, directionSign, endTime, dropDepth);
        var events = BuildEvents(assignments, settings, center.Id, quarterback.Id, receiver.Id, flagPullerId,
            snapTime, snapReceivedTime, quarterbackSetTime, throwTime, arrivalTime, endTime, outcome, noPass);

        var clock = new SimulationClock(FixedStepSeconds);
        var frames = new List<SimulationFrame>();
        var defenderPositions = defense.Roster.ToDictionary(player => player.Id,
            player => ToSimulation(play.StartingPositions[player.Id]));
        var defenderSpeeds = defense.Roster.ToDictionary(player => player.Id, _ => 0f);
        var frameCount = (int)Math.Ceiling(endTime / FixedStepSeconds) + 1;
        for (var index = 0; index < frameCount; index++)
        {
            var time = index == frameCount - 1 ? endTime : clock.TimeSeconds;
            var players = BuildPlayerStates(play, assignments, defenderPositions, defenderSpeeds,
                time, FixedStepSeconds, settings, directionSign, quarterback.Id, receiver.Id, center.Id,
                interceptorId, flagPullerId, quarterbackStart, quarterbackSet, catchPoint, carrierEnd,
                snapTime, snapReceivedTime, quarterbackSetTime, throwTime, arrivalTime, endTime, outcome, noPass);
            var ball = BuildBallState(play, settings, center.Id, quarterback.Id, receiver.Id, interceptorId,
                players, time, snapTime, snapReceivedTime, throwTime, arrivalTime, quarterbackSet, passTarget, outcome, noPass);
            var possession = BuildPossession(offense.Id, defense.Id, quarterback.Id, receiver.Id,
                time, snapTime, snapReceivedTime, throwTime, arrivalTime, outcome, noPass);
            frames.Add(new SimulationFrame(time,
                new ReadOnlyDictionary<Guid, PlayerSimulationState>(players), ball, possession));
            clock.Advance();
        }

        return new PlaySimulation(assignments.AsReadOnly(), frames.AsReadOnly(), events.AsReadOnly(), outcome, FixedStepSeconds);
    }

    private static List<PlayerAssignment> BuildAssignments(
        PlayDefinition play, Team offense, Team defense, double routeReleaseTime,
        double snapReceivedTime, PlaySimulationSettings settings, FieldDirection direction)
    {
        var assignments = new List<PlayerAssignment>();
        foreach (var player in offense.Roster)
        {
            var route = play.Routes.TryGetValue(player.Id, out var authored) ? authored : Array.Empty<PlayPoint>();
            assignments.Add(new OffensiveAssignment(player.Id, MaximumSpeed(player), Acceleration(player),
                routeReleaseTime, ClassifyRoute(play.StartingPositions[player.Id], route, direction), route));
        }

        var unassigned = defense.Roster.Where(player => !play.CoverageAssignments.ContainsKey(player.Id)).ToArray();
        var quarterbackPoint = play.StartingPositions[play.QuarterbackId];
        var rusherId = unassigned.OrderBy(player => Distance(play.StartingPositions[player.Id], quarterbackPoint))
            .Select(player => (Guid?)player.Id).FirstOrDefault();
        foreach (var defender in defense.Roster)
        {
            var hasMan = play.CoverageAssignments.TryGetValue(defender.Id, out var target);
            var coverage = hasMan ? DefensiveCoverageType.Man
                : defender.Id == rusherId ? DefensiveCoverageType.Rusher : DefensiveCoverageType.Zone;
            var start = play.StartingPositions[defender.Id];
            var zoneDepth = 3.5f + MathF.Min(2, MathF.Abs(start.X) * 0.15f);
            var zone = PlayDirectionResolver.Advance(start, direction, zoneDepth);
            var reaction = coverage == DefensiveCoverageType.Rusher
                ? snapReceivedTime + settings.RusherDelay
                : routeReleaseTime + settings.DefensiveReactionDelay;
            assignments.Add(new DefensiveAssignment(defender.Id, 6.4f, 9.2f, coverage,
                hasMan ? target : null, zone, reaction));
        }
        return assignments;
    }

    private static Dictionary<Guid, PlayerSimulationState> BuildPlayerStates(
        PlayDefinition play, IReadOnlyList<PlayerAssignment> assignments,
        Dictionary<Guid, SimulationVector3> defenderPositions, Dictionary<Guid, float> defenderSpeeds,
        double time, double step, PlaySimulationSettings settings, int directionSign,
        Guid quarterbackId, Guid receiverId, Guid centerId, Guid interceptorId, Guid flagPullerId,
        SimulationVector3 quarterbackStart, SimulationVector3 quarterbackSet,
        SimulationVector3 catchPoint, SimulationVector3 carrierEnd,
        double snapTime, double snapReceivedTime, double quarterbackSetTime, double throwTime,
        double arrivalTime, double endTime, PlayOutcome outcome, bool noPass)
    {
        var states = new Dictionary<Guid, PlayerSimulationState>();
        var intendedRoute = assignments.OfType<OffensiveAssignment>().Single(item => item.PlayerId == receiverId);
        var intendedTarget = SampleRoute(intendedRoute, play.StartingPositions[receiverId], Math.Min(time, throwTime)).Position;
        foreach (var assignment in assignments.OfType<OffensiveAssignment>())
        {
            var start = play.StartingPositions[assignment.PlayerId];
            if (assignment.PlayerId == quarterbackId)
            {
                var dropProgress = SmoothStep((float)((time - snapReceivedTime) / Math.Max(0.01, quarterbackSetTime - snapReceivedTime)));
                var quarterbackPosition = SimulationVector3.Lerp(quarterbackStart, quarterbackSet, dropProgress);
                var quarterbackFacing = Direction(quarterbackPosition, intendedTarget);
                var quarterbackMotion = time >= snapTime && time <= snapReceivedTime + 0.14 ? SimulationMotionState.Catch
                    : !noPass && time >= throwTime - 0.14 && time <= throwTime + 0.36 ? SimulationMotionState.Throw
                    : noPass && time >= endTime - 0.35 ? SimulationMotionState.Turn
                    : time > snapReceivedTime && time < quarterbackSetTime ? SimulationMotionState.Jog
                    : SimulationMotionState.Idle;
                states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, quarterbackPosition, quarterbackFacing, 0, quarterbackMotion);
                continue;
            }

            var routeSampleTime = time;
            if (assignment.PlayerId == receiverId && !noPass && time >= throwTime)
            {
                var leadBlend = (float)Math.Clamp((time - throwTime) / Math.Max(0.01, arrivalTime - throwTime), 0, 1);
                routeSampleTime += settings.TargetLead * leadBlend;
            }
            var sample = SampleRoute(assignment, start, routeSampleTime);
            var position = sample.Position;
            var facing = sample.Facing;
            var speed = sample.Speed;
            if (assignment.PlayerId == receiverId && IsCatchOutcome(outcome.Kind) && time >= arrivalTime)
            {
                var carryProgress = SmoothStep((float)((time - arrivalTime) / Math.Max(0.01, endTime - arrivalTime)));
                position = SimulationVector3.Lerp(catchPoint with { Y = PlayerGroundHeight }, carrierEnd, carryProgress);
                facing = Direction(catchPoint, carrierEnd);
                speed = (float)(catchPoint.DistanceTo(carrierEnd) / Math.Max(0.01, endTime - arrivalTime));
            }

            var motion = speed > 5 ? SimulationMotionState.Sprint : speed > 0.1f ? SimulationMotionState.Jog : SimulationMotionState.Idle;
            if (assignment.PlayerId == centerId && time >= snapTime - 0.08 && time <= snapReceivedTime)
                motion = SimulationMotionState.Turn;
            if (assignment.PlayerId == receiverId && !noPass &&
                time >= arrivalTime - settings.CatchWindow * 0.5 && time <= arrivalTime + 0.35)
                motion = SimulationMotionState.Catch;
            states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, position, facing,
                speed, motion, sample.Progress);
        }

        foreach (var assignment in assignments.OfType<DefensiveAssignment>())
        {
            var current = defenderPositions[assignment.PlayerId];
            var target = current;
            if (time >= assignment.ReactionTime)
            {
                target = ResolveDefensiveTarget(assignment, play, assignments, states, quarterbackId,
                    receiverId, interceptorId, time, arrivalTime, outcome);
                var speed = MathF.Min(assignment.MaximumSpeed,
                    defenderSpeeds[assignment.PlayerId] + assignment.Acceleration * (float)step);
                defenderSpeeds[assignment.PlayerId] = speed;
                current = MoveTowards(current, target, speed * (float)step);

                var isPuller = assignment.PlayerId == flagPullerId &&
                    (outcome.Kind is PlayOutcomeKind.FlagPullAfterCatch or PlayOutcomeKind.QuarterbackFlagPull);
                if (isPuller && time >= endTime - 0.28)
                {
                    var carrierId = outcome.Kind == PlayOutcomeKind.QuarterbackFlagPull ? quarterbackId : receiverId;
                    var carrier = states[carrierId].Position;
                    var interaction = carrier with { X = carrier.X + (current.X <= carrier.X ? -0.58f : 0.58f) };
                    var interactionProgress = SmoothStep((float)((time - (endTime - 0.28)) / 0.28));
                    current = SimulationVector3.Lerp(current, interaction, interactionProgress);
                    target = carrier;
                }
                defenderPositions[assignment.PlayerId] = current;
            }

            var facing = Direction(current, target);
            var motion = defenderSpeeds[assignment.PlayerId] > 5 ? SimulationMotionState.Sprint
                : defenderSpeeds[assignment.PlayerId] > 0.1f ? SimulationMotionState.Jog : SimulationMotionState.Idle;
            if (assignment.PlayerId == flagPullerId && time >= endTime - 0.42 &&
                outcome.Kind is PlayOutcomeKind.FlagPullAfterCatch or PlayOutcomeKind.QuarterbackFlagPull)
                motion = SimulationMotionState.FlagPull;
            if (assignment.PlayerId == interceptorId && outcome.Kind == PlayOutcomeKind.Interception &&
                time >= arrivalTime - settings.CatchWindow * 0.5 && time <= arrivalTime + 0.3)
                motion = SimulationMotionState.Catch;
            states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, current, facing,
                defenderSpeeds[assignment.PlayerId], motion, AssignmentTarget: target);
        }
        return states;
    }

    private static SimulationVector3 ResolveDefensiveTarget(
        DefensiveAssignment assignment, PlayDefinition play, IReadOnlyList<PlayerAssignment> assignments,
        IReadOnlyDictionary<Guid, PlayerSimulationState> states, Guid quarterbackId, Guid receiverId,
        Guid interceptorId, double time, double arrivalTime, PlayOutcome outcome)
    {
        if (outcome.Kind == PlayOutcomeKind.Interception && assignment.PlayerId == interceptorId && time < arrivalTime)
            return states[receiverId].Position;
        if (time >= arrivalTime && IsCatchOutcome(outcome.Kind) && states.TryGetValue(receiverId, out var carrier))
            return carrier.Position;
        if (assignment.CoverageType == DefensiveCoverageType.Rusher)
            return states[quarterbackId].Position;
        if (assignment.CoverageType == DefensiveCoverageType.Man && assignment.TargetPlayerId.HasValue &&
            states.TryGetValue(assignment.TargetPlayerId.Value, out var covered))
        {
            var defenderStart = play.StartingPositions[assignment.PlayerId];
            var offenseStart = play.StartingPositions[assignment.TargetPlayerId.Value];
            var leverage = new SimulationVector3(
                (defenderStart.X - offenseStart.X) * 0.22f, 0,
                (defenderStart.Y - offenseStart.Y) * 0.18f);
            return new SimulationVector3(covered.Position.X + leverage.X, PlayerGroundHeight, covered.Position.Z + leverage.Z);
        }

        var landmark = ToSimulation(assignment.ZoneLandmark);
        var routedReceivers = assignments.OfType<OffensiveAssignment>()
            .Where(item => item.Waypoints.Count > 0 && states.ContainsKey(item.PlayerId))
            .Select(item => states[item.PlayerId])
            .OrderBy(state => state.Position.DistanceTo(landmark))
            .ToArray();
        if (routedReceivers.Length == 0 || routedReceivers[0].Position.DistanceTo(landmark) > 5.5f)
            return landmark;
        return MoveTowards(landmark, routedReceivers[0].Position, MathF.Min(3.25f, landmark.DistanceTo(routedReceivers[0].Position)));
    }

    private static BallState BuildBallState(
        PlayDefinition play, PlaySimulationSettings settings, Guid centerId, Guid quarterbackId,
        Guid receiverId, Guid interceptorId, IReadOnlyDictionary<Guid, PlayerSimulationState> players,
        double time, double snapTime, double snapReceivedTime, double throwTime, double arrivalTime,
        SimulationVector3 quarterbackSet, SimulationVector3 passTarget, PlayOutcome outcome, bool noPass)
    {
        var center = ToSimulation(play.StartingPositions[centerId], 0.92f);
        var quarterbackHeld = players[quarterbackId].Position with { Y = HeldBallHeight };
        if (time < snapTime)
            return new BallState(BallPhase.Ready, center, centerId, quarterbackId);
        if (time < snapReceivedTime)
        {
            var progress = (float)((time - snapTime) / (snapReceivedTime - snapTime));
            var position = SimulationVector3.Lerp(center, quarterbackHeld, progress);
            position = position with { Y = position.Y + MathF.Sin(progress * MathF.PI) * 0.16f };
            return new BallState(BallPhase.Snap, position, null, quarterbackId);
        }
        if (noPass || time < throwTime)
            return new BallState(BallPhase.HeldByQuarterback, quarterbackHeld, quarterbackId, receiverId);

        var liveTarget = outcome.Kind switch
        {
            PlayOutcomeKind.Interception => players[interceptorId].Position with { Y = CatchBallHeight },
            PlayOutcomeKind.Incompletion => passTarget,
            _ => players[receiverId].Position with { Y = CatchBallHeight }
        };
        if (time < arrivalTime)
        {
            var progress = (float)((time - throwTime) / (arrivalTime - throwTime));
            var origin = quarterbackSet with { Y = HeldBallHeight };
            var position = SimulationVector3.Lerp(origin, liveTarget, progress);
            var arc = settings.PassArc switch { PassArcPreset.Short => 1.0f, PassArcPreset.Medium => 2.35f, _ => 4.1f };
            position = position with { Y = position.Y + MathF.Sin(progress * MathF.PI) * arc };
            return new BallState(BallPhase.PassFlight, position, null, receiverId);
        }

        return outcome.Kind switch
        {
            PlayOutcomeKind.Interception => new BallState(BallPhase.Intercepted,
                players[interceptorId].Position with { Y = CatchBallHeight }, interceptorId, interceptorId),
            PlayOutcomeKind.Incompletion => new BallState(BallPhase.Incomplete, passTarget, null, receiverId),
            PlayOutcomeKind.DroppedPass => BuildDroppedBall(time, arrivalTime, passTarget, receiverId),
            _ => new BallState(BallPhase.Caught,
                players[receiverId].Position with { Y = CatchBallHeight }, receiverId, receiverId)
        };
    }

    private static BallState BuildDroppedBall(double time, double arrivalTime, SimulationVector3 receiver, Guid receiverId)
    {
        var progress = (float)Math.Clamp((time - arrivalTime) / 0.48, 0, 1);
        var start = receiver with { Y = CatchBallHeight };
        var end = receiver with { X = receiver.X + 0.45f, Y = 0.14f, Z = receiver.Z + 0.35f };
        return new BallState(BallPhase.Dropped, SimulationVector3.Lerp(start, end, SmoothStep(progress)), null, receiverId);
    }

    private static PossessionState BuildPossession(
        Guid offenseId, Guid defenseId, Guid quarterbackId, Guid receiverId, double time,
        double snapTime, double snapReceivedTime, double throwTime, double arrivalTime,
        PlayOutcome outcome, bool noPass)
    {
        if (time < snapTime) return new PossessionState(PossessionPhase.PreSnap, offenseId, null);
        if (time < snapReceivedTime) return new PossessionState(PossessionPhase.Snap, offenseId, null);
        if (noPass || time < throwTime) return new PossessionState(PossessionPhase.Quarterback, offenseId, quarterbackId);
        if (time < arrivalTime) return new PossessionState(PossessionPhase.PassInFlight, offenseId, null);
        if (outcome.Kind == PlayOutcomeKind.Interception)
            return new PossessionState(PossessionPhase.Defender, defenseId, outcome.BallCarrierId);
        if (outcome.Kind is PlayOutcomeKind.Incompletion or PlayOutcomeKind.DroppedPass)
            return new PossessionState(PossessionPhase.Loose, offenseId, null);
        return new PossessionState(PossessionPhase.Receiver, offenseId, receiverId);
    }

    private static List<SimulationEvent> BuildEvents(
        IReadOnlyList<PlayerAssignment> assignments, PlaySimulationSettings settings,
        Guid centerId, Guid quarterbackId, Guid receiverId, Guid flagPullerId,
        double snapTime, double snapReceivedTime, double quarterbackSetTime, double throwTime,
        double arrivalTime, double endTime, PlayOutcome outcome, bool noPass)
    {
        var events = new List<SimulationEvent> { new(0, SimulationEventType.PlayStarted, Description: "Pre-snap") };
        events.Add(new SimulationEvent(snapTime, SimulationEventType.SnapStarted, centerId, quarterbackId, "Snap"));
        foreach (var assignment in assignments.OfType<OffensiveAssignment>().Where(item => item.Waypoints.Count > 0))
            events.Add(new SimulationEvent(assignment.ReleaseTime, SimulationEventType.RouteReleased,
                assignment.PlayerId, Description: $"{assignment.RouteType} release"));
        events.Add(new SimulationEvent(snapReceivedTime, SimulationEventType.SnapReceived,
            quarterbackId, centerId, "Quarterback received snap"));
        foreach (var assignment in assignments.OfType<DefensiveAssignment>())
            events.Add(new SimulationEvent(assignment.ReactionTime,
                assignment.CoverageType == DefensiveCoverageType.Rusher
                    ? SimulationEventType.RusherReleased : SimulationEventType.DefenderReacted,
                assignment.PlayerId, assignment.TargetPlayerId, assignment.CoverageType.ToString()));
        events.Add(new SimulationEvent(quarterbackSetTime, SimulationEventType.QuarterbackSet,
            quarterbackId, Description: $"Quarterback set ({settings.PassArc})"));

        if (!noPass)
        {
            events.Add(new SimulationEvent(throwTime, SimulationEventType.ThrowReleased,
                quarterbackId, receiverId, $"Pass released to {receiverId}"));
            events.Add(new SimulationEvent(arrivalTime - settings.CatchWindow * 0.5,
                SimulationEventType.CatchWindowOpened, receiverId, Description: "Catch window"));
            events.Add(outcome.Kind switch
            {
                PlayOutcomeKind.Interception => new SimulationEvent(arrivalTime,
                    SimulationEventType.Intercepted, outcome.BallCarrierId, quarterbackId, outcome.Description),
                PlayOutcomeKind.Incompletion => new SimulationEvent(arrivalTime,
                    SimulationEventType.PassIncomplete, receiverId, Description: outcome.Description),
                PlayOutcomeKind.DroppedPass => new SimulationEvent(arrivalTime,
                    SimulationEventType.PassDropped, receiverId, Description: outcome.Description),
                _ => new SimulationEvent(arrivalTime,
                    SimulationEventType.PassCompleted, receiverId, quarterbackId, outcome.Description)
            });
        }

        if (outcome.Kind is PlayOutcomeKind.FlagPullAfterCatch or PlayOutcomeKind.QuarterbackFlagPull)
        {
            var carrierId = outcome.Kind == PlayOutcomeKind.QuarterbackFlagPull ? quarterbackId : receiverId;
            events.Add(new SimulationEvent(endTime - 0.42, SimulationEventType.FlagPullAttempted,
                flagPullerId, carrierId, "Flag pull attempt"));
            events.Add(new SimulationEvent(endTime - 0.08, SimulationEventType.FlagPulled,
                carrierId, flagPullerId, outcome.Description));
        }
        if (outcome.Kind == PlayOutcomeKind.Touchdown)
            events.Add(new SimulationEvent(endTime - 0.08, SimulationEventType.Touchdown,
                receiverId, Description: "Touchdown"));
        if (outcome.Kind == PlayOutcomeKind.OutOfBounds)
            events.Add(new SimulationEvent(endTime - 0.08, SimulationEventType.OutOfBounds,
                receiverId, Description: "Out of bounds"));
        events.Add(new SimulationEvent(endTime, SimulationEventType.PlayEnded,
            outcome.BallCarrierId, Description: outcome.Description));
        return events.OrderBy(item => item.TimeSeconds).ThenBy(item => item.Type).ToList();
    }

    private static PlayOutcome BuildOutcome(
        PlayOutcomeKind kind, PlayDefinition play, Player receiver, Guid interceptorId,
        SimulationVector3 carrierEnd, int directionSign, double endTime, float dropDepth)
    {
        var start = play.StartingPositions[receiver.Id];
        var yards = Math.Max(0, (int)MathF.Round((carrierEnd.Z - start.Y) * directionSign));
        if (kind is PlayOutcomeKind.Incompletion or PlayOutcomeKind.DroppedPass or PlayOutcomeKind.Interception)
            yards = 0;
        if (kind == PlayOutcomeKind.QuarterbackFlagPull)
            yards = -(int)MathF.Ceiling(dropDepth);
        if (kind == PlayOutcomeKind.Touchdown)
            yards = Math.Max(yards, 20);
        var changes = kind is PlayOutcomeKind.Interception or PlayOutcomeKind.Touchdown;
        var carrier = kind == PlayOutcomeKind.Interception ? interceptorId
            : kind == PlayOutcomeKind.QuarterbackFlagPull ? play.QuarterbackId
            : kind is PlayOutcomeKind.Incompletion or PlayOutcomeKind.DroppedPass ? (Guid?)null : receiver.Id;
        var description = kind switch
        {
            PlayOutcomeKind.Completion => $"Complete for {yards} yards",
            PlayOutcomeKind.Incompletion => "Incomplete pass",
            PlayOutcomeKind.Interception => "Intercepted",
            PlayOutcomeKind.DroppedPass => "Dropped pass",
            PlayOutcomeKind.FlagPullAfterCatch => $"Flag pulled after {yards} yards",
            PlayOutcomeKind.Touchdown => "Touchdown",
            PlayOutcomeKind.OutOfBounds => $"Out of bounds after {yards} yards",
            PlayOutcomeKind.QuarterbackFlagPull => $"Quarterback flag pulled for a loss of {Math.Abs(yards)}",
            _ => kind.ToString()
        };
        return new PlayOutcome(kind, yards, changes, kind == PlayOutcomeKind.Touchdown,
            endTime, carrier, description);
    }

    private static SimulationVector3 BuildCarrierEnd(
        PlayOutcomeKind kind, SimulationVector3 catchPoint, SimulationVector3 routeFacing, int directionSign)
    {
        var groundCatch = catchPoint with { Y = PlayerGroundHeight };
        return kind switch
        {
            PlayOutcomeKind.Completion => Add(groundCatch, Scale(routeFacing, 4.0f)),
            PlayOutcomeKind.FlagPullAfterCatch => Add(groundCatch, Scale(routeFacing, 2.6f)),
            PlayOutcomeKind.Touchdown => groundCatch with { Z = directionSign * 20f },
            PlayOutcomeKind.OutOfBounds => groundCatch with
            {
                X = catchPoint.X >= 0 ? 9.8f : -9.8f,
                Z = catchPoint.Z + directionSign * 1.5f
            },
            PlayOutcomeKind.Interception => groundCatch with { Z = groundCatch.Z - directionSign * 2.5f },
            _ => groundCatch
        };
    }

    private static SimulationVector3 BuildPassTarget(
        PlayOutcomeKind kind, SimulationVector3 catchPoint, SimulationVector3 routeFacing, int directionSign)
    {
        if (kind != PlayOutcomeKind.Incompletion)
            return catchPoint;
        var lateral = MathF.Abs(routeFacing.X) < 0.25f ? (catchPoint.X >= 0 ? 2.2f : -2.2f) : routeFacing.X * 2.2f;
        return new SimulationVector3(catchPoint.X + lateral, 0.14f, catchPoint.Z + directionSign * 1.8f);
    }

    private static RouteSample SampleRoute(OffensiveAssignment assignment, PlayPoint start, double time)
    {
        var origin = ToSimulation(start);
        if (assignment.Waypoints.Count == 0 || time <= assignment.ReleaseTime)
            return new RouteSample(origin, new SimulationVector3(0, 0, 1), 0,
                new RouteProgress(assignment.PlayerId, 0, 0, 0, assignment.Waypoints.Count == 0));
        var elapsed = (float)(time - assignment.ReleaseTime);
        var accelerationTime = assignment.MaximumSpeed / assignment.Acceleration;
        var distance = elapsed <= accelerationTime
            ? 0.5f * assignment.Acceleration * elapsed * elapsed
            : 0.5f * assignment.Acceleration * accelerationTime * accelerationTime +
              assignment.MaximumSpeed * (elapsed - accelerationTime);
        var totalRouteDistance = RouteDistance(origin, assignment.Waypoints);
        var speed = MathF.Min(assignment.MaximumSpeed, assignment.Acceleration * elapsed);
        speed = MathF.Min(speed,
            MathF.Sqrt(MathF.Max(0, 2 * assignment.Acceleration * (totalRouteDistance - distance))));
        var travelled = MathF.Min(distance, totalRouteDistance);
        var remaining = distance;
        var previous = origin;
        for (var index = 0; index < assignment.Waypoints.Count; index++)
        {
            var next = ToSimulation(assignment.Waypoints[index]);
            var segment = previous.DistanceTo(next);
            if (remaining <= segment || index == assignment.Waypoints.Count - 1)
            {
                var progress = segment <= 0.0001f ? 1 : Math.Clamp(remaining / segment, 0, 1);
                var position = SimulationVector3.Lerp(previous, next, progress);
                var complete = index == assignment.Waypoints.Count - 1 && progress >= 1;
                var facing = Direction(previous, next);
                if (index + 1 < assignment.Waypoints.Count && progress > 0.78f)
                {
                    var following = ToSimulation(assignment.Waypoints[index + 1]);
                    facing = Normalize(SimulationVector3.Lerp(facing,
                        Direction(next, following), (progress - 0.78f) / 0.22f));
                }
                return new RouteSample(position, facing, complete ? 0 : speed,
                    new RouteProgress(assignment.PlayerId, index, progress, travelled, complete));
            }
            remaining -= segment;
            previous = next;
        }
        return new RouteSample(previous, new SimulationVector3(0, 0, 1), 0,
            new RouteProgress(assignment.PlayerId, assignment.Waypoints.Count - 1, 1, travelled, true));
    }

    private static RouteClassification ClassifyRoute(
        PlayPoint start, IReadOnlyList<PlayPoint> route, FieldDirection direction)
    {
        if (route.Count == 0) return RouteClassification.CustomWaypoint;
        var sign = PlayDirectionResolver.Sign(direction);
        var end = route[^1];
        var lateral = end.X - start.X;
        var vertical = (end.Y - start.Y) * sign;
        if (route.Count >= 2)
        {
            var previous = route[^2];
            var finalVertical = (end.Y - previous.Y) * sign;
            if (finalVertical < -0.75f) return RouteClassification.Curl;
        }
        if (vertical >= 11 && MathF.Abs(lateral) <= 2.2f) return RouteClassification.Go;
        var towardSideline = MathF.Abs(start.X) > 0.5f && MathF.Sign(lateral) == MathF.Sign(start.X);
        if (vertical >= 7 && MathF.Abs(lateral) >= 3)
            return towardSideline ? RouteClassification.Corner : RouteClassification.Post;
        if (MathF.Abs(lateral) >= MathF.Max(2.5f, vertical * 0.75f) && vertical <= 7)
            return towardSideline ? RouteClassification.Out : RouteClassification.In;
        if (vertical > 2 && MathF.Abs(lateral) > 1.5f) return RouteClassification.Slant;
        return RouteClassification.CustomWaypoint;
    }

    private static double ResolveMilestoneThrowTime(
        OffensiveAssignment assignment, PlayPoint start, float routeProgress, double snapReceivedTime)
    {
        if (assignment.Waypoints.Count == 0)
            return snapReceivedTime + PlaySimulationSettings.Default.ThrowTime;
        var total = RouteDistance(ToSimulation(start), assignment.Waypoints);
        var minimum = snapReceivedTime + 0.25;
        for (var time = minimum; time <= snapReceivedTime + 8; time += FixedStepSeconds)
        {
            var progress = SampleRoute(assignment, start, time).Progress.DistanceTravelled / MathF.Max(0.001f, total);
            if (progress >= routeProgress) return time;
        }
        return snapReceivedTime + 8;
    }

    private static void ValidatePlay(PlayDefinition play, Team offense, Team defense)
    {
        if (!offense.Roster.Any(player => player.Id == play.QuarterbackId))
            throw new InvalidOperationException("The authored quarterback must belong to the offense.");
        if (!offense.Roster.Any(player => player.Id == play.IntendedReceiverId))
            throw new InvalidOperationException("The intended receiver must belong to the offense.");
        foreach (var player in offense.Roster.Concat(defense.Roster))
            if (!play.StartingPositions.ContainsKey(player.Id))
                throw new InvalidOperationException($"The play has no starting position for {player.Name}.");
    }

    private static float MaximumSpeed(Player player) => player.Position switch
    {
        PlayerPosition.Receiver => 6.9f,
        PlayerPosition.SlotReceiver => 6.6f,
        PlayerPosition.RunningBack => 6.3f,
        PlayerPosition.Quarterback => 5.3f,
        PlayerPosition.Center => 4.8f,
        _ => 6.2f
    };

    private static float Acceleration(Player player) => player.Position switch
    {
        PlayerPosition.Receiver or PlayerPosition.SlotReceiver => 10.5f,
        PlayerPosition.RunningBack => 10f,
        _ => 8.5f
    };

    private static (float Depth, double Duration) DropFor(PassArcPreset preset) => preset switch
    {
        PassArcPreset.Short => (0.8f, 0.3),
        PassArcPreset.Medium => (1.55f, 0.52),
        _ => (2.5f, 0.78)
    };

    private static double PostOutcomeDuration(PlayOutcomeKind outcome) => outcome switch
    {
        PlayOutcomeKind.Completion => 1.2,
        PlayOutcomeKind.FlagPullAfterCatch => 1.45,
        PlayOutcomeKind.Touchdown => 1.6,
        PlayOutcomeKind.OutOfBounds => 1.25,
        PlayOutcomeKind.Interception => 1.15,
        PlayOutcomeKind.DroppedPass => 0.72,
        _ => 0.55
    };

    private static bool IsCatchOutcome(PlayOutcomeKind outcome) => outcome is
        PlayOutcomeKind.Completion or PlayOutcomeKind.FlagPullAfterCatch or
        PlayOutcomeKind.Touchdown or PlayOutcomeKind.OutOfBounds;

    private static SimulationVector3 ToSimulation(PlayPoint point, float height = PlayerGroundHeight) =>
        new(point.X, height, point.Y);
    private static float Distance(PlayPoint a, PlayPoint b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static float RouteDistance(SimulationVector3 origin, IReadOnlyList<PlayPoint> route)
    {
        var total = 0f;
        var previous = origin;
        foreach (var point in route)
        {
            var next = ToSimulation(point, origin.Y);
            total += previous.DistanceTo(next);
            previous = next;
        }
        return total;
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    private static SimulationVector3 MoveTowards(SimulationVector3 from, SimulationVector3 to, float distance)
    {
        var total = from.DistanceTo(to);
        return total <= distance || total < 0.0001f ? to : SimulationVector3.Lerp(from, to, distance / total);
    }

    private static SimulationVector3 Direction(SimulationVector3 from, SimulationVector3 to)
    {
        var x = to.X - from.X;
        var z = to.Z - from.Z;
        var length = MathF.Sqrt(x * x + z * z);
        return length < 0.0001f ? new SimulationVector3(0, 0, 1)
            : new SimulationVector3(x / length, 0, z / length);
    }

    private static SimulationVector3 Normalize(SimulationVector3 direction)
    {
        var length = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        return length < 0.0001f ? new SimulationVector3(0, 0, 1)
            : new SimulationVector3(direction.X / length, 0, direction.Z / length);
    }

    private static SimulationVector3 Add(SimulationVector3 left, SimulationVector3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    private static SimulationVector3 Scale(SimulationVector3 value, float amount) =>
        new(value.X * amount, value.Y * amount, value.Z * amount);

    private sealed record RouteSample(
        SimulationVector3 Position, SimulationVector3 Facing, float Speed, RouteProgress Progress);
}
