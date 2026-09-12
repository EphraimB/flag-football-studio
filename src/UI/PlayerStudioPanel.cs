using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum GazePreviewTargetKind
{
    Manual,
    Football,
    Player
}

public partial class PlayerStudioPanel : PanelContainer
{
    private enum FaceControl
    {
        HeadWidth, HeadHeight, JawWidth, JawHeight, ChinWidth, ChinProjection,
        CheekboneWidth, CheekFullness, ForeheadHeight, EyeSpacing, EyeSize,
        EyeVerticalPosition, EyebrowHeight, NoseWidth, NoseLength, NoseProjection,
        MouthWidth, LipFullness, EarSize, EarPosition
    }

    private readonly List<Guid> _playerIds = [];
    private readonly List<(GazePreviewTargetKind Kind, Guid? PlayerId)> _gazeTargets = [];
    private readonly Dictionary<PlayerAccessories, CheckButton> _accessoryChecks = [];
    private readonly Dictionary<FaceControl, SpinBox> _faceControls = [];
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
    private ColorPickerButton _eyeColor = null!;
    private OptionButton _expressionOption = null!;
    private Button _blinkButton = null!;
    private CheckButton _automaticBlink = null!;
    private OptionButton _gazeTarget = null!;
    private SpinBox _gazeHorizontal = null!;
    private SpinBox _gazeVertical = null!;
    private Button _applyGazeButton = null!;
    private Button _centerGazeButton = null!;

    public event Action<Guid>? AppearanceChanged;
    public event Action<string>? StatusChanged;
    public event Action<Guid, FacialExpressionState>? ExpressionPreviewRequested;
    public event Action<Guid>? BlinkRequested;
    public event Action<Guid, bool>? AutomaticBlinkChanged;
    public event Action<Guid, GazePreviewTargetKind, Guid?, float, float>? GazePreviewRequested;

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
        foreach (var control in _faceControls.Values)
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        foreach (var control in new Control[] { _eyeColor, _expressionOption, _gazeTarget, _gazeHorizontal, _gazeVertical })
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _blinkButton.Disabled = !enabled;
        _automaticBlink.Disabled = !enabled;
        _applyGazeButton.Disabled = !enabled;
        _centerGazeButton.Disabled = !enabled;
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

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        stack.AddChild(tabs);

        var scroll = new ScrollContainer
        {
            Name = "Body",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(scroll);

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

        BuildFaceEditor(tabs);
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
        RefreshGazeTargets();
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
        RefreshFaceEditor(appearance.Face);
    }

    private void OnPlayerSelected(long index)
    {
        if (_refreshing || index < 0 || index >= _playerIds.Count)
            return;
        _selectedPlayerId = _playerIds[(int)index];
        _refreshing = true;
        RefreshGazeTargets();
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

    private void OnFaceChanged(double _)
    {
        if (_refreshing || _selectedPlayerId == Guid.Empty)
            return;

        var appearance = _project.AppearanceFor(_selectedPlayerId);
        var face = appearance.Face;
        face.SetParameters(
            FaceValue(FaceControl.HeadWidth),
            FaceValue(FaceControl.HeadHeight),
            FaceValue(FaceControl.JawWidth),
            FaceValue(FaceControl.JawHeight),
            FaceValue(FaceControl.ChinWidth),
            FaceValue(FaceControl.ChinProjection),
            FaceValue(FaceControl.CheekboneWidth),
            FaceValue(FaceControl.CheekFullness),
            FaceValue(FaceControl.ForeheadHeight),
            FaceValue(FaceControl.EyeSpacing),
            FaceValue(FaceControl.EyeSize),
            FaceValue(FaceControl.EyeVerticalPosition),
            FaceValue(FaceControl.EyebrowHeight),
            FaceValue(FaceControl.NoseWidth),
            FaceValue(FaceControl.NoseLength),
            FaceValue(FaceControl.NoseProjection),
            FaceValue(FaceControl.MouthWidth),
            FaceValue(FaceControl.LipFullness),
            FaceValue(FaceControl.EarSize),
            FaceValue(FaceControl.EarPosition));

        _refreshing = true;
        RefreshFaceEditor(face);
        _refreshing = false;
        AppearanceChanged?.Invoke(_selectedPlayerId);
    }

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

    private void BuildFaceEditor(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Face",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(stack);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 4);
        stack.AddChild(grid);

        AddFaceField(grid, FaceControl.HeadWidth, "Head width");
        AddFaceField(grid, FaceControl.HeadHeight, "Head height");
        AddFaceField(grid, FaceControl.JawWidth, "Jaw width");
        AddFaceField(grid, FaceControl.JawHeight, "Jaw height");
        AddFaceField(grid, FaceControl.ChinWidth, "Chin width");
        AddFaceField(grid, FaceControl.ChinProjection, "Chin projection", true);
        AddFaceField(grid, FaceControl.CheekboneWidth, "Cheekbone width");
        AddFaceField(grid, FaceControl.CheekFullness, "Cheek fullness");
        AddFaceField(grid, FaceControl.ForeheadHeight, "Forehead height");
        AddFaceField(grid, FaceControl.EyeSpacing, "Eye spacing");
        AddFaceField(grid, FaceControl.EyeSize, "Eye size");
        AddFaceField(grid, FaceControl.EyeVerticalPosition, "Eye vertical", true);
        AddFaceField(grid, FaceControl.EyebrowHeight, "Eyebrow height");
        AddFaceField(grid, FaceControl.NoseWidth, "Nose width");
        AddFaceField(grid, FaceControl.NoseLength, "Nose length");
        AddFaceField(grid, FaceControl.NoseProjection, "Nose projection", true);
        AddFaceField(grid, FaceControl.MouthWidth, "Mouth width");
        AddFaceField(grid, FaceControl.LipFullness, "Lip fullness");
        AddFaceField(grid, FaceControl.EarSize, "Ear size");
        AddFaceField(grid, FaceControl.EarPosition, "Ear position", true);

        _eyeColor = CreateColorButton();
        _eyeColor.ColorChanged += color => UpdateAppearance(appearance => appearance.Face.SetEyeColor(ToDomain(color)));
        AddField(grid, "Eye color", _eyeColor);

        stack.AddChild(new HSeparator());
        stack.AddChild(new Label { Text = "Expression preview" });
        _expressionOption = CreateEnumOption<FacialExpressionState>();
        _expressionOption.ItemSelected += OnExpressionSelected;
        stack.AddChild(_expressionOption);
        var blinkRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(blinkRow);
        _blinkButton = new Button { Text = "Blink Test" };
        _blinkButton.Pressed += () => BlinkRequested?.Invoke(_selectedPlayerId);
        blinkRow.AddChild(_blinkButton);
        _automaticBlink = new CheckButton { Text = "Auto Blink" };
        _automaticBlink.Toggled += enabled =>
        {
            if (!_refreshing)
                AutomaticBlinkChanged?.Invoke(_selectedPlayerId, enabled);
        };
        blinkRow.AddChild(_automaticBlink);

        stack.AddChild(new HSeparator());
        stack.AddChild(new Label { Text = "Gaze test" });
        _gazeTarget = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddChild(_gazeTarget);
        var gazeGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddChild(gazeGrid);
        _gazeHorizontal = CreateSpinBox(-1, 1, 0.05);
        _gazeVertical = CreateSpinBox(-1, 1, 0.05);
        AddField(gazeGrid, "Horizontal", _gazeHorizontal);
        AddField(gazeGrid, "Vertical", _gazeVertical);
        var gazeButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(gazeButtons);
        _applyGazeButton = new Button { Text = "Apply Gaze" };
        _applyGazeButton.Pressed += OnApplyGaze;
        gazeButtons.AddChild(_applyGazeButton);
        _centerGazeButton = new Button { Text = "Center" };
        _centerGazeButton.Pressed += OnCenterGaze;
        gazeButtons.AddChild(_centerGazeButton);
    }

    private void AddFaceField(GridContainer grid, FaceControl faceControl, string label, bool offset = false)
    {
        var editor = offset
            ? CreateSpinBox(FaceAppearance.MinimumOffset, FaceAppearance.MaximumOffset, 0.01, " offset")
            : CreateSpinBox(FaceAppearance.MinimumScale, FaceAppearance.MaximumScale, 0.01, "×");
        editor.ValueChanged += OnFaceChanged;
        _faceControls.Add(faceControl, editor);
        AddField(grid, label, editor);
    }

    private void RefreshFaceEditor(FaceAppearance face)
    {
        SetFaceValue(FaceControl.HeadWidth, face.HeadWidth);
        SetFaceValue(FaceControl.HeadHeight, face.HeadHeight);
        SetFaceValue(FaceControl.JawWidth, face.JawWidth);
        SetFaceValue(FaceControl.JawHeight, face.JawHeight);
        SetFaceValue(FaceControl.ChinWidth, face.ChinWidth);
        SetFaceValue(FaceControl.ChinProjection, face.ChinProjection);
        SetFaceValue(FaceControl.CheekboneWidth, face.CheekboneWidth);
        SetFaceValue(FaceControl.CheekFullness, face.CheekFullness);
        SetFaceValue(FaceControl.ForeheadHeight, face.ForeheadHeight);
        SetFaceValue(FaceControl.EyeSpacing, face.EyeSpacing);
        SetFaceValue(FaceControl.EyeSize, face.EyeSize);
        SetFaceValue(FaceControl.EyeVerticalPosition, face.EyeVerticalPosition);
        SetFaceValue(FaceControl.EyebrowHeight, face.EyebrowHeight);
        SetFaceValue(FaceControl.NoseWidth, face.NoseWidth);
        SetFaceValue(FaceControl.NoseLength, face.NoseLength);
        SetFaceValue(FaceControl.NoseProjection, face.NoseProjection);
        SetFaceValue(FaceControl.MouthWidth, face.MouthWidth);
        SetFaceValue(FaceControl.LipFullness, face.LipFullness);
        SetFaceValue(FaceControl.EarSize, face.EarSize);
        SetFaceValue(FaceControl.EarPosition, face.EarPosition);
        _eyeColor.Color = ToGodot(face.EyeColor);
    }

    private void RefreshGazeTargets()
    {
        _gazeTargets.Clear();
        _gazeTarget.Clear();
        AddGazeTarget("Manual H/V", GazePreviewTargetKind.Manual, null);
        AddGazeTarget("Football", GazePreviewTargetKind.Football, null);
        foreach (var player in _game.Gold.Roster.Concat(_game.Navy.Roster).Where(player => player.Id != _selectedPlayerId))
            AddGazeTarget($"{player.Team?.Name} #{player.JerseyNumber} {player.Name}", GazePreviewTargetKind.Player, player.Id);
        _gazeTarget.Select(0);
    }

    private void AddGazeTarget(string label, GazePreviewTargetKind kind, Guid? playerId)
    {
        _gazeTarget.AddItem(label);
        _gazeTargets.Add((kind, playerId));
    }

    private void OnExpressionSelected(long index)
    {
        if (_refreshing || _selectedPlayerId == Guid.Empty)
            return;
        ExpressionPreviewRequested?.Invoke(
            _selectedPlayerId,
            (FacialExpressionState)_expressionOption.GetItemId((int)index));
    }

    private void OnApplyGaze()
    {
        if (_selectedPlayerId == Guid.Empty || _gazeTarget.Selected < 0 || _gazeTarget.Selected >= _gazeTargets.Count)
            return;
        var target = _gazeTargets[_gazeTarget.Selected];
        GazePreviewRequested?.Invoke(
            _selectedPlayerId,
            target.Kind,
            target.PlayerId,
            (float)_gazeHorizontal.Value,
            (float)_gazeVertical.Value);
    }

    private void OnCenterGaze()
    {
        _gazeTarget.Select(0);
        _gazeHorizontal.Value = 0;
        _gazeVertical.Value = 0;
        GazePreviewRequested?.Invoke(_selectedPlayerId, GazePreviewTargetKind.Manual, null, 0, 0);
    }

    private float FaceValue(FaceControl control) => (float)_faceControls[control].Value;
    private void SetFaceValue(FaceControl control, float value) => _faceControls[control].Value = value;

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
