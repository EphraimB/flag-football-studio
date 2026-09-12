using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayDirectorPanel : Control
{
    private enum EditorMode
    {
        Move,
        Route,
        Coverage,
        Quarterback,
        Target
    }

    private readonly Dictionary<Guid, Player> _players = [];
    private readonly HashSet<Guid> _goldIds = [];
    private readonly HashSet<Guid> _navyIds = [];
    private readonly HashSet<Guid> _offenseIds = [];
    private readonly HashSet<Guid> _defenseIds = [];
    private readonly List<Button> _buttons = [];
    private Team _gold = null!;
    private Team _navy = null!;
    private Team _offense = null!;
    private Team _defense = null!;
    private PlayDefinition _play = null!;
    private EditorMode _mode = EditorMode.Move;
    private Guid? _selectedPlayerId;
    private Guid? _draggedPlayerId;
    private Label _instructionLabel = null!;

    public event Action? FormationChanged;
    public event Action? ResetRequested;
    public event Action? RunRequested;

    public void Configure(Game game, PlayDefinition play, Guid possessionTeamId)
    {
        SetRoster(game, possessionTeamId);
        _play = play;
        BuildToolbar();
        UpdateInstructions();
        QueueRedraw();
    }

    public void SetGameAndPlay(Game game, PlayDefinition play, Guid possessionTeamId)
    {
        SetRoster(game, possessionTeamId);
        SetPlay(play);
    }

    private void SetRoster(Game game, Guid possessionTeamId)
    {
        _gold = game.Gold;
        _navy = game.Navy;
        _players.Clear();
        _goldIds.Clear();
        _navyIds.Clear();
        foreach (var player in game.Gold.Roster)
        {
            _players.Add(player.Id, player);
            _goldIds.Add(player.Id);
        }
        foreach (var player in game.Navy.Roster)
        {
            _players.Add(player.Id, player);
            _navyIds.Add(player.Id);
        }
        SetPossession(possessionTeamId);
    }

    public void SetPossession(Guid possessionTeamId)
    {
        _offense = possessionTeamId == _gold.Id
            ? _gold
            : possessionTeamId == _navy.Id
                ? _navy
                : throw new ArgumentException("Possession must belong to Gold or Navy.", nameof(possessionTeamId));
        _defense = _offense.Id == _gold.Id ? _navy : _gold;
        _offenseIds.Clear();
        _defenseIds.Clear();
        foreach (var player in _offense.Roster)
            _offenseIds.Add(player.Id);
        foreach (var player in _defense.Roster)
            _defenseIds.Add(player.Id);
        QueueRedraw();
    }

    public void SetPlay(PlayDefinition play)
    {
        _play = play;
        _selectedPlayerId = null;
        _draggedPlayerId = null;
        UpdateInstructions();
        QueueRedraw();
    }

    public void SetEditingEnabled(bool enabled)
    {
        _draggedPlayerId = null;
        foreach (var button in _buttons)
            button.Disabled = !enabled;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    public void RefreshAppearance() => QueueRedraw();

    public override void _Draw()
    {
        var field = FieldRect();
        DrawStyleBox(GetThemeStylebox("panel", "Panel"), new Rect2(Vector2.Zero, Size));
        DrawRect(field, new Color("205f37"));

        for (var yard = -15; yard <= 15; yard += 5)
        {
            var start = FieldToCanvas(new PlayPoint(-10, yard));
            var end = FieldToCanvas(new PlayPoint(10, yard));
            DrawLine(start, end, new Color(1, 1, 1, 0.45f), 1.5f);
        }
        DrawRect(field, new Color("f4f0dc"), false, 2);

        foreach (var route in _play.Routes)
        {
            if (!_play.StartingPositions.TryGetValue(route.Key, out var start) || route.Value.Count == 0)
                continue;

            var points = new List<Vector2> { FieldToCanvas(start) };
            points.AddRange(route.Value.Select(FieldToCanvas));
            DrawPolyline(points.ToArray(), TeamColor(route.Key).Lightened(0.25f), 3, true);
            foreach (var waypoint in route.Value)
                DrawCircle(FieldToCanvas(waypoint), 4, Colors.White);
        }

        foreach (var assignment in _play.CoverageAssignments)
        {
            if (!_play.StartingPositions.TryGetValue(assignment.Key, out var defender) ||
                !_play.StartingPositions.TryGetValue(assignment.Value, out var offense))
                continue;
            DrawDashedLine(FieldToCanvas(defender), FieldToCanvas(offense), new Color("ff8ca0"), 2, 8);
        }

        foreach (var entry in _play.StartingPositions)
            DrawPlayer(entry.Key, entry.Value);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            if (button.Pressed)
                HandlePress(button.Position);
            else
                _draggedPlayerId = null;
            AcceptEvent();
            return;
        }

        if (inputEvent is InputEventMouseMotion motion && _draggedPlayerId.HasValue)
        {
            _play.SetStartingPosition(_draggedPlayerId.Value, CanvasToField(motion.Position));
            FormationChanged?.Invoke();
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void HandlePress(Vector2 position)
    {
        if (!FieldRect().HasPoint(position))
            return;

        var hit = PlayerAt(position);
        switch (_mode)
        {
            case EditorMode.Move:
                if (hit.HasValue)
                {
                    _selectedPlayerId = hit;
                    _draggedPlayerId = hit;
                }
                break;
            case EditorMode.Route:
                if (hit.HasValue && _offenseIds.Contains(hit.Value))
                {
                    _selectedPlayerId = hit;
                }
                else if (_selectedPlayerId.HasValue && _offenseIds.Contains(_selectedPlayerId.Value))
                {
                    var waypoints = _play.Routes.TryGetValue(_selectedPlayerId.Value, out var route)
                        ? route.ToList()
                        : [];
                    waypoints.Add(CanvasToField(position));
                    _play.SetRoute(_selectedPlayerId.Value, waypoints);
                }
                break;
            case EditorMode.Coverage:
                if (hit.HasValue && _defenseIds.Contains(hit.Value))
                    _selectedPlayerId = hit;
                else if (hit.HasValue && _offenseIds.Contains(hit.Value) && _selectedPlayerId.HasValue && _defenseIds.Contains(_selectedPlayerId.Value))
                    _play.AssignCoverage(_selectedPlayerId.Value, hit.Value);
                break;
            case EditorMode.Quarterback:
                if (hit.HasValue && _offenseIds.Contains(hit.Value))
                {
                    _selectedPlayerId = hit;
                    _play.SetQuarterback(hit.Value);
                }
                break;
            case EditorMode.Target:
                if (hit.HasValue && _offenseIds.Contains(hit.Value))
                {
                    _selectedPlayerId = hit;
                    _play.SetIntendedReceiver(hit.Value);
                }
                break;
        }

        UpdateInstructions();
        QueueRedraw();
    }

    private void BuildToolbar()
    {
        _buttons.Clear();
        var stack = new VBoxContainer
        {
            Position = new Vector2(12, 10),
            Size = new Vector2(Size.X - 24, 100)
        };
        AddChild(stack);

        var title = new Label { Text = "PLAY DIRECTOR", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 20);
        stack.AddChild(title);

        var tools = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(tools);
        AddModeButton(tools, "Move", EditorMode.Move);
        AddModeButton(tools, "Route", EditorMode.Route);
        AddModeButton(tools, "Coverage", EditorMode.Coverage);
        AddModeButton(tools, "Set QB", EditorMode.Quarterback);
        AddModeButton(tools, "Target", EditorMode.Target);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(actions);
        AddActionButton(actions, "Clear Route", ClearSelectedRoute);
        AddActionButton(actions, "Reset", () => ResetRequested?.Invoke());
        AddActionButton(actions, "Run Play", () => RunRequested?.Invoke());

        _instructionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        stack.AddChild(_instructionLabel);
    }

    private void AddModeButton(Node parent, string text, EditorMode mode) =>
        AddActionButton(parent, text, () => SetMode(mode));

    private void AddActionButton(Node parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        _buttons.Add(button);
    }

    private void SetMode(EditorMode mode)
    {
        _mode = mode;
        _draggedPlayerId = null;
        UpdateInstructions();
    }

    private void ClearSelectedRoute()
    {
        if (_selectedPlayerId.HasValue && _offenseIds.Contains(_selectedPlayerId.Value))
        {
            _play.SetRoute(_selectedPlayerId.Value, Array.Empty<PlayPoint>());
            QueueRedraw();
        }
    }

    private void UpdateInstructions()
    {
        if (_instructionLabel is null)
            return;

        var selected = _selectedPlayerId.HasValue && _players.TryGetValue(_selectedPlayerId.Value, out var player)
            ? $" Selected: {player.Name} #{player.JerseyNumber}."
            : string.Empty;
        _instructionLabel.Text = _mode switch
        {
            EditorMode.Move => "Drag any player into formation." + selected,
            EditorMode.Route => $"Select {_offense.Name}, then click field waypoints." + selected,
            EditorMode.Coverage => $"Select {_defense.Name}, then the {_offense.Name} player covered." + selected,
            EditorMode.Quarterback => $"Click a {_offense.Name} player to select the quarterback." + selected,
            EditorMode.Target => $"Click a {_offense.Name} player to select the target." + selected,
            _ => string.Empty
        };
    }

    private void DrawPlayer(Guid playerId, PlayPoint point)
    {
        var center = FieldToCanvas(point);
        var selected = _selectedPlayerId == playerId;
        if (selected)
            DrawCircle(center, 19, Colors.White);
        DrawCircle(center, 15, TeamColor(playerId));

        var attackDirection = PlayDirectionResolver.Resolve(_play, _offense, _defense);
        var facingDirection = _offenseIds.Contains(playerId)
            ? attackDirection
            : attackDirection == FieldDirection.PositiveY
                ? FieldDirection.NegativeY
                : FieldDirection.PositiveY;
        var facingTip = FieldToCanvas(PlayDirectionResolver.Advance(point, facingDirection, 1.7f));
        DrawLine(center, facingTip, Colors.White, 3, true);
        DrawCircle(facingTip, 3.5f, Colors.White);

        var jersey = _players[playerId].JerseyNumber.ToString();
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-12, 5), jersey, HorizontalAlignment.Center, 24, 14, Colors.White);
        if (_play.QuarterbackId == playerId)
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-20, -19), "QB", HorizontalAlignment.Center, 40, 12, Colors.White);
        if (_play.IntendedReceiverId == playerId)
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-20, 29), "TARGET", HorizontalAlignment.Center, 40, 10, Colors.White);
    }

    private Guid? PlayerAt(Vector2 position)
    {
        foreach (var entry in _play.StartingPositions.Reverse())
        {
            if (FieldToCanvas(entry.Value).DistanceTo(position) <= 20)
                return entry.Key;
        }
        return null;
    }

    private Color TeamColor(Guid playerId) =>
        _goldIds.Contains(playerId) ? new Color("d8a91b") : new Color("173b73");

    private Rect2 FieldRect() => new(16, 128, Mathf.Max(100, Size.X - 32), Mathf.Max(100, Size.Y - 144));

    private Vector2 FieldToCanvas(PlayPoint point)
    {
        var field = FieldRect();
        return new Vector2(
            field.Position.X + ((point.X + 10) / 20f * field.Size.X),
            field.Position.Y + ((20 - point.Y) / 40f * field.Size.Y));
    }

    private PlayPoint CanvasToField(Vector2 point)
    {
        var field = FieldRect();
        var x = Mathf.Clamp(((point.X - field.Position.X) / field.Size.X * 20f) - 10f, -10f, 10f);
        var y = Mathf.Clamp(20f - ((point.Y - field.Position.Y) / field.Size.Y * 40f), -20f, 20f);
        return new PlayPoint(x, y);
    }
}
