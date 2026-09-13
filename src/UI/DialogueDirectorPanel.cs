using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
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
    private ProjectAudioAssetStore _audioAssets = null!;
    private FileDialog _audioDialog = null!;
    private Label _audioStatus = null!;
    private Label _clipDuration = null!;
    private Label _lipSyncStatus = null!;
    private ItemList _visemeList = null!;
    private OptionButton _visemeShape = null!;
    private SpinBox _visemeTime = null!;
    private SpinBox _visemeEnd = null!;
    private SpinBox _visemeStrength = null!;
    private SpinBox _seekTime = null!;
    private int _selectedVisemeIndex = -1;
    private LineEdit _voiceName = null!;
    private LineEdit _voiceDescription = null!;
    private SpinBox _voiceVolume = null!;
    private SpinBox _voicePitch = null!;
    private SpinBox _voiceRate = null!;

    public event Action<DialogueLine>? PreviewLineRequested;
    public event Action<DialogueLine, double>? PreviewFromTimeRequested;
    public event Action<string>? StatusChanged;

    public void Configure(GameProject project, PlayDefinition play, ProjectAudioAssetStore audioAssets)
    {
        _audioAssets = audioAssets ?? throw new ArgumentNullException(nameof(audioAssets));
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
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddChild(scroll);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        stack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(stack);

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

        stack.AddChild(new HSeparator());
        stack.AddChild(new Label { Text = "VOICE / AUDIO", HorizontalAlignment = HorizontalAlignment.Center });
        var voiceGrid = new GridContainer { Columns = 6 };
        stack.AddChild(voiceGrid);
        _voiceName = new LineEdit(); AddField(voiceGrid, "Voice", _voiceName);
        _voiceDescription = new LineEdit { PlaceholderText = "Optional description" }; AddField(voiceGrid, "Description", _voiceDescription);
        _voiceVolume = Number(0, 1, 0.05, 1); AddField(voiceGrid, "Default volume", _voiceVolume);
        _voicePitch = Number(-12, 12, 0.25, 0); AddField(voiceGrid, "Pitch semitones", _voicePitch);
        _voiceRate = Number(0.5, 2, 0.05, 1); AddField(voiceGrid, "Rate metadata", _voiceRate);
        AddButton(voiceGrid, "Save Voice", SaveVoiceProfile);

        var audioRow = new HBoxContainer();
        stack.AddChild(audioRow);
        AddButton(audioRow, "Choose WAV/OGG/MP3", ChooseAudio);
        AddButton(audioRow, "Remove Audio", RemoveAudio);
        AddButton(audioRow, "Preview Clip", PreviewLine);
        _audioStatus = new Label { Text = "No audio assigned", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        audioRow.AddChild(_audioStatus);
        _clipDuration = new Label { Text = "Duration: —" }; audioRow.AddChild(_clipDuration);
        _lipSyncStatus = new Label { Text = "Generic fallback" }; audioRow.AddChild(_lipSyncStatus);

        stack.AddChild(new Label { Text = "MANUAL LIP SYNC" });
        var lipRow = new HBoxContainer();
        stack.AddChild(lipRow);
        _visemeList = new ItemList { CustomMinimumSize = new Vector2(310, 70), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _visemeList.ItemSelected += SelectViseme;
        lipRow.AddChild(_visemeList);
        var lipGrid = new GridContainer { Columns = 4 };
        lipRow.AddChild(lipGrid);
        _visemeShape = EnumOption<DialogueViseme>(); AddField(lipGrid, "Shape", _visemeShape);
        _visemeTime = Number(0, 120, 0.01, 0); AddField(lipGrid, "Time", _visemeTime);
        _visemeEnd = Number(-1, 120, 0.01, -1); AddField(lipGrid, "End (-1 none)", _visemeEnd);
        _visemeStrength = Number(0, 1, 0.05, 1); AddField(lipGrid, "Strength", _visemeStrength);
        AddButton(lipGrid, "Add / Update", SaveViseme);
        AddButton(lipGrid, "Delete", DeleteViseme);
        _seekTime = Number(0, 120, 0.05, 0); AddField(lipGrid, "Seek", _seekTime);
        AddButton(lipGrid, "Preview From Time", PreviewFromTime);

        _audioDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Import Voice Audio"
        };
        _audioDialog.Filters = ["*.wav ; WAV Audio", "*.ogg ; OGG Vorbis", "*.mp3 ; MP3 Audio"];
        _audioDialog.FileSelected += ImportAudio;
        AddChild(_audioDialog);

        _speaker.ItemSelected += _ => RefreshVoiceProfile();
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
            var existing = _selectedLineId == Guid.Empty ? null : SelectedLine();
            var line = new DialogueLine(
                lineId, _playerIds[_speaker.Selected], _start.Value, _duration.Value, _text.Text,
                (float)_volume.Value, (SpeechStyle)_style.GetSelectedId(), (float)_radius.Value,
                listenerId, expressionId < 0 ? null : (DialogueExpression)expressionId,
                (DialogueGazeTargetKind)_gazeKind.GetSelectedId(), gazeId,
                new DialoguePoint((float)_worldPoint[0].Value, (float)_worldPoint[1].Value, (float)_worldPoint[2].Value),
                existing?.AudioReference, existing?.LipSyncEvents);
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
        _seekTime.MaxValue = line.Duration;
        _visemeTime.MaxValue = line.Duration;
        _visemeEnd.MaxValue = line.Duration;
        RefreshVoiceProfile();
        RefreshAudioAndVisemes(line);
    }

    private void SaveVoiceProfile()
    {
        if (_speaker.Selected < 0) return;
        try
        {
            var playerId = _playerIds[_speaker.Selected];
            var profile = _project.VoiceProfileFor(playerId);
            profile.Update(_voiceName.Text, _voiceDescription.Text, (float)_voiceVolume.Value,
                (float)_voicePitch.Value, (float)_voiceRate.Value);
            StatusChanged?.Invoke("Player voice profile saved");
        }
        catch (Exception exception) { StatusChanged?.Invoke(exception.Message); }
    }

    private void RefreshVoiceProfile()
    {
        if (_speaker.Selected < 0 || _speaker.Selected >= _playerIds.Count) return;
        var profile = _project.VoiceProfileFor(_playerIds[_speaker.Selected]);
        _voiceName.Text = profile.DisplayName;
        _voiceDescription.Text = profile.Description ?? string.Empty;
        _voiceVolume.Value = profile.DefaultSpeakingVolume;
        _voicePitch.Value = profile.DefaultPitchAdjustment;
        _voiceRate.Value = profile.DefaultSpeakingRate;
    }

    private void ChooseAudio()
    {
        if (_selectedLineId == Guid.Empty) { StatusChanged?.Invoke("Select and save a dialogue line first"); return; }
        _audioDialog.PopupCenteredRatio(0.7f);
    }

    private void ImportAudio(string sourcePath)
    {
        try
        {
            var reference = _audioAssets.Import(sourcePath);
            ReplaceSelectedLine(reference, SelectedLine().LipSyncEvents);
            StatusChanged?.Invoke("Voice audio imported into the project audio folder");
        }
        catch (Exception exception) { StatusChanged?.Invoke(exception.Message); }
    }

    private void RemoveAudio()
    {
        if (_selectedLineId == Guid.Empty) return;
        ReplaceSelectedLine(null, SelectedLine().LipSyncEvents);
        StatusChanged?.Invoke("Audio assignment removed; imported file retained for safe reuse");
    }

    private void SelectViseme(long index)
    {
        if (_selectedLineId == Guid.Empty || index < 0 || index >= SelectedLine().LipSyncEvents.Count) return;
        _selectedVisemeIndex = (int)index;
        var item = SelectedLine().LipSyncEvents[_selectedVisemeIndex];
        _visemeShape.Select((int)item.Viseme);
        _visemeTime.Value = item.StartTime;
        _visemeEnd.Value = item.EndTime ?? -1;
        _visemeStrength.Value = item.BlendStrength;
    }

    private void SaveViseme()
    {
        if (_selectedLineId == Guid.Empty) return;
        try
        {
            var line = SelectedLine();
            var events = line.LipSyncEvents.ToList();
            var item = new VisemeEvent(_visemeTime.Value, _visemeEnd.Value < 0 ? null : _visemeEnd.Value,
                (DialogueViseme)_visemeShape.GetSelectedId(), (float)_visemeStrength.Value);
            if (_selectedVisemeIndex >= 0 && _selectedVisemeIndex < events.Count) events[_selectedVisemeIndex] = item;
            else events.Add(item);
            events.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
            ReplaceSelectedLine(line.AudioReference, events);
            _selectedVisemeIndex = events.IndexOf(item);
            StatusChanged?.Invoke("Timestamped viseme saved");
        }
        catch (Exception exception) { StatusChanged?.Invoke(exception.Message); }
    }

    private void DeleteViseme()
    {
        if (_selectedLineId == Guid.Empty || _selectedVisemeIndex < 0) return;
        var line = SelectedLine();
        var events = line.LipSyncEvents.ToList();
        if (_selectedVisemeIndex < events.Count) events.RemoveAt(_selectedVisemeIndex);
        _selectedVisemeIndex = -1;
        ReplaceSelectedLine(line.AudioReference, events);
        StatusChanged?.Invoke("Viseme deleted");
    }

    private void PreviewFromTime()
    {
        if (_selectedLineId != Guid.Empty) PreviewFromTimeRequested?.Invoke(SelectedLine(), _seekTime.Value);
    }

    private void ReplaceSelectedLine(VoiceAudioReference? audio, IEnumerable<VisemeEvent> events)
    {
        var line = SelectedLine();
        var replacement = new DialogueLine(line.Id, line.SpeakerPlayerId, line.StartTime, line.Duration, line.Text,
            line.Volume, line.SpeechStyle, line.AudibilityRadius, line.ListenerPlayerId, line.Expression,
            line.GazeTargetKind, line.GazeTargetPlayerId, line.GazeWorldPoint, audio, events);
        _project.DialogueSequence(_selectedSequenceId).ReplaceLine(replacement);
        RefreshLines();
        LoadLine(replacement);
    }

    private void RefreshAudioAndVisemes(DialogueLine line)
    {
        _audioStatus.Text = line.AudioReference?.RelativePath ?? "No audio assigned";
        _lipSyncStatus.Text = line.HasManualLipSync ? "Manual/timestamped lip sync" : "Generic fallback mouth motion";
        if (line.AudioReference is not null)
        {
            try { _clipDuration.Text = $"Duration: {VoiceAudioStreamLoader.Duration(_audioAssets, line.AudioReference):0.00}s"; }
            catch { _clipDuration.Text = "Duration: missing/unreadable"; }
        }
        else _clipDuration.Text = "Duration: —";
        _visemeList.Clear();
        foreach (var item in line.LipSyncEvents)
            _visemeList.AddItem($"{item.StartTime:0.00}s  {item.Viseme}  {item.BlendStrength:0.00}");
        _selectedVisemeIndex = -1;
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
