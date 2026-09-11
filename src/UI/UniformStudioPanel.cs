using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class UniformStudioPanel : PanelContainer
{
    private readonly List<Guid> _teamIds = [];
    private readonly List<Guid> _uniformIds = [];
    private readonly Dictionary<string, ColorPickerButton> _colors = [];
    private GameProject _project = null!;
    private Guid _selectedTeamId;
    private Guid _selectedUniformId;
    private bool _refreshing;
    private OptionButton _teamOption = null!;
    private ItemList _uniformList = null!;
    private LineEdit _nameEdit = null!;
    private OptionButton _designation = null!;
    private LineEdit _wordmark = null!;
    private CheckButton _showPlayerName = null!;

    public event Action<Guid>? UniformChanged;
    public event Action<string>? StatusChanged;

    public void Configure(GameProject project)
    {
        BuildUi();
        SetProject(project);
    }

    public void SetProject(GameProject project)
    {
        _project = project;
        _refreshing = true;
        _teamIds.Clear();
        _teamOption.Clear();
        foreach (var team in new[] { project.HomeTeam, project.AwayTeam })
        {
            _teamIds.Add(team.Id);
            _teamOption.AddItem(team.Name);
        }
        _selectedTeamId = _teamIds[0];
        _teamOption.Select(0);
        _selectedUniformId = _project.ActiveUniformFor(_selectedTeamId).Id;
        RefreshUniformList();
        _refreshing = false;
    }

    public void SetInteractionEnabled(bool enabled) => MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        margin.AddChild(stack);

        var title = new Label { Text = "UNIFORM STUDIO", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(title);

        _teamOption = new OptionButton();
        _teamOption.ItemSelected += OnTeamSelected;
        stack.AddChild(_teamOption);

        _uniformList = new ItemList { CustomMinimumSize = new Vector2(0, 58), SelectMode = ItemList.SelectModeEnum.Single };
        _uniformList.ItemSelected += OnUniformSelected;
        stack.AddChild(_uniformList);

        _nameEdit = new LineEdit { PlaceholderText = "Uniform name" };
        stack.AddChild(_nameEdit);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(actions);
        AddButton(actions, "New", CreateUniform);
        AddButton(actions, "Rename", RenameUniform);
        AddButton(actions, "Duplicate", DuplicateUniform);
        AddButton(actions, "Delete", DeleteUniform);
        AddButton(actions, "Set Active", SetActiveUniform);

        var metadata = new GridContainer { Columns = 2 };
        stack.AddChild(metadata);
        metadata.AddChild(new Label { Text = "Designation" });
        _designation = new OptionButton();
        foreach (var value in Enum.GetValues<UniformDesignation>())
            _designation.AddItem(value.ToString(), (int)value);
        _designation.ItemSelected += OnDesignationChanged;
        metadata.AddChild(_designation);

        metadata.AddChild(new Label { Text = "Wordmark" });
        _wordmark = new LineEdit();
        _wordmark.TextChanged += OnWordmarkChanged;
        metadata.AddChild(_wordmark);

        metadata.AddChild(new Label { Text = "Player name on back" });
        _showPlayerName = new CheckButton();
        _showPlayerName.Toggled += OnPlayerNameToggled;
        metadata.AddChild(_showPlayerName);

        var colors = new GridContainer { Columns = 4 };
        colors.AddThemeConstantOverride("h_separation", 8);
        colors.AddThemeConstantOverride("v_separation", 3);
        stack.AddChild(colors);
        AddColorField(colors, "Primary", "primary");
        AddColorField(colors, "Secondary", "secondary");
        AddColorField(colors, "Accent", "accent");
        AddColorField(colors, "Jersey base", "jersey");
        AddColorField(colors, "Sleeve trim", "sleeve");
        AddColorField(colors, "Collar trim", "collar");
        AddColorField(colors, "Number", "number");
        AddColorField(colors, "Number outline", "outline");
        AddColorField(colors, "Shorts", "shorts");
        AddColorField(colors, "Flags", "flags");
    }

    private void RefreshUniformList()
    {
        _uniformIds.Clear();
        _uniformList.Clear();
        var activeId = _project.ActiveUniformFor(_selectedTeamId).Id;
        var selectedIndex = -1;
        foreach (var uniform in _project.UniformsFor(_selectedTeamId))
        {
            var index = _uniformIds.Count;
            _uniformIds.Add(uniform.Id);
            _uniformList.AddItem($"{(uniform.Id == activeId ? "★ " : string.Empty)}{uniform.Name} ({uniform.Designation})");
            if (uniform.Id == _selectedUniformId)
                selectedIndex = index;
        }
        if (selectedIndex < 0 && _uniformIds.Count > 0)
        {
            selectedIndex = 0;
            _selectedUniformId = _uniformIds[0];
        }
        if (selectedIndex >= 0)
            _uniformList.Select(selectedIndex);
        RefreshEditor();
    }

    private void RefreshEditor()
    {
        if (_selectedUniformId == Guid.Empty)
            return;
        var uniform = _project.Uniform(_selectedUniformId);
        _nameEdit.Text = uniform.Name;
        _designation.Select((int)uniform.Designation);
        _wordmark.Text = uniform.TeamWordmark;
        _showPlayerName.ButtonPressed = uniform.ShowPlayerNameOnBack;
        SetColor("primary", uniform.PrimaryColor);
        SetColor("secondary", uniform.SecondaryColor);
        SetColor("accent", uniform.AccentColor);
        SetColor("jersey", uniform.JerseyBaseColor);
        SetColor("sleeve", uniform.SleeveTrimColor);
        SetColor("collar", uniform.CollarTrimColor);
        SetColor("number", uniform.NumberColor);
        SetColor("outline", uniform.NumberOutlineColor);
        SetColor("shorts", uniform.ShortsColor);
        SetColor("flags", uniform.FlagColor);
    }

    private void OnTeamSelected(long index)
    {
        if (_refreshing || index < 0 || index >= _teamIds.Count)
            return;
        _refreshing = true;
        _selectedTeamId = _teamIds[(int)index];
        _selectedUniformId = _project.ActiveUniformFor(_selectedTeamId).Id;
        RefreshUniformList();
        _refreshing = false;
    }

    private void OnUniformSelected(long index)
    {
        if (_refreshing || index < 0 || index >= _uniformIds.Count)
            return;
        _refreshing = true;
        _selectedUniformId = _uniformIds[(int)index];
        RefreshEditor();
        _refreshing = false;
    }

    private void CreateUniform()
    {
        var team = TeamFor(_selectedTeamId);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        uniform.Rename($"Uniform {_project.UniformsFor(team.Id).Count + 1}");
        _project.AddUniform(uniform);
        _selectedUniformId = uniform.Id;
        RefreshSafely("Uniform created");
    }

    private void RenameUniform()
    {
        TryEdit(() => SelectedUniform().Rename(_nameEdit.Text), "Uniform renamed", false, true);
    }

    private void DuplicateUniform()
    {
        var source = SelectedUniform();
        var duplicate = source.Duplicate($"{source.Name} Copy");
        _project.AddUniform(duplicate);
        _selectedUniformId = duplicate.Id;
        RefreshSafely("Uniform duplicated");
    }

    private void DeleteUniform()
    {
        if (_refreshing)
            return;
        try
        {
            var wasActive = _project.ActiveUniformFor(_selectedTeamId).Id == _selectedUniformId;
            _project.RemoveUniform(_selectedUniformId);
            _selectedUniformId = _project.ActiveUniformFor(_selectedTeamId).Id;
            RefreshSafely("Uniform deleted");
            if (wasActive)
                UniformChanged?.Invoke(_selectedTeamId);
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(exception.Message);
            RefreshSafely(null);
        }
    }

    private void SetActiveUniform()
    {
        TryEdit(() => _project.SetActiveUniform(_selectedTeamId, _selectedUniformId), "Active uniform updated", true, true);
    }

    private void OnDesignationChanged(long index) =>
        TryEdit(() => SelectedUniform().SetDesignation((UniformDesignation)_designation.GetItemId((int)index)), null, false, true);

    private void OnWordmarkChanged(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        TryEdit(() => SelectedUniform().SetWordmark(text), null, true);
    }

    private void OnPlayerNameToggled(bool show) =>
        TryEdit(() => SelectedUniform().SetShowPlayerNameOnBack(show), null, true);

    private void OnColorChanged(Color _)
    {
        var uniform = SelectedUniform();
        TryEdit(() =>
        {
            uniform.SetPrimaryColors(GetColor("primary"), GetColor("secondary"), GetColor("accent"));
            uniform.SetJerseyColors(GetColor("jersey"), GetColor("sleeve"), GetColor("collar"));
            uniform.SetNumberColors(GetColor("number"), GetColor("outline"));
            uniform.SetShortsColor(GetColor("shorts"));
            uniform.SetFlagColor(GetColor("flags"));
        }, null, true);
    }

    private void TryEdit(Action edit, string? status, bool notify = false, bool refreshList = false)
    {
        if (_refreshing)
            return;
        try
        {
            edit();
            if (notify)
                NotifyIfActive();
            if (status is not null)
                StatusChanged?.Invoke(status);
            if (refreshList)
                RefreshSafely(null);
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(exception.Message);
            RefreshSafely(null);
        }
    }

    private void NotifyIfActive()
    {
        if (_project.ActiveUniformFor(_selectedTeamId).Id == _selectedUniformId)
            UniformChanged?.Invoke(_selectedTeamId);
    }

    private void RefreshSafely(string? status)
    {
        _refreshing = true;
        RefreshUniformList();
        _refreshing = false;
        if (status is not null)
            StatusChanged?.Invoke(status);
    }

    private void AddColorField(GridContainer grid, string label, string key)
    {
        grid.AddChild(new Label { Text = label });
        var picker = new ColorPickerButton { CustomMinimumSize = new Vector2(92, 26), EditAlpha = false };
        picker.ColorChanged += OnColorChanged;
        grid.AddChild(picker);
        _colors.Add(key, picker);
    }

    private void AddButton(Node parent, string label, Action action)
    {
        var button = new Button { Text = label };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private UniformDefinition SelectedUniform() => _project.Uniform(_selectedUniformId);
    private Team TeamFor(Guid teamId) => teamId == _project.HomeTeam.Id ? _project.HomeTeam : _project.AwayTeam;
    private AppearanceColor GetColor(string key) => ToDomain(_colors[key].Color);
    private void SetColor(string key, AppearanceColor color) => _colors[key].Color = ToGodot(color);

    private static AppearanceColor ToDomain(Color color) => new(
        (byte)Mathf.RoundToInt(color.R * 255),
        (byte)Mathf.RoundToInt(color.G * 255),
        (byte)Mathf.RoundToInt(color.B * 255));
    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
