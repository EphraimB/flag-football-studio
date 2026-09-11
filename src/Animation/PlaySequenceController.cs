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
    private Vector3 _ballStart;

    public bool IsRunning { get; private set; }

    public void Configure(
        PlayDefinition play,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        Node3D football,
        Node3D footballHome,
        Label status)
    {
        _play = play;
        _pawns = pawns;
        _football = football;
        _footballHome = footballHome;
        _status = status;
        _ballStart = football.Position;
    }

    public void SetPlay(PlayDefinition play, Vector3 ballStart)
    {
        _play = play;
        _ballStart = ballStart;
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

            _status.Text = "Snap";
            await MoveToAsync(_football, quarterback.GlobalPosition + new Vector3(0, 1.25f, 0), 0.45);

            _status.Text = "Routes and coverage";
            var movements = BuildRouteMovements();
            if (movements.Count > 0)
                await Task.WhenAll(movements);

            _status.Text = "Throw";
            await ThrowToAsync(receiver.GlobalPosition + new Vector3(0, 1.45f, 0), 0.85);

            _status.Text = "Catch!";
            _football.Reparent(receiver, true);
            _football.Position = new Vector3(0.45f, 1.35f, -0.15f);
            await PauseAsync(0.35);

            var closingDefenders = _play.CoverageAssignments
                .Where(assignment => assignment.Value == _play.IntendedReceiverId)
                .Select(assignment => MoveToAsync(Pawn(assignment.Key, "defender"), receiver.Position + new Vector3(0.75f, 0, 0.75f), 0.55))
                .ToArray();
            if (closingDefenders.Length > 0)
            {
                _status.Text = "Defender closes in";
                await Task.WhenAll(closingDefenders);
            }

            _status.Text = "Play complete";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ResetPlay()
    {
        foreach (var startingPosition in _play.StartingPositions)
            Pawn(startingPosition.Key, "formation player").Position = ToWorld(startingPosition.Value);

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
                movements.Add(AnimatePathAsync(Pawn(route.Key, "route runner"), route.Value));
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
            movements.Add(AnimatePathAsync(Pawn(assignment.Key, "defender"), coveragePath));
        }
        return movements;
    }

    private async Task AnimatePathAsync(Node3D node, IReadOnlyList<PlayPoint> path)
    {
        var tween = CreateTween();
        foreach (var waypoint in path)
            tween.TweenProperty(node, "position", ToWorld(waypoint), 0.55).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(tween, Tween.SignalName.Finished);
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

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
}
