using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class GameDirectorPanel : PanelContainer
{
    private readonly List<Guid> _playIds = [];
    private readonly List<Button> _buttons = [];
    private GameProject _project = null!;
    private Guid _activePlayId;
    private Label _scoreLabel = null!;
    private Label _situationLabel = null!;
    private Label _statusLabel = null!;
    private ItemList _playList = null!;
    private LineEdit _nameEdit = null!;

    public event Action<Guid>? PlaySelected;
    public event Action? CreateRequested;
    public event Action<Guid, string>? RenameRequested;
    public event Action<Guid>? DuplicateRequested;
    public event Action<Guid>? DeleteRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;

    public Label StatusLabel => _statusLabel;

    public void Configure(GameProject project, Guid activePlayId)
    {
        BuildUi();
        SetProject(project, activePlayId);
    }

    public void SetProject(GameProject project, Guid activePlayId)
    {
        _project = project;
        _activePlayId = activePlayId;
        RefreshScoreboard();
        RefreshPlayList();
    }

    public void SetActivePlay(Guid playId)
    {
        _activePlayId = playId;
        RefreshPlayList();
    }

    public void RefreshPlayList()
    {
        _playIds.Clear();
        _playList.Clear();
        var selectedIndex = -1;
        for (var index = 0; index < _project.Plays.Count; index++)
        {
            var play = _project.Plays[index];
            _playIds.Add(play.Id);
            _playList.AddItem($"{index + 1}. {play.Name}");
            if (play.Id == _activePlayId)
                selectedIndex = index;
        }

        if (selectedIndex >= 0)
        {
            _playList.Select(selectedIndex);
            _playList.EnsureCurrentIsVisible();
            _nameEdit.Text = _project.Plays[selectedIndex].Name;
        }
        else
        {
            _nameEdit.Text = string.Empty;
        }
    }

    public void SetStatus(string status) => _statusLabel.Text = status;

    public void SetInteractionEnabled(bool enabled)
    {
        foreach (var button in _buttons)
            button.Disabled = !enabled;
        _playList.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _nameEdit.Editable = enabled;
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 7);
        margin.AddChild(stack);

        _scoreLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _scoreLabel.AddThemeFontSizeOverride("font_size", 24);
        stack.AddChild(_scoreLabel);

        _situationLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        stack.AddChild(_situationLabel);

        _statusLabel = new Label { Text = "Ready", HorizontalAlignment = HorizontalAlignment.Center };
        stack.AddChild(_statusLabel);
        stack.AddChild(new HSeparator());

        var libraryTitle = new Label { Text = "PLAY LIBRARY" };
        libraryTitle.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(libraryTitle);

        _playList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 130),
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _playList.ItemSelected += OnItemSelected;
        stack.AddChild(_playList);

        _nameEdit = new LineEdit { PlaceholderText = "Play name" };
        stack.AddChild(_nameEdit);

        var editActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(editActions);
        AddButton(editActions, "New", () => CreateRequested?.Invoke());
        AddButton(editActions, "Rename", RequestRename);
        AddButton(editActions, "Duplicate", RequestDuplicate);
        AddButton(editActions, "Delete", RequestDelete);

        var fileActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(fileActions);
        AddButton(fileActions, "Save Project", () => SaveRequested?.Invoke());
        AddButton(fileActions, "Load Project", () => LoadRequested?.Invoke());
    }

    private void RefreshScoreboard()
    {
        _scoreLabel.Text = $"{_project.HomeTeam.Name.ToUpperInvariant()} {_project.HomeScore}  —  {_project.AwayScore} {_project.AwayTeam.Name.ToUpperInvariant()}";
        var minutes = _project.GameClockSeconds / 60;
        var seconds = _project.GameClockSeconds % 60;
        _situationLabel.Text = $"Q{_project.Quarter}  {minutes:00}:{seconds:00}  •  {Ordinal(_project.Down)} & {_project.Distance}  •  {_project.Possession.Name.ToUpperInvariant()} BALL";
    }

    private void OnItemSelected(long index)
    {
        if (index < 0 || index >= _playIds.Count)
            return;
        _activePlayId = _playIds[(int)index];
        _nameEdit.Text = _project.Play(_activePlayId).Name;
        PlaySelected?.Invoke(_activePlayId);
    }

    private void RequestRename()
    {
        if (_activePlayId != Guid.Empty)
            RenameRequested?.Invoke(_activePlayId, _nameEdit.Text);
    }

    private void RequestDuplicate()
    {
        if (_activePlayId != Guid.Empty)
            DuplicateRequested?.Invoke(_activePlayId);
    }

    private void RequestDelete()
    {
        if (_activePlayId != Guid.Empty)
            DeleteRequested?.Invoke(_activePlayId);
    }

    private void AddButton(Node parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        _buttons.Add(button);
    }

    private static string Ordinal(int down) => down switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{down}th"
    };
}
