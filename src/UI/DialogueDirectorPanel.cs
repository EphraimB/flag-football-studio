using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class DialogueDirectorPanel : PanelContainer
{
    private readonly List<Guid> _sequenceIds = [];
    private readonly List<Guid> _lineIds = [];
    private readonly List<Guid> _playerIds = [];
    private GameProject _project = null!;
    private Guid _playId;
    private Guid _selectedSequenceId;
    private Guid _selectedLineId;
    private bool _refreshing;
    private ItemList _sequenceList = null!;
    private ItemList _lineList = null!;
    private LineEdit _sequenceName = null!;
    private OptionButton _context = null!;
    private OptionButton _speaker = null!;
    private LineEdit _text = null!;
    private SpinBox _start = null!;
    private SpinBox _duration = null!;
    private SpinBox _volume = null!;
    private OptionButton _style = null!;
    private SpinBox _radius = null!;
    private OptionButton _listener = null!;
    private OptionButton _expression = null!;
    private OptionButton _gazeKind = null!;
    private OptionButton _gazePlayer = null!;
    private readonly SpinBox[] _worldPoint = new SpinBox[3];

    public event Action<DialogueLine>? PreviewLineRequested;
    public event Action<string>? StatusChanged;

    public void Configure(GameProject project, PlayDefinition play)
    {
        BuildUi();
        SetProject(project, play);
    }

    public void SetProject(GameProject project, PlayDefinition play)
    {
        _project = project;
        _playId = play.Id;
        PopulatePlayers();
        RefreshSequences();
    }

    public void SetPlay(PlayDefinition play)
    {
        _playId = play.Id;
        RefreshSequences();
    }

    public void SetInteractionEnabled(bool enabled) => MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

    private void BuildUi()
    {
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        margin.AddChild(stack);

        var title = new Label { Text = "DIALOGUE DIRECTOR", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(title);

        var sequenceRow = new HBoxContainer();
        stack.AddChild(sequenceRow);
        _sequenceName = new LineEdit { PlaceholderText = "Sequence name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sequenceRow.AddChild(_sequenceName);
        _context = EnumOption<DialogueSequenceContext>();
        sequenceRow.AddChild(_context);
        AddButton(sequenceRow, "New Sequence", CreateSequence);

        var lists = new HBoxContainer();
        stack.AddChild(lists);
        _sequenceList = new ItemList { CustomMinimumSize = new Vector2(250, 72), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sequenceList.ItemSelected += SelectSequence;
        lists.AddChild(_sequenceList);
        _lineList = new ItemList { CustomMinimumSize = new Vector2(400, 72), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _lineList.ItemSelected += SelectLine;
        lists.AddChild(_lineList);

        var grid = new GridContainer { Columns = 8 };
        stack.AddChild(grid);
        _speaker = new OptionButton(); AddField(grid, "Speaker", _speaker);
        _text = new LineEdit { PlaceholderText = "Dialogue text", SizeFlagsHorizontal = SizeFlags.ExpandFill }; AddField(grid, "Text", _text);
        _start = Number(0, 120, 0.1, 0); AddField(grid, "Start", _start);
        _duration = Number(0.1, 120, 0.1, 1.5); AddField(grid, "Duration", _duration);
        _volume = Number(0, 1, 0.05, 1); AddField(grid, "Volume", _volume);
        _style = EnumOption<SpeechStyle>(); AddField(grid, "Style", _style);
        _radius = Number(0.5, 100, 0.5, 14); AddField(grid, "Radius", _radius);
        _listener = new OptionButton(); AddField(grid, "Listener", _listener);
        _expression = new OptionButton();
        _expression.AddItem("None", -1);
        foreach (var value in Enum.GetValues<DialogueExpression>()) _expression.AddItem(value.ToString(), (int)value);
        AddField(grid, "Expression", _expression);
        _gazeKind = EnumOption<DialogueGazeTargetKind>(); AddField(grid, "Gaze", _gazeKind);
        _gazePlayer = new OptionButton(); AddField(grid, "Gaze player", _gazePlayer);
        for (var index = 0; index < 3; index++)
        {
            _worldPoint[index] = Number(-100, 100, 0.5, index == 1 ? 1 : 0);
            AddField(grid, $"World {(char)('X' + index)}", _worldPoint[index]);
        }

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(actions);
        AddButton(actions, "Add / Update", SaveLine);
        AddButton(actions, "Move Up", () => MoveLine(-1));
        AddButton(actions, "Move Down", () => MoveLine(1));
        AddButton(actions, "Delete Line", DeleteLine);
        AddButton(actions, "Preview Line", PreviewLine);
    }

    private void PopulatePlayers()
    {
        _playerIds.Clear();
        _speaker.Clear(); _listener.Clear(); _gazePlayer.Clear();
        _listener.AddItem("None", -1); _gazePlayer.AddItem("None", -1);
        foreach (var player in _project.HomeTeam.Roster.Concat(_project.AwayTeam.Roster))
        {
            _playerIds.Add(player.Id);
            _speaker.AddItem($"{player.Team!.Name} {player.Name}");
            _listener.AddItem($"{player.Team!.Name} {player.Name}");
            _gazePlayer.AddItem($"{player.Team!.Name} {player.Name}");
        }
    }

    private void CreateSequence()
    {
        try
        {
            var context = (DialogueSequenceContext)_context.GetSelectedId();
            Guid? playId = context == DialogueSequenceContext.Sideline ? null : _playId;
            var name = string.IsNullOrWhiteSpace(_sequenceName.Text) ? $"Dialogue {_project.DialogueSequences.Count + 1}" : _sequenceName.Text;
            var sequence = new DialogueSequence(Guid.NewGuid(), name, context, playId);
            _project.AddDialogueSequence(sequence);
            _selectedSequenceId = sequence.Id;
            RefreshSequences();
            StatusChanged?.Invoke("Dialogue sequence created");
        }
        catch (Exception exception) { StatusChanged?.Invoke(exception.Message); }
    }

    private void SaveLine()
    {
        if (_selectedSequenceId == Guid.Empty || _speaker.Selected < 0) return;
        try
        {
            var lineId = _selectedLineId == Guid.Empty ? Guid.NewGuid() : _selectedLineId;
            var listenerId = OptionalPlayer(_listener);
            var gazeId = OptionalPlayer(_gazePlayer);
            var expressionId = _expression.GetSelectedId();
            var line = new DialogueLine(
                lineId, _playerIds[_speaker.Selected], _start.Value, _duration.Value, _text.Text,
                (float)_volume.Value, (SpeechStyle)_style.GetSelectedId(), (float)_radius.Value,
                listenerId, expressionId < 0 ? null : (DialogueExpression)expressionId,
                (DialogueGazeTargetKind)_gazeKind.GetSelectedId(), gazeId,
                new DialoguePoint((float)_worldPoint[0].Value, (float)_worldPoint[1].Value, (float)_worldPoint[2].Value));
            var sequence = _project.DialogueSequence(_selectedSequenceId);
            if (_selectedLineId == Guid.Empty) sequence.AddLine(line); else sequence.ReplaceLine(line);
            _selectedLineId = line.Id;
            RefreshLines();
            StatusChanged?.Invoke("Dialogue line saved");
        }
        catch (Exception exception) { StatusChanged?.Invoke(exception.Message); }
    }

    private void SelectSequence(long index)
    {
        if (_refreshing || index < 0 || index >= _sequenceIds.Count) return;
        _selectedSequenceId = _sequenceIds[(int)index];
        _selectedLineId = Guid.Empty;
        RefreshLines();
    }

    private void SelectLine(long index)
    {
        if (_refreshing || index < 0 || index >= _lineIds.Count) return;
        _selectedLineId = _lineIds[(int)index];
        LoadLine(SelectedLine());
    }

    private void MoveLine(int offset)
    {
        if (_selectedLineId == Guid.Empty) return;
        _project.DialogueSequence(_selectedSequenceId).MoveLine(_selectedLineId, offset);
        RefreshLines();
    }

    private void DeleteLine()
    {
        if (_selectedLineId == Guid.Empty) return;
        _project.DialogueSequence(_selectedSequenceId).RemoveLine(_selectedLineId);
        _selectedLineId = Guid.Empty;
        RefreshLines();
        StatusChanged?.Invoke("Dialogue line deleted");
    }

    private void PreviewLine()
    {
        if (_selectedLineId != Guid.Empty) PreviewLineRequested?.Invoke(SelectedLine());
    }

    private void RefreshSequences()
    {
        _refreshing = true;
        _sequenceIds.Clear(); _sequenceList.Clear();
        var visible = _project.DialogueSequences.Where(sequence => sequence.PlayId == _playId || sequence.Context == DialogueSequenceContext.Sideline).ToArray();
        foreach (var sequence in visible)
        {
            _sequenceIds.Add(sequence.Id);
            _sequenceList.AddItem($"{sequence.Name} [{sequence.Context}]");
        }
        if (!_sequenceIds.Contains(_selectedSequenceId)) _selectedSequenceId = _sequenceIds.FirstOrDefault();
        var index = _sequenceIds.IndexOf(_selectedSequenceId);
        if (index >= 0) _sequenceList.Select(index);
        _refreshing = false;
        RefreshLines();
    }

    private void RefreshLines()
    {
        _refreshing = true;
        _lineIds.Clear(); _lineList.Clear();
        if (_selectedSequenceId != Guid.Empty)
        {
            foreach (var line in _project.DialogueSequence(_selectedSequenceId).Lines)
            {
                _lineIds.Add(line.Id);
                _lineList.AddItem($"{line.StartTime:0.0}s {PlayerName(line.SpeakerPlayerId)}: {line.Text}");
            }
        }
        var index = _lineIds.IndexOf(_selectedLineId);
        if (index >= 0) _lineList.Select(index);
        _refreshing = false;
    }

    private void LoadLine(DialogueLine line)
    {
        _speaker.Select(_playerIds.IndexOf(line.SpeakerPlayerId));
        _text.Text = line.Text; _start.Value = line.StartTime; _duration.Value = line.Duration;
        _volume.Value = line.Volume; _style.Select((int)line.SpeechStyle); _radius.Value = line.AudibilityRadius;
        _listener.Select(line.ListenerPlayerId.HasValue ? _playerIds.IndexOf(line.ListenerPlayerId.Value) + 1 : 0);
        _expression.Select(line.Expression.HasValue ? (int)line.Expression.Value + 1 : 0);
        _gazeKind.Select((int)line.GazeTargetKind);
        _gazePlayer.Select(line.GazeTargetPlayerId.HasValue ? _playerIds.IndexOf(line.GazeTargetPlayerId.Value) + 1 : 0);
        _worldPoint[0].Value = line.GazeWorldPoint.X; _worldPoint[1].Value = line.GazeWorldPoint.Y; _worldPoint[2].Value = line.GazeWorldPoint.Z;
    }

    private DialogueLine SelectedLine() => _project.DialogueSequence(_selectedSequenceId).Lines.First(line => line.Id == _selectedLineId);
    private Guid? OptionalPlayer(OptionButton option) => option.Selected <= 0 ? null : _playerIds[option.Selected - 1];
    private string PlayerName(Guid id) => _project.HomeTeam.Roster.Concat(_project.AwayTeam.Roster).First(player => player.Id == id).Name;

    private static OptionButton EnumOption<T>() where T : struct, Enum
    {
        var option = new OptionButton();
        foreach (var value in Enum.GetValues<T>()) option.AddItem(value.ToString(), Convert.ToInt32(value));
        return option;
    }

    private static SpinBox Number(double min, double max, double step, double value) => new() { MinValue = min, MaxValue = max, Step = step, Value = value };
    private static void AddField(GridContainer grid, string label, Control control) { grid.AddChild(new Label { Text = label }); grid.AddChild(control); }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
}
