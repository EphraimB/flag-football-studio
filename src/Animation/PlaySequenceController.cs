using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlaySequenceController : Node
{
    private PlayDefinition _play = null!;
    private IReadOnlyDictionary<Guid, Node3D> _pawns = null!;
    private Node3D _football = null!;
    private Node3D _footballHome = null!;
    private Label _status = null!;
    private Team _offense = null!;
    private Team _defense = null!;
    private Vector3 _ballStart;

    public bool IsRunning { get; private set; }

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

    public async Task RunAsync()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        try
        {
            ResetPlay();
            var quarterback = Pawn(_play.QuarterbackId, "quarterback");
            var receiver = Pawn(_play.IntendedReceiverId, "intended receiver");
            var center = FindCenterFor(quarterback);

            _status.Text = "Snap";
            SetAnimation(center, HumanoidAnimationState.Turn, true);
            SetAnimation(quarterback, HumanoidAnimationState.Catch, true);
            await MoveToAsync(_football, quarterback.GlobalPosition + new Vector3(0, 1.25f, 0), 0.45);
            SetAnimation(center, HumanoidAnimationState.Idle);
            SetAnimation(quarterback, HumanoidAnimationState.Idle);

            _status.Text = "Routes and coverage";
            var movements = BuildRouteMovements();
            if (movements.Count > 0)
                await Task.WhenAll(movements);

            _status.Text = "Throw";
            FaceToward(quarterback, receiver.GlobalPosition);
            SetAnimation(quarterback, HumanoidAnimationState.Turn, true);
            await PauseAsync(0.16);
            SetAnimation(quarterback, HumanoidAnimationState.Throw, true);
            var catchTarget = receiver is PlayerPawn receiverPawn
                ? receiverPawn.CatchAnchor.GlobalPosition
                : receiver.GlobalPosition + new Vector3(0, 1.45f, 0);
            await ThrowToAsync(catchTarget, 0.85);
            SetAnimation(quarterback, HumanoidAnimationState.Idle);

            _status.Text = "Catch!";
            SetAnimation(receiver, HumanoidAnimationState.Catch, true);
            var catchAnchor = receiver is PlayerPawn catchingPawn ? catchingPawn.CatchAnchor : receiver;
            _football.Reparent(catchAnchor, false);
            _football.Position = Vector3.Zero;
            await PauseAsync(0.35);

            var closingDefenders = _play.CoverageAssignments
                .Where(assignment => assignment.Value == _play.IntendedReceiverId)
                .Select(assignment => MovePawnToAsync(
                    Pawn(assignment.Key, "defender"),
                    receiver.GlobalPosition + new Vector3(0.75f, 0, 0.75f),
                    0.55,
                    HumanoidAnimationState.Sprint))
                .ToArray();
            if (closingDefenders.Length > 0)
            {
                _status.Text = "Defender closes in";
                await Task.WhenAll(closingDefenders);
                foreach (var assignment in _play.CoverageAssignments.Where(pair => pair.Value == _play.IntendedReceiverId))
                    SetAnimation(Pawn(assignment.Key, "defender"), HumanoidAnimationState.FlagPull, true);
                SetAnimation(receiver, HumanoidAnimationState.Turn, true);
                await PauseAsync(0.55);
            }

            _status.Text = "Play complete";
        }
        finally
        {
            SetAllAnimationStates(HumanoidAnimationState.Idle);
            IsRunning = false;
        }
    }

    private void ResetPlay()
    {
        foreach (var startingPosition in _play.StartingPositions)
        {
            var pawn = Pawn(startingPosition.Key, "formation player");
            pawn.Position = ToWorld(startingPosition.Value);
            if (pawn is PlayerPawn playerPawn)
                playerPawn.ResetPresentationPose();
        }
        FormationFacing.Apply(_play, _offense, _defense, _pawns);

        if (_football.GetParent() != _footballHome)
            _football.Reparent(_footballHome, false);
        _football.Position = _ballStart;
    }

    private List<Task> BuildRouteMovements()
    {
        var movements = new List<Task>();
        foreach (var route in _play.Routes)
        {
            if (route.Value.Count > 0)
                movements.Add(AnimatePathAsync(Pawn(route.Key, "route runner"), route.Value, HumanoidAnimationState.Sprint));
        }

        foreach (var assignment in _play.CoverageAssignments)
        {
            if (!_play.Routes.TryGetValue(assignment.Value, out var coveredRoute) || coveredRoute.Count == 0)
                continue;

            var defenderStart = _play.StartingPositions[assignment.Key];
            var offenseStart = _play.StartingPositions[assignment.Value];
            var offset = new PlayPoint(defenderStart.X - offenseStart.X, defenderStart.Y - offenseStart.Y);
            var coveragePath = coveredRoute
                .Select(point => new PlayPoint(point.X + offset.X * 0.45f, point.Y + offset.Y * 0.45f))
                .ToArray();
            movements.Add(AnimatePathAsync(Pawn(assignment.Key, "defender"), coveragePath, HumanoidAnimationState.Jog));
        }
        return movements;
    }

    private async Task AnimatePathAsync(Node3D node, IReadOnlyList<PlayPoint> path, HumanoidAnimationState locomotionState)
    {
        SetAnimation(node, locomotionState, true);
        foreach (var waypoint in path)
        {
            FaceToward(node, ToWorld(waypoint));
            var tween = CreateTween();
            tween.TweenProperty(node, "position", ToWorld(waypoint), 0.55).SetTrans(Tween.TransitionType.Sine);
            await ToSignal(tween, Tween.SignalName.Finished);
        }
        SetAnimation(node, HumanoidAnimationState.Idle);
    }

    private async Task MovePawnToAsync(Node3D node, Vector3 target, double duration, HumanoidAnimationState locomotionState)
    {
        FaceToward(node, target);
        SetAnimation(node, locomotionState, true);
        await MoveToAsync(node, target, duration);
        SetAnimation(node, HumanoidAnimationState.Idle);
    }

    private async Task MoveToAsync(Node3D node, Vector3 target, double duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(node, "global_position", target, duration).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task ThrowToAsync(Vector3 target, double duration)
    {
        var start = _football.GlobalPosition;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(progress =>
        {
            var position = start.Lerp(target, progress);
            position.Y += Mathf.Sin(progress * Mathf.Pi) * 2.2f;
            _football.GlobalPosition = position;
        }), 0f, 1f, duration).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task PauseAsync(double duration) =>
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);

    private Node3D Pawn(Guid playerId, string role)
    {
        if (playerId == Guid.Empty || !_pawns.TryGetValue(playerId, out var pawn))
            throw new InvalidOperationException($"The play does not define a valid {role}.");
        return pawn;
    }

    private Node3D? FindCenterFor(Node3D quarterback)
    {
        var quarterbackTeam = (quarterback as PlayerPawn)?.Player?.Team;
        return _pawns.Values
            .OfType<PlayerPawn>()
            .FirstOrDefault(pawn => pawn.Player?.Position == PlayerPosition.Center && pawn.Player.Team == quarterbackTeam);
    }

    private void SetAllAnimationStates(HumanoidAnimationState state)
    {
        foreach (var pawn in _pawns.Values)
            SetAnimation(pawn, state);
    }

    private static void SetAnimation(Node3D? node, HumanoidAnimationState state, bool restart = false)
    {
        if (node is PlayerPawn pawn)
            pawn.SetAnimationState(state, restart);
    }

    private static void FaceToward(Node3D node, Vector3 target)
    {
        if (node is PlayerPawn pawn)
            pawn.FaceToward(target);
    }

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
}
