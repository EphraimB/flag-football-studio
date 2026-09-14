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
    private Label _lastPlayLabel = null!;
    private Label _statusLabel = null!;
    private ItemList _playList = null!;
    private LineEdit _nameEdit = null!;
    private OptionButton _lightingOption = null!;
    private OptionButton _qualityOption = null!;
    private OptionButton _shadowOption = null!;
    private SpinBox _exposure = null!;
    private OptionButton _venueOption = null!;
    private HSlider _spectatorDensity = null!;
    private Label _spectatorDensityValue = null!;
    private CheckButton _showSpectators = null!;
    private CheckButton _showEquipment = null!;

    public event Action<Guid>? PlaySelected;
    public event Action? CreateRequested;
    public event Action<Guid, string>? RenameRequested;
    public event Action<Guid>? DuplicateRequested;
    public event Action<Guid>? DeleteRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action<VisualPresentationSettings>? VisualSettingsChanged;
    public event Action<VenuePresentationSettings>? EnvironmentSettingsChanged;

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

        _lastPlayLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _lastPlayLabel.AddThemeColorOverride("font_color", new Color("ffe08a"));
        stack.AddChild(_lastPlayLabel);

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

        stack.AddChild(new HSeparator());
        var visualsTitle = new Label { Text = "VISUALS / ENVIRONMENT" };
        visualsTitle.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(visualsTitle);

        var visuals = new GridContainer { Columns = 2 };
        visuals.AddThemeConstantOverride("h_separation", 10);
        visuals.AddThemeConstantOverride("v_separation", 5);
        stack.AddChild(visuals);
        _lightingOption = AddOption(visuals, "Lighting", Enum.GetNames<SportsLightingPreset>());
        _qualityOption = AddOption(visuals, "Quality", Enum.GetNames<PresentationQualityPreset>());
        _shadowOption = AddOption(visuals, "Shadows", Enum.GetNames<ShadowQualityPreset>());
        visuals.AddChild(new Label { Text = "Exposure" });
        _exposure = new SpinBox
        {
            MinValue = 0.6,
            MaxValue = 1.4,
            Step = 0.05,
            Value = 1,
            CustomMinimumSize = new Vector2(170, 0)
        };
        visuals.AddChild(_exposure);
        _lightingOption.Select((int)SportsLightingPreset.Day);
        _qualityOption.Select((int)PresentationQualityPreset.Preview);
        _shadowOption.Select((int)ShadowQualityPreset.Medium);
        _lightingOption.ItemSelected += _ => RaiseVisualSettingsChanged();
        _qualityOption.ItemSelected += _ => RaiseVisualSettingsChanged();
        _shadowOption.ItemSelected += _ => RaiseVisualSettingsChanged();
        _exposure.ValueChanged += _ => RaiseVisualSettingsChanged();

        stack.AddChild(new HSeparator());
        var environmentTitle = new Label { Text = "VENUE ENVIRONMENT" };
        environmentTitle.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(environmentTitle);
        var environment = new GridContainer { Columns = 2 };
        environment.AddThemeConstantOverride("h_separation", 10);
        environment.AddThemeConstantOverride("v_separation", 5);
        stack.AddChild(environment);
        _venueOption = AddOption(environment, "Venue", Enum.GetNames<VenuePreset>());
        environment.AddChild(new Label { Text = "Crowd density" });
        var densityRow = new HBoxContainer();
        _spectatorDensity = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05,
            Value = VenuePresentationSettings.Default.SpectatorDensity,
            CustomMinimumSize = new Vector2(120, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Deterministic seat occupancy; quality also limits preview crowd count."
        };
        _spectatorDensityValue = new Label { Text = "45%", CustomMinimumSize = new Vector2(42, 0) };
        densityRow.AddChild(_spectatorDensity);
        densityRow.AddChild(_spectatorDensityValue);
        environment.AddChild(densityRow);
        environment.AddChild(new Label { Text = "Spectators" });
        _showSpectators = new CheckButton { Text = "Show", ButtonPressed = true };
        environment.AddChild(_showSpectators);
        environment.AddChild(new Label { Text = "Sideline equipment" });
        _showEquipment = new CheckButton { Text = "Show", ButtonPressed = true };
        environment.AddChild(_showEquipment);
        _venueOption.Select((int)VenuePresentationSettings.Default.Preset);
        _venueOption.ItemSelected += _ => RaiseEnvironmentSettingsChanged();
        _spectatorDensity.ValueChanged += value =>
        {
            _spectatorDensityValue.Text = $"{value * 100:0}%";
            RaiseEnvironmentSettingsChanged();
        };
        _showSpectators.Toggled += _ => RaiseEnvironmentSettingsChanged();
        _showEquipment.Toggled += _ => RaiseEnvironmentSettingsChanged();
    }

    public void RefreshScoreboard()
    {
        _scoreLabel.Text = $"{_project.HomeTeam.Name.ToUpperInvariant()} {_project.HomeScore}  —  {_project.AwayScore} {_project.AwayTeam.Name.ToUpperInvariant()}";
        var minutes = _project.GameClockSeconds / 60;
        var seconds = _project.GameClockSeconds % 60;
        _situationLabel.Text = $"Q{_project.Quarter}  {minutes:00}:{seconds:00}  •  {Ordinal(_project.Down)} & {_project.Distance}  •  {_project.Possession.Name.ToUpperInvariant()} BALL";
        _lastPlayLabel.Text = $"Last play: {_project.LastPlayResult}";
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

    private static OptionButton AddOption(GridContainer grid, string label, string[] names)
    {
        grid.AddChild(new Label { Text = label });
        var option = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        foreach (var name in names)
            option.AddItem(ReadableName(name));
        grid.AddChild(option);
        return option;
    }

    private void RaiseVisualSettingsChanged() => VisualSettingsChanged?.Invoke(
        new VisualPresentationSettings(
            (SportsLightingPreset)_lightingOption.Selected,
            (PresentationQualityPreset)_qualityOption.Selected,
            (ShadowQualityPreset)_shadowOption.Selected,
            (float)_exposure.Value));

    private void RaiseEnvironmentSettingsChanged() => EnvironmentSettingsChanged?.Invoke(
        new VenuePresentationSettings(
            (VenuePreset)_venueOption.Selected,
            (float)_spectatorDensity.Value,
            _showSpectators.ButtonPressed,
            _showEquipment.ButtonPressed));

    private static string ReadableName(string value)
    {
        var result = value;
        for (var index = result.Length - 1; index > 0; index--)
            if (char.IsUpper(result[index]) && !char.IsUpper(result[index - 1]))
                result = result.Insert(index, " ");
        return result;
    }

    private static string Ordinal(int down) => down switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{down}th"
    };
}
