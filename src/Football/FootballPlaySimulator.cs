using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public sealed class FootballPlaySimulator
{
    public const double FixedStepSeconds = 1d / 30d;
    private const double SnapTravelSeconds = 0.34;
    private const double QuarterbackSetSeconds = 0.42;

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
        var quarterbackSetTime = snapReceivedTime + QuarterbackSetSeconds;
        var assignments = BuildAssignments(play, offense, defense, routeReleaseTime, snapReceivedTime, settings, direction);
        var receiverAssignment = assignments.OfType<OffensiveAssignment>().Single(assignment => assignment.PlayerId == receiver.Id);
        var throwTime = settings.ThrowTrigger == ThrowTriggerMode.RouteMilestone
            ? ResolveMilestoneThrowTime(receiverAssignment, play.StartingPositions[receiver.Id], settings.ThrowRouteProgress, snapReceivedTime)
            : snapReceivedTime + settings.ThrowTime;
        var quarterbackStart = ToSimulation(play.StartingPositions[quarterback.Id], 0.08f);
        var quarterbackSet = quarterbackStart with { Z = quarterbackStart.Z - directionSign * 1.15f };
        var receiverAtThrow = SampleRoute(receiverAssignment, play.StartingPositions[receiver.Id], throwTime);
        var estimateDistance = quarterbackSet.DistanceTo(receiverAtThrow.Position);
        var flightSeconds = Math.Clamp(estimateDistance / settings.ThrowSpeed, 0.38f, 1.65f);
        var arrivalTime = throwTime + flightSeconds;
        var leadSample = SampleRoute(receiverAssignment, play.StartingPositions[receiver.Id], arrivalTime + settings.TargetLead);
        var catchPoint = leadSample.Position with { Y = 1.35f };
        var postOutcomeSeconds = settings.IntendedOutcome == PlayOutcomeKind.FlagPullAfterCatch ? 1.25 : 0.65;
        var endTime = arrivalTime + postOutcomeSeconds;
        var interceptorId = assignments.OfType<DefensiveAssignment>()
            .Where(assignment => assignment.CoverageType == DefensiveCoverageType.Man)
            .Select(assignment => (Guid?)assignment.PlayerId)
            .FirstOrDefault() ?? defense.Roster[0].Id;
        var outcome = BuildOutcome(settings.IntendedOutcome, play, receiver, interceptorId, catchPoint, directionSign, endTime);
        var events = BuildEvents(assignments, settings, center.Id, quarterback.Id, receiver.Id,
            snapTime, snapReceivedTime, quarterbackSetTime, throwTime, arrivalTime, endTime, outcome);

        var clock = new SimulationClock(FixedStepSeconds);
        var frames = new List<SimulationFrame>();
        var defenderPositions = defense.Roster.ToDictionary(
            player => player.Id,
            player => ToSimulation(play.StartingPositions[player.Id], 0.08f));
        var defenderSpeeds = defense.Roster.ToDictionary(player => player.Id, _ => 0f);
        var frameCount = (int)Math.Ceiling(endTime / FixedStepSeconds) + 1;
        for (var index = 0; index < frameCount; index++)
        {
            var time = index == frameCount - 1 ? endTime : clock.TimeSeconds;
            var playerStates = BuildPlayerStates(play, offense, defense, assignments, defenderPositions,
                defenderSpeeds, time, FixedStepSeconds, settings, directionSign, quarterback.Id,
                receiver.Id, center.Id, quarterbackStart, quarterbackSet, snapTime, snapReceivedTime, quarterbackSetTime,
                throwTime, arrivalTime, endTime, outcome);
            var ball = BuildBallState(play, settings, center.Id, quarterback.Id, receiver.Id, playerStates,
                time, snapTime, snapReceivedTime, throwTime, arrivalTime, catchPoint, outcome);
            var possession = BuildPossession(offense.Id, quarterback.Id, receiver.Id, time,
                snapTime, snapReceivedTime, throwTime, arrivalTime, defense.Id, outcome);
            frames.Add(new SimulationFrame(time,
                new ReadOnlyDictionary<Guid, PlayerSimulationState>(playerStates), ball, possession));
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
            var coverage = hasMan
                ? DefensiveCoverageType.Man
                : defender.Id == rusherId ? DefensiveCoverageType.Rusher : DefensiveCoverageType.Zone;
            var start = play.StartingPositions[defender.Id];
            var zone = PlayDirectionResolver.Advance(start, direction, -3);
            var reaction = coverage == DefensiveCoverageType.Rusher
                ? snapReceivedTime + settings.RusherDelay
                : routeReleaseTime + settings.DefensiveReactionDelay;
            assignments.Add(new DefensiveAssignment(defender.Id, 6.4f, 9.2f, coverage,
                hasMan ? target : null, zone, reaction));
        }
        return assignments;
    }

    private static Dictionary<Guid, PlayerSimulationState> BuildPlayerStates(
        PlayDefinition play, Team offense, Team defense, IReadOnlyList<PlayerAssignment> assignments,
        Dictionary<Guid, SimulationVector3> defenderPositions, Dictionary<Guid, float> defenderSpeeds,
        double time, double step, PlaySimulationSettings settings, int directionSign, Guid quarterbackId,
        Guid receiverId, Guid centerId, SimulationVector3 quarterbackStart, SimulationVector3 quarterbackSet,
        double snapTime, double snapReceivedTime, double quarterbackSetTime, double throwTime, double arrivalTime,
        double endTime, PlayOutcome outcome)
    {
        var states = new Dictionary<Guid, PlayerSimulationState>();
        var intendedRoute = assignments.OfType<OffensiveAssignment>().Single(item => item.PlayerId == receiverId);
        var intendedTarget = SampleRoute(intendedRoute, play.StartingPositions[receiverId], time).Position;
        foreach (var assignment in assignments.OfType<OffensiveAssignment>())
        {
            var start = play.StartingPositions[assignment.PlayerId];
            if (assignment.PlayerId == quarterbackId)
            {
                var dropProgress = SmoothStep((float)((time - snapReceivedTime) / (quarterbackSetTime - snapReceivedTime)));
                var position = SimulationVector3.Lerp(quarterbackStart, quarterbackSet, dropProgress);
                var facing = Direction(position, intendedTarget);
                var quarterbackMotion = time >= snapTime && time <= snapReceivedTime + 0.16
                    ? SimulationMotionState.Catch
                    : time >= throwTime - 0.12 && time <= throwTime + 0.38
                    ? SimulationMotionState.Throw
                    : time > snapReceivedTime && time < quarterbackSetTime ? SimulationMotionState.Jog : SimulationMotionState.Idle;
                states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, position, facing, 0, quarterbackMotion);
                continue;
            }

            var routeSampleTime = time;
            if (assignment.PlayerId == receiverId && time >= throwTime)
            {
                var leadBlend = (float)Math.Clamp((time - throwTime) / Math.Max(0.01, arrivalTime - throwTime), 0, 1);
                routeSampleTime += settings.TargetLead * leadBlend;
            }
            var sample = SampleRoute(assignment, start, routeSampleTime);
            var motion = sample.Speed > 5 ? SimulationMotionState.Sprint : sample.Speed > 0.1f ? SimulationMotionState.Jog : SimulationMotionState.Idle;
            if (assignment.PlayerId == centerId && time >= snapTime - 0.08 && time <= snapReceivedTime)
                motion = SimulationMotionState.Turn;
            if (assignment.PlayerId == receiverId && time >= arrivalTime - settings.CatchWindow * 0.5 && time <= arrivalTime + 0.35)
                motion = SimulationMotionState.Catch;
            states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, sample.Position,
                sample.Facing, sample.Speed, motion, sample.Progress);
        }

        foreach (var assignment in assignments.OfType<DefensiveAssignment>())
        {
            var current = defenderPositions[assignment.PlayerId];
            var target = current;
            if (time >= assignment.ReactionTime)
            {
                if (time >= arrivalTime && outcome.BallCarrierId.HasValue && states.TryGetValue(outcome.BallCarrierId.Value, out var carrier))
                    target = carrier.Position;
                else if (assignment.CoverageType == DefensiveCoverageType.Man && assignment.TargetPlayerId.HasValue &&
                         states.TryGetValue(assignment.TargetPlayerId.Value, out var covered))
                    target = covered.Position;
                else if (assignment.CoverageType == DefensiveCoverageType.Rusher && states.TryGetValue(quarterbackId, out var quarterback))
                    target = quarterback.Position;
                else
                    target = ToSimulation(assignment.ZoneLandmark, 0.08f);

                var speed = MathF.Min(assignment.MaximumSpeed,
                    defenderSpeeds[assignment.PlayerId] + assignment.Acceleration * (float)step);
                defenderSpeeds[assignment.PlayerId] = speed;
                current = MoveTowards(current, target, speed * (float)step);
                defenderPositions[assignment.PlayerId] = current;
            }

            var facing = Direction(current, target);
            var motion = defenderSpeeds[assignment.PlayerId] > 5 ? SimulationMotionState.Sprint
                : defenderSpeeds[assignment.PlayerId] > 0.1f ? SimulationMotionState.Jog : SimulationMotionState.Idle;
            if (settings.IntendedOutcome == PlayOutcomeKind.FlagPullAfterCatch && time >= endTime - 0.5)
            {
                var closest = assignments.OfType<DefensiveAssignment>()
                    .OrderBy(candidate => defenderPositions[candidate.PlayerId].DistanceTo(states[receiverId].Position)).First();
                if (closest.PlayerId == assignment.PlayerId)
                    motion = SimulationMotionState.FlagPull;
            }
            states[assignment.PlayerId] = new PlayerSimulationState(assignment.PlayerId, current, facing,
                defenderSpeeds[assignment.PlayerId], motion);
        }
        return states;
    }

    private static BallState BuildBallState(
        PlayDefinition play, PlaySimulationSettings settings, Guid centerId, Guid quarterbackId, Guid receiverId,
        IReadOnlyDictionary<Guid, PlayerSimulationState> players, double time, double snapTime, double snapReceivedTime,
        double throwTime, double arrivalTime, SimulationVector3 catchPoint, PlayOutcome outcome)
    {
        var center = ToSimulation(play.StartingPositions[centerId], 0.92f);
        var quarterback = players[quarterbackId].Position with { Y = 1.28f };
        if (time < snapTime)
            return new BallState(BallPhase.Ready, center, centerId, quarterbackId);
        if (time < snapReceivedTime)
        {
            var progress = (float)((time - snapTime) / (snapReceivedTime - snapTime));
            var position = SimulationVector3.Lerp(center, quarterback, progress);
            position = position with { Y = position.Y + MathF.Sin(progress * MathF.PI) * 0.16f };
            return new BallState(BallPhase.Snap, position, null, quarterbackId);
        }
        if (time < throwTime)
            return new BallState(BallPhase.HeldByQuarterback, quarterback, quarterbackId, receiverId);
        if (time < arrivalTime)
        {
            var progress = (float)((time - throwTime) / (arrivalTime - throwTime));
            var position = SimulationVector3.Lerp(quarterback, catchPoint, progress);
            var arc = settings.PassArc switch { PassArcPreset.Short => 1.1f, PassArcPreset.Medium => 2.2f, _ => 3.6f };
            position = position with { Y = position.Y + MathF.Sin(progress * MathF.PI) * arc };
            return new BallState(BallPhase.PassFlight, position, null, receiverId);
        }

        return outcome.Kind switch
        {
            PlayOutcomeKind.Interception => new BallState(BallPhase.Intercepted,
                players[outcome.BallCarrierId!.Value].Position with { Y = 1.3f }, outcome.BallCarrierId, outcome.BallCarrierId),
            PlayOutcomeKind.Incompletion => new BallState(BallPhase.Incomplete, catchPoint with { Y = 0.16f }, null, receiverId),
            PlayOutcomeKind.DroppedPass => new BallState(BallPhase.Dropped, catchPoint with { Y = 0.16f }, null, receiverId),
            _ => new BallState(BallPhase.Caught,
                players[receiverId].Position with { Y = 1.3f }, receiverId, receiverId)
        };
    }

    private static PossessionState BuildPossession(Guid offenseId, Guid quarterbackId, Guid receiverId,
        double time, double snapTime, double snapReceivedTime, double throwTime, double arrivalTime,
        Guid defenseTeamId, PlayOutcome outcome)
    {
        if (time < snapTime) return new PossessionState(PossessionPhase.PreSnap, offenseId, null);
        if (time < snapReceivedTime) return new PossessionState(PossessionPhase.Snap, offenseId, null);
        if (time < throwTime) return new PossessionState(PossessionPhase.Quarterback, offenseId, quarterbackId);
        if (time < arrivalTime) return new PossessionState(PossessionPhase.PassInFlight, offenseId, null);
        if (outcome.Kind == PlayOutcomeKind.Interception)
            return new PossessionState(PossessionPhase.Defender, defenseTeamId, outcome.BallCarrierId);
        if (outcome.Kind is PlayOutcomeKind.Incompletion or PlayOutcomeKind.DroppedPass)
            return new PossessionState(PossessionPhase.Loose, offenseId, null);
        return new PossessionState(PossessionPhase.Receiver, offenseId, receiverId);
    }

    private static List<SimulationEvent> BuildEvents(IReadOnlyList<PlayerAssignment> assignments,
        PlaySimulationSettings settings, Guid centerId, Guid quarterbackId, Guid receiverId,
        double snapTime, double snapReceivedTime, double quarterbackSetTime, double throwTime,
        double arrivalTime, double endTime, PlayOutcome outcome)
    {
        var events = new List<SimulationEvent> { new(0, SimulationEventType.PlayStarted, Description: "Pre-snap") };
        events.Add(new SimulationEvent(snapTime, SimulationEventType.SnapStarted, centerId, quarterbackId, "Snap"));
        foreach (var assignment in assignments.OfType<OffensiveAssignment>().Where(item => item.Waypoints.Count > 0))
            events.Add(new SimulationEvent(assignment.ReleaseTime, SimulationEventType.RouteReleased, assignment.PlayerId, Description: "Route release"));
        events.Add(new SimulationEvent(snapReceivedTime, SimulationEventType.SnapReceived, quarterbackId, centerId, "Quarterback received snap"));
        foreach (var assignment in assignments.OfType<DefensiveAssignment>())
            events.Add(new SimulationEvent(assignment.ReactionTime,
                assignment.CoverageType == DefensiveCoverageType.Rusher ? SimulationEventType.RusherReleased : SimulationEventType.DefenderReacted,
                assignment.PlayerId, assignment.TargetPlayerId, assignment.CoverageType.ToString()));
        events.Add(new SimulationEvent(quarterbackSetTime, SimulationEventType.QuarterbackSet, quarterbackId, Description: "Quarterback set"));
        events.Add(new SimulationEvent(throwTime, SimulationEventType.ThrowReleased, quarterbackId, receiverId, "Pass released"));
        events.Add(new SimulationEvent(arrivalTime - settings.CatchWindow * 0.5, SimulationEventType.CatchWindowOpened, receiverId, Description: "Catch window"));
        events.Add(outcome.Kind switch
        {
            PlayOutcomeKind.Interception => new SimulationEvent(arrivalTime, SimulationEventType.Intercepted, outcome.BallCarrierId, quarterbackId, outcome.Description),
            PlayOutcomeKind.Incompletion => new SimulationEvent(arrivalTime, SimulationEventType.PassIncomplete, receiverId, Description: outcome.Description),
            PlayOutcomeKind.DroppedPass => new SimulationEvent(arrivalTime, SimulationEventType.PassDropped, receiverId, Description: outcome.Description),
            _ => new SimulationEvent(arrivalTime, SimulationEventType.PassCompleted, receiverId, quarterbackId, outcome.Description)
        });
        if (outcome.Kind == PlayOutcomeKind.FlagPullAfterCatch)
        {
            events.Add(new SimulationEvent(endTime - 0.5, SimulationEventType.FlagPullAttempted, RelatedPlayerId: receiverId, Description: "Flag pull attempt"));
            events.Add(new SimulationEvent(endTime - 0.1, SimulationEventType.FlagPulled, receiverId, Description: "Flag pulled"));
        }
        if (outcome.Kind == PlayOutcomeKind.Touchdown)
            events.Add(new SimulationEvent(arrivalTime + 0.2, SimulationEventType.Touchdown, receiverId, Description: "Touchdown"));
        if (outcome.Kind == PlayOutcomeKind.OutOfBounds)
            events.Add(new SimulationEvent(arrivalTime + 0.2, SimulationEventType.OutOfBounds, receiverId, Description: "Out of bounds"));
        events.Add(new SimulationEvent(endTime, SimulationEventType.PlayEnded, outcome.BallCarrierId, Description: outcome.Description));
        return events.OrderBy(item => item.TimeSeconds).ThenBy(item => item.Type).ToList();
    }

    private static PlayOutcome BuildOutcome(PlayOutcomeKind kind, PlayDefinition play, Player receiver, Guid interceptorId,
        SimulationVector3 catchPoint, int directionSign, double endTime)
    {
        var start = play.StartingPositions[receiver.Id];
        var yards = Math.Max(0, (int)MathF.Round((catchPoint.Z - start.Y) * directionSign));
        if (kind is PlayOutcomeKind.Incompletion or PlayOutcomeKind.DroppedPass or PlayOutcomeKind.Interception)
            yards = 0;
        if (kind == PlayOutcomeKind.Touchdown)
            yards = Math.Max(yards, 20);
        var changes = kind is PlayOutcomeKind.Interception or PlayOutcomeKind.Touchdown;
        var carrier = kind == PlayOutcomeKind.Interception ? interceptorId
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
            _ => kind.ToString()
        };
        return new PlayOutcome(kind, yards, changes, kind == PlayOutcomeKind.Touchdown, endTime, carrier, description);
    }

    private static RouteSample SampleRoute(OffensiveAssignment assignment, PlayPoint start, double time)
    {
        var origin = ToSimulation(start, 0.08f);
        if (assignment.Waypoints.Count == 0 || time <= assignment.ReleaseTime)
            return new RouteSample(origin, new SimulationVector3(0, 0, 1), 0, new RouteProgress(assignment.PlayerId, 0, 0, 0, assignment.Waypoints.Count == 0));
        var elapsed = (float)(time - assignment.ReleaseTime);
        var accelerationTime = assignment.MaximumSpeed / assignment.Acceleration;
        var distance = elapsed <= accelerationTime
            ? 0.5f * assignment.Acceleration * elapsed * elapsed
            : 0.5f * assignment.Acceleration * accelerationTime * accelerationTime + assignment.MaximumSpeed * (elapsed - accelerationTime);
        var speed = MathF.Min(assignment.MaximumSpeed, assignment.Acceleration * elapsed);
        var totalRouteDistance = RouteDistance(origin, assignment.Waypoints);
        speed = MathF.Min(speed, MathF.Sqrt(MathF.Max(0, 2 * assignment.Acceleration * (totalRouteDistance - distance))));
        var travelled = distance;
        var previous = origin;
        for (var index = 0; index < assignment.Waypoints.Count; index++)
        {
            var next = ToSimulation(assignment.Waypoints[index], 0.08f);
            var segment = previous.DistanceTo(next);
            if (distance <= segment || index == assignment.Waypoints.Count - 1)
            {
                var progress = segment <= 0.0001f ? 1 : Math.Clamp(distance / segment, 0, 1);
                var position = SimulationVector3.Lerp(previous, next, progress);
                var complete = index == assignment.Waypoints.Count - 1 && progress >= 1;
                var facing = Direction(previous, next);
                if (index + 1 < assignment.Waypoints.Count && progress > 0.7f)
                {
                    var following = ToSimulation(assignment.Waypoints[index + 1], 0.08f);
                    facing = Normalize(SimulationVector3.Lerp(facing, Direction(next, following), (progress - 0.7f) / 0.3f));
                }
                return new RouteSample(position, facing, complete ? 0 : speed,
                    new RouteProgress(assignment.PlayerId, index, progress, MathF.Min(travelled, totalRouteDistance), complete));
            }
            distance -= segment;
            previous = next;
        }
        return new RouteSample(previous, new SimulationVector3(0, 0, 1), 0,
            new RouteProgress(assignment.PlayerId, assignment.Waypoints.Count - 1, 1, distance, true));
    }

    private static RouteClassification ClassifyRoute(PlayPoint start, IReadOnlyList<PlayPoint> route, FieldDirection direction)
    {
        if (route.Count == 0) return RouteClassification.CustomWaypoint;
        var end = route[^1];
        var lateral = end.X - start.X;
        var vertical = (end.Y - start.Y) * PlayDirectionResolver.Sign(direction);
        if (route.Count >= 2 && Math.Abs(vertical) < 2) return RouteClassification.Curl;
        if (vertical > 12 && Math.Abs(lateral) < 2) return RouteClassification.Go;
        if (vertical > 7 && Math.Abs(lateral) > 5) return lateral > 0 ? RouteClassification.Corner : RouteClassification.Post;
        if (vertical > 2 && Math.Abs(lateral) > 2) return RouteClassification.Slant;
        if (Math.Abs(lateral) > Math.Abs(vertical)) return lateral > 0 ? RouteClassification.Out : RouteClassification.In;
        return RouteClassification.CustomWaypoint;
    }

    private static double ResolveMilestoneThrowTime(OffensiveAssignment assignment, PlayPoint start,
        float routeProgress, double snapReceivedTime)
    {
        if (assignment.Waypoints.Count == 0)
            return snapReceivedTime + PlaySimulationSettings.Default.ThrowTime;
        var total = RouteDistance(ToSimulation(start, 0.08f), assignment.Waypoints);
        var minimum = snapReceivedTime + 0.35;
        for (var time = minimum; time <= snapReceivedTime + 8; time += FixedStepSeconds)
        {
            var progress = SampleRoute(assignment, start, time).Progress.DistanceTravelled / MathF.Max(0.001f, total);
            if (progress >= routeProgress)
                return time;
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

    private static SimulationVector3 ToSimulation(PlayPoint point, float height) => new(point.X, height, point.Y);
    private static float Distance(PlayPoint a, PlayPoint b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
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
    private static float SmoothStep(float value) { value = Math.Clamp(value, 0, 1); return value * value * (3 - 2 * value); }
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
        return length < 0.0001f ? new SimulationVector3(0, 0, 1) : new SimulationVector3(x / length, 0, z / length);
    }

    private static SimulationVector3 Normalize(SimulationVector3 direction)
    {
        var length = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        return length < 0.0001f ? new SimulationVector3(0, 0, 1) : new SimulationVector3(direction.X / length, 0, direction.Z / length);
    }

    private sealed record RouteSample(SimulationVector3 Position, SimulationVector3 Facing, float Speed, RouteProgress Progress);
}
