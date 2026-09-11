using System.Threading.Tasks;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlaySequenceController : Node
{
    private Node3D _receiver = null!;
    private Node3D _defender = null!;
    private Node3D _football = null!;
    private Node3D _footballHome = null!;
    private Label _status = null!;
    private Vector3 _receiverStart;
    private Vector3 _defenderStart;
    private Vector3 _ballStart;

    public bool IsRunning { get; private set; }

    public void Configure(
        Node3D receiver,
        Node3D defender,
        Node3D football,
        Node3D footballHome,
        Label status)
    {
        _receiver = receiver;
        _defender = defender;
        _football = football;
        _footballHome = footballHome;
        _status = status;
        _receiverStart = receiver.Position;
        _defenderStart = defender.Position;
        _ballStart = football.Position;
    }

    public async Task RunAsync(Node3D quarterback)
    {
        if (IsRunning)
            return;

        IsRunning = true;
        ResetPlay();

        _status.Text = "Snap";
        await MoveToAsync(_football, quarterback.GlobalPosition + new Vector3(0, 1.25f, 0), 0.45);

        _status.Text = "Receiver runs; defender covers";
        var receiverTarget = _receiverStart + new Vector3(1.5f, 0, 8.5f);
        var defenderTarget = receiverTarget + new Vector3(0.8f, 0, 1.3f);
        var route = CreateTween().SetParallel();
        route.TweenProperty(_receiver, "position", receiverTarget, 1.5).SetTrans(Tween.TransitionType.Sine);
        route.TweenProperty(_defender, "position", defenderTarget, 1.5).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(route, Tween.SignalName.Finished);

        _status.Text = "Throw";
        await ThrowToAsync(_receiver.GlobalPosition + new Vector3(0, 1.45f, 0), 0.85);

        _status.Text = "Catch!";
        _football.Reparent(_receiver, true);
        _football.Position = new Vector3(0.45f, 1.35f, -0.15f);
        await PauseAsync(0.35);

        _status.Text = "Defender closes in";
        await MoveToAsync(_defender, _receiver.Position + new Vector3(0.75f, 0, 0.75f), 0.55);

        _status.Text = "Play complete";
        IsRunning = false;
    }

    private void ResetPlay()
    {
        _receiver.Position = _receiverStart;
        _defender.Position = _defenderStart;
        if (_football.GetParent() != _footballHome)
            _football.Reparent(_footballHome);
        _football.Position = _ballStart;
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
}
