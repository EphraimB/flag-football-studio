using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlaySequenceController : Node
{
    private readonly FootballPlaySimulator _simulator = new();
    private readonly FootballAnimationQualityLayer _animationQuality = new();
    private PlayDefinition _play = null!;
    private IReadOnlyDictionary<Guid, Node3D> _pawns = null!;
    private Node3D _football = null!;
    private Node3D _footballHome = null!;
    private Label _status = null!;
    private Team _offense = null!;
    private Team _defense = null!;
    private Vector3 _ballStart;
    private int _nextEventIndex;
    private Guid? _attachedBallPlayerId;
    private Node3D? _attachedBallAnchor;

    public bool IsRunning { get; private set; }
    public PlaySimulation? LastSimulation { get; private set; }
    public FootballAnimationTimeline? LastAnimationTimeline { get; private set; }
    public event Action<PlaySimulation>? SimulationStarted;
    public event Action<SimulationFrame>? SimulationFrameApplied;
    public event Action<SimulationEvent>? SimulationEventApplied;

    public void Configure(
        PlayDefinition play,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        Node3D football,
        Node3D footballHome,
        Label status,
        Team offense,
        Team defense)
    {
        _play = play;
        _pawns = pawns;
        _football = football;
        _footballHome = footballHome;
        _status = status;
        _offense = offense;
        _defense = defense;
        _ballStart = football.Position;
    }

    public void SetPlay(PlayDefinition play, Vector3 ballStart, Team offense, Team defense)
    {
        _play = play;
        _ballStart = ballStart;
        _offense = offense;
        _defense = defense;
    }

    public async Task<PlayOutcome?> RunAsync()
    {
        if (IsRunning)
            return null;

        IsRunning = true;
        try
        {
            ResetPlay();
            LastSimulation = _simulator.Simulate(_play, _offense, _defense);
            LastAnimationTimeline = _animationQuality.Build(LastSimulation);
            SimulationStarted?.Invoke(LastSimulation);
            _nextEventIndex = 0;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < LastSimulation.DurationSeconds)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                ApplyFrame(LastSimulation.FrameAt(elapsed));
                ApplyEventsThrough(LastSimulation, elapsed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            ApplyFrame(LastSimulation.Frames[^1]);
            ApplyEventsThrough(LastSimulation, LastSimulation.DurationSeconds);
            _status.Text = LastSimulation.Outcome.Description;
            return LastSimulation.Outcome;
        }
        finally
        {
            SetAllAnimationStates(HumanoidAnimationState.Idle);
            IsRunning = false;
        }
    }

    private void ApplyFrame(SimulationFrame frame)
    {
        var animationFrame = LastAnimationTimeline?.FrameAt(frame.TimeSeconds);
        foreach (var state in frame.Players.Values)
        {
            var pawn = Pawn(state.PlayerId, "simulation player");
            pawn.Position = ToGodot(state.Position);
            if (state.FacingDirection.X * state.FacingDirection.X + state.FacingDirection.Z * state.FacingDirection.Z > 0.0001f && pawn is PlayerPawn playerPawn)
                playerPawn.FaceToward(pawn.GlobalPosition + ToGodot(state.FacingDirection), 0);
            if (pawn is PlayerPawn animatedPawn && animationFrame is not null)
                animatedPawn.ApplyAnimationCue(animationFrame.Players[state.PlayerId]);
            else
                SetAnimation(pawn, ToAnimation(state.MotionState));
        }

        ApplyBall(frame.Ball);
        SimulationFrameApplied?.Invoke(frame);
    }

    private void ApplyBall(BallState ball)
    {
        if (ball.Phase is BallPhase.HeldByQuarterback or BallPhase.Caught or BallPhase.Intercepted &&
            ball.PossessingPlayerId.HasValue)
        {
            var owner = Pawn(ball.PossessingPlayerId.Value, "ball carrier");
            var anchor = owner;
            if (owner is PlayerPawn pawn)
            {
                var interaction = FootballInteractionResolver.Resolve(ball, pawn.AnimationState);
                pawn.SetFootballInteractionMode(interaction);
                anchor = interaction switch
                {
                    FootballInteractionMode.QuarterbackHold => pawn.QuarterbackHoldAnchor,
                    FootballInteractionMode.ThrowingHand => pawn.ThrowAnchor,
                    FootballInteractionMode.CatchHands => pawn.CatchAnchor,
                    FootballInteractionMode.Carry => pawn.CarryAnchor,
                    _ => pawn.CatchAnchor
                };
            }
            if (_attachedBallPlayerId != ball.PossessingPlayerId || _attachedBallAnchor != anchor)
            {
                _football.Reparent(anchor, false);
                _football.Position = Vector3.Zero;
                _attachedBallPlayerId = ball.PossessingPlayerId;
                _attachedBallAnchor = anchor;
            }
            return;
        }

        if (_football.GetParent() != _footballHome)
            _football.Reparent(_footballHome, true);
        _attachedBallPlayerId = null;
        _attachedBallAnchor = null;
        _football.GlobalPosition = ToGodot(ball.Position);
    }

    private void ApplyEventsThrough(PlaySimulation simulation, double elapsed)
    {
        while (_nextEventIndex < simulation.Events.Count && simulation.Events[_nextEventIndex].TimeSeconds <= elapsed)
        {
            var simulationEvent = simulation.Events[_nextEventIndex++];
            _status.Text = string.IsNullOrWhiteSpace(simulationEvent.Description)
                ? simulationEvent.Type.ToString()
                : simulationEvent.Description;

            if (simulationEvent.Type == SimulationEventType.ThrowReleased && simulationEvent.PlayerId.HasValue)
                RestartAnimationCue(simulation, simulationEvent, "quarterback");
            else if (simulationEvent.Type == SimulationEventType.PassCompleted && simulationEvent.PlayerId.HasValue)
                RestartAnimationCue(simulation, simulationEvent, "receiver");
            else if (simulationEvent.Type == SimulationEventType.PassDropped && simulationEvent.PlayerId.HasValue)
                RestartAnimationCue(simulation, simulationEvent, "receiver");
            else if (simulationEvent.Type == SimulationEventType.Intercepted && simulationEvent.PlayerId.HasValue)
                RestartAnimationCue(simulation, simulationEvent, "interceptor");
            else if (simulationEvent.Type == SimulationEventType.FlagPullAttempted)
            {
                foreach (var state in simulation.FrameAt(simulationEvent.TimeSeconds).Players.Values)
                    if (state.MotionState == SimulationMotionState.FlagPull)
                        SetAnimation(Pawn(state.PlayerId, "flag puller"), HumanoidAnimationState.FlagPull, true);
            }
            else if (simulationEvent.Type == SimulationEventType.Touchdown && simulationEvent.PlayerId.HasValue)
                RestartAnimationCue(simulation, simulationEvent, "touchdown scorer");
            SimulationEventApplied?.Invoke(simulationEvent);
        }
    }

    private void RestartAnimationCue(PlaySimulation simulation, SimulationEvent simulationEvent, string role)
    {
        if (!simulationEvent.PlayerId.HasValue)
            return;
        var pawn = Pawn(simulationEvent.PlayerId.Value, role);
        if (pawn is PlayerPawn playerPawn && LastAnimationTimeline is not null)
            playerPawn.ApplyAnimationCue(
                LastAnimationTimeline.FrameAt(simulationEvent.TimeSeconds).Players[simulationEvent.PlayerId.Value], true);
        else
            SetAnimation(pawn, ToAnimation(simulation.FrameAt(simulationEvent.TimeSeconds)
                .Players[simulationEvent.PlayerId.Value].MotionState), true);
    }

    private void ResetPlay()
    {
        foreach (var startingPosition in _play.StartingPositions)
        {
            var pawn = Pawn(startingPosition.Key, "formation player");
            pawn.Position = new Vector3(startingPosition.Value.X, 0.08f, startingPosition.Value.Y);
            if (pawn is PlayerPawn playerPawn)
                playerPawn.ResetPresentationPose();
        }
        FormationFacing.Apply(_play, _offense, _defense, _pawns);

        if (_football.GetParent() != _footballHome)
            _football.Reparent(_footballHome, false);
        _football.Position = _ballStart;
        _attachedBallPlayerId = null;
        _attachedBallAnchor = null;
    }

    private Node3D Pawn(Guid playerId, string role)
    {
        if (playerId == Guid.Empty || !_pawns.TryGetValue(playerId, out var pawn))
            throw new InvalidOperationException($"The play does not define a valid {role}.");
        return pawn;
    }

    private void SetAllAnimationStates(HumanoidAnimationState state)
    {
        foreach (var pawn in _pawns.Values)
            SetAnimation(pawn, state);
    }

    private static void SetAnimation(Node3D node, HumanoidAnimationState state, bool restart = false)
    {
        if (node is PlayerPawn pawn && (restart || pawn.AnimationState != state))
            pawn.SetAnimationState(state, restart);
    }

    private static HumanoidAnimationState ToAnimation(SimulationMotionState state) => state switch
    {
        SimulationMotionState.Jog => HumanoidAnimationState.Jog,
        SimulationMotionState.Sprint => HumanoidAnimationState.Sprint,
        SimulationMotionState.Turn => HumanoidAnimationState.Turn,
        SimulationMotionState.Throw => HumanoidAnimationState.Throw,
        SimulationMotionState.Catch => HumanoidAnimationState.Catch,
        SimulationMotionState.FlagPull => HumanoidAnimationState.FlagPull,
        _ => HumanoidAnimationState.Idle
    };

    private static Vector3 ToGodot(SimulationVector3 value) => new(value.X, value.Y, value.Z);
}
