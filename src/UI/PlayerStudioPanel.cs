using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerStudioPanel : PanelContainer
{
    private readonly List<Guid> _playerIds = [];
    private readonly Dictionary<PlayerAccessories, CheckButton> _accessoryChecks = [];
    private GameProject _project = null!;
    private Game _game = null!;
    private Guid _selectedPlayerId;
    private bool _refreshing;
    private OptionButton _playerOption = null!;
    private SpinBox _height = null!;
    private OptionButton _bodyBuild = null!;
    private SpinBox _shoulderWidth = null!;
    private SpinBox _chestWidth = null!;
    private SpinBox _waistWidth = null!;
    private SpinBox _hipWidth = null!;
    private SpinBox _armLength = null!;
    private SpinBox _legLength = null!;
    private ColorPickerButton _skinTone = null!;
    private OptionButton _hairStyle = null!;
    private ColorPickerButton _hairColor = null!;
    private SpinBox _jerseyNumber = null!;
    private ColorPickerButton _primaryColor = null!;
    private ColorPickerButton _secondaryColor = null!;
    private ColorPickerButton _flagColor = null!;

    public event Action<Guid>? AppearanceChanged;
    public event Action<string>? StatusChanged;

    public void Configure(GameProject project, Game game)
    {
        BuildUi();
        SetProject(project, game);
    }

    public void SetProject(GameProject project, Game game)
    {
        _project = project;
        _game = game;
        RefreshPlayers();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _playerOption.Disabled = !enabled;
        foreach (var control in new Control[] { _height, _bodyBuild, _shoulderWidth, _chestWidth, _waistWidth, _hipWidth, _armLength, _legLength, _skinTone, _hairStyle, _hairColor, _jerseyNumber, _primaryColor, _secondaryColor, _flagColor })
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        foreach (var check in _accessoryChecks.Values)
            check.Disabled = !enabled;
    }

    public void RefreshUniformFields()
    {
        _refreshing = true;
        RefreshEditor();
        _refreshing = false;
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 5);
        margin.AddChild(stack);

        var title = new Label { Text = "PLAYER STUDIO", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(title);

        _playerOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerOption.ItemSelected += OnPlayerSelected;
        stack.AddChild(_playerOption);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        stack.AddChild(scroll);

        var editorStack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(editorStack);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 4);
        editorStack.AddChild(grid);

        _height = CreateSpinBox(1.4, 2.3, 0.01, " m");
        _height.ValueChanged += OnHeightChanged;
        AddField(grid, "Height", _height);

        _bodyBuild = CreateBodyBuildOption();
        _bodyBuild.ItemSelected += OnBodyBuildChanged;
        AddField(grid, "Body build", _bodyBuild);

        _shoulderWidth = CreateSpinBox(0.7, 1.3, 0.01, "×");
        _shoulderWidth.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Shoulder width", _shoulderWidth);

        _chestWidth = CreateSpinBox(0.7, 1.3, 0.01, "×");
        _chestWidth.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Chest width", _chestWidth);

        _waistWidth = CreateSpinBox(0.7, 1.3, 0.01, "×");
        _waistWidth.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Waist width", _waistWidth);

        _hipWidth = CreateSpinBox(0.7, 1.3, 0.01, "×");
        _hipWidth.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Hip width", _hipWidth);

        _armLength = CreateSpinBox(0.75, 1.25, 0.01, "×");
        _armLength.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Arm length", _armLength);

        _legLength = CreateSpinBox(0.75, 1.25, 0.01, "×");
        _legLength.ValueChanged += OnBodyProportionsChanged;
        AddField(grid, "Leg length", _legLength);

        _skinTone = CreateColorButton();
        _skinTone.ColorChanged += color => UpdateAppearance(appearance => appearance.SetSkinTone(ToDomain(color)));
        AddField(grid, "Skin tone", _skinTone);

        _hairStyle = CreateEnumOption<HairStyle>();
        _hairStyle.ItemSelected += OnHairStyleChanged;
        AddField(grid, "Hair style", _hairStyle);

        _hairColor = CreateColorButton();
        _hairColor.ColorChanged += OnHairColorChanged;
        AddField(grid, "Hair color", _hairColor);

        _jerseyNumber = CreateSpinBox(0, 99, 1);
        _jerseyNumber.ValueChanged += OnJerseyNumberChanged;
        AddField(grid, "Jersey number", _jerseyNumber);

        _primaryColor = CreateColorButton();
        _primaryColor.ColorChanged += OnUniformColorChanged;
        AddField(grid, "Primary uniform", _primaryColor);

        _secondaryColor = CreateColorButton();
        _secondaryColor.ColorChanged += OnUniformColorChanged;
        AddField(grid, "Secondary uniform", _secondaryColor);

        _flagColor = CreateColorButton();
        _flagColor.ColorChanged += OnFlagColorChanged;
        AddField(grid, "Flag color", _flagColor);

        editorStack.AddChild(new Label { Text = "Accessories" });
        var accessories = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        editorStack.AddChild(accessories);
        AddAccessory(accessories, "Headband", PlayerAccessories.Headband);
        AddAccessory(accessories, "Wristbands", PlayerAccessories.Wristbands);
        AddAccessory(accessories, "Visor", PlayerAccessories.Visor);
        AddAccessory(accessories, "Sleeves", PlayerAccessories.ArmSleeves);
    }

    private void RefreshPlayers()
    {
        _refreshing = true;
        _playerIds.Clear();
        _playerOption.Clear();
        foreach (var player in _game.Gold.Roster.Concat(_game.Navy.Roster))
        {
            _playerIds.Add(player.Id);
            _playerOption.AddItem($"{player.Team?.Name} #{player.JerseyNumber} {player.Name}");
        }

        _selectedPlayerId = _playerIds.Count > 0 ? _playerIds[0] : Guid.Empty;
        if (_playerIds.Count > 0)
            _playerOption.Select(0);
        RefreshEditor();
        _refreshing = false;
    }

    private void RefreshEditor()
    {
        if (_selectedPlayerId == Guid.Empty)
            return;
        var appearance = _project.AppearanceFor(_selectedPlayerId);
        var player = SelectedPlayer();
        var uniform = _project.ActiveUniformFor(player.Team!.Id);
        _height.Value = appearance.HeightMeters;
        SelectItemById(_bodyBuild, (int)appearance.BodyBuild);
        _shoulderWidth.Value = appearance.ShoulderWidth;
        _chestWidth.Value = appearance.ChestWidth;
        _waistWidth.Value = appearance.WaistWidth;
        _hipWidth.Value = appearance.HipWidth;
        _armLength.Value = appearance.ArmLength;
        _legLength.Value = appearance.LegLength;
        _skinTone.Color = ToGodot(appearance.SkinTone);
        _hairStyle.Select((int)appearance.HairStyle);
        _hairColor.Color = ToGodot(appearance.HairColor);
        _jerseyNumber.Value = player.JerseyNumber;
        _primaryColor.Color = ToGodot(uniform.PrimaryColor);
        _secondaryColor.Color = ToGodot(uniform.SecondaryColor);
        _flagColor.Color = ToGodot(uniform.FlagColor);
        foreach (var accessory in _accessoryChecks)
            accessory.Value.ButtonPressed = appearance.Accessories.HasFlag(accessory.Key);
    }

    private void OnPlayerSelected(long index)
    {
        if (_refreshing || index < 0 || index >= _playerIds.Count)
            return;
        _selectedPlayerId = _playerIds[(int)index];
        _refreshing = true;
        RefreshEditor();
        _refreshing = false;
    }

    private void OnHeightChanged(double value) =>
        UpdateAppearance(appearance => appearance.SetHeight((float)value));

    private void OnBodyBuildChanged(long index) =>
        UpdateAppearance(appearance => appearance.SetBodyBuild((BodyBuild)_bodyBuild.GetItemId((int)index)));

    private void OnBodyProportionsChanged(double _) =>
        UpdateAppearance(appearance => appearance.SetBodyProportions(
            (float)_shoulderWidth.Value,
            (float)_chestWidth.Value,
            (float)_waistWidth.Value,
            (float)_hipWidth.Value,
            (float)_armLength.Value,
            (float)_legLength.Value));

    private void OnHairStyleChanged(long index) =>
        UpdateAppearance(appearance => appearance.SetHair((HairStyle)_hairStyle.GetItemId((int)index), ToDomain(_hairColor.Color)));

    private void OnHairColorChanged(Color color) =>
        UpdateAppearance(appearance => appearance.SetHair(appearance.HairStyle, ToDomain(color)));

    private void OnUniformColorChanged(Color _)
    {
        if (_refreshing)
            return;
        var appearance = _project.AppearanceFor(_selectedPlayerId);
        var uniform = SelectedUniform();
        var primary = ToDomain(_primaryColor.Color);
        var secondary = ToDomain(_secondaryColor.Color);
        appearance.SetUniformColors(primary, secondary);
        uniform.SetPrimaryColors(primary, secondary, uniform.AccentColor);
        AppearanceChanged?.Invoke(_selectedPlayerId);
    }

    private void OnFlagColorChanged(Color color)
    {
        if (_refreshing)
            return;
        var flag = ToDomain(color);
        _project.AppearanceFor(_selectedPlayerId).SetFlagColor(flag);
        SelectedUniform().SetFlagColor(flag);
        AppearanceChanged?.Invoke(_selectedPlayerId);
    }

    private void OnJerseyNumberChanged(double value)
    {
        if (_refreshing)
            return;
        try
        {
            var player = SelectedPlayer();
            player.Team!.ChangeJerseyNumber(player.Id, (int)value);
            _project.AppearanceFor(player.Id).SetJerseyNumber(player.JerseyNumber);
            UpdatePlayerLabel(player);
            AppearanceChanged?.Invoke(player.Id);
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(exception.Message);
            _refreshing = true;
            RefreshEditor();
            _refreshing = false;
        }
    }

    private void OnAccessoriesChanged(bool _)
    {
        var selected = PlayerAccessories.None;
        foreach (var accessory in _accessoryChecks)
        {
            if (accessory.Value.ButtonPressed)
                selected |= accessory.Key;
        }
        UpdateAppearance(appearance => appearance.SetAccessories(selected));
    }

    private void UpdateAppearance(Action<PlayerAppearance> update)
    {
        if (_refreshing || _selectedPlayerId == Guid.Empty)
            return;
        update(_project.AppearanceFor(_selectedPlayerId));
        UpdatePlayerLabel(SelectedPlayer());
        AppearanceChanged?.Invoke(_selectedPlayerId);
    }

    private Player SelectedPlayer() =>
        _game.Gold.Roster.Concat(_game.Navy.Roster).First(player => player.Id == _selectedPlayerId);

    private UniformDefinition SelectedUniform() =>
        _project.ActiveUniformFor(SelectedPlayer().Team!.Id);

    private void UpdatePlayerLabel(Player player)
    {
        var index = _playerIds.IndexOf(player.Id);
        if (index >= 0)
            _playerOption.SetItemText(index, $"{player.Team?.Name} #{player.JerseyNumber} {player.Name}");
    }

    private void AddAccessory(Node parent, string label, PlayerAccessories accessory)
    {
        var check = new CheckButton { Text = label };
        check.Toggled += OnAccessoriesChanged;
        parent.AddChild(check);
        _accessoryChecks.Add(accessory, check);
    }

    private static void AddField(GridContainer grid, string label, Control editor)
    {
        grid.AddChild(new Label { Text = label });
        editor.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(editor);
    }

    private static SpinBox CreateSpinBox(double minimum, double maximum, double step, string suffix = "") => new()
    {
        MinValue = minimum,
        MaxValue = maximum,
        Step = step,
        Suffix = suffix
    };

    private static ColorPickerButton CreateColorButton() => new()
    {
        CustomMinimumSize = new Vector2(170, 28),
        EditAlpha = false
    };

    private static OptionButton CreateEnumOption<T>() where T : struct, Enum
    {
        var option = new OptionButton();
        foreach (var value in Enum.GetValues<T>())
            option.AddItem(SplitName(value.ToString()), Convert.ToInt32(value));
        return option;
    }

    private static OptionButton CreateBodyBuildOption()
    {
        var option = new OptionButton();
        foreach (var build in new[] { BodyBuild.Slim, BodyBuild.Average, BodyBuild.Athletic, BodyBuild.Heavy })
            option.AddItem(build.ToString(), (int)build);
        return option;
    }

    private static void SelectItemById(OptionButton option, int id)
    {
        for (var index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }
        option.Select(0);
    }

    private static string SplitName(string value)
    {
        var result = value;
        for (var index = result.Length - 1; index > 0; index--)
        {
            if (char.IsUpper(result[index]) && !char.IsUpper(result[index - 1]))
                result = result.Insert(index, " ");
        }
        return result;
    }

    private static AppearanceColor ToDomain(Color color) => new(
        (byte)Mathf.RoundToInt(color.R * 255),
        (byte)Mathf.RoundToInt(color.G * 255),
        (byte)Mathf.RoundToInt(color.B * 255));

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);
}
