using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Tts;
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
    private IReadOnlyList<PiperVoiceModelInfo> _voiceModels = [];
    private readonly List<PiperVoiceModelInfo> _displayVoiceModels = [];
    private VoiceSettingsSnapshot? _copiedVoiceSettings;
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
    private SpinBox _hairLength = null!;
    private SpinBox _hairVolume = null!;
    private SpinBox _hairlineHeight = null!;
    private SpinBox _partPosition = null!;
    private SpinBox _curlAmount = null!;
    private SpinBox _ponytailLength = null!;
    private SpinBox _ponytailVolume = null!;
    private SpinBox _bunSize = null!;
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
    private OptionButton _mouthShape = null!;
    private SpinBox _jawOpen = null!;
    private SpinBox _speechLipWidth = null!;
    private SpinBox _speechLipFullness = null!;
    private SpinBox _upperLip = null!;
    private SpinBox _lowerLip = null!;
    private Button _cycleSpeechButton = null!;
    private Label _rosterVoiceStatus = null!;
    private Label _voiceConfiguredStatus = null!;
    private Label _voiceModelStatus = null!;
    private Label _voiceReferenceAudio = null!;
    private Label _voiceTestStatus = null!;
    private OptionButton _voiceBackend = null!;
    private OptionButton _voiceModel = null!;
    private LineEdit _voiceSpeaker = null!;
    private SpinBox _voiceRate = null!;
    private SpinBox _voicePitch = null!;
    private SpinBox _voiceVolume = null!;
    private LineEdit _voiceStyle = null!;
    private LineEdit _voiceEmotion = null!;
    private Button _voiceAssignButton = null!;
    private Button _voiceTestButton = null!;
    private Button _voiceRefreshButton = null!;
    private Button _voiceCopyButton = null!;
    private Button _voicePasteButton = null!;
    private Button _voiceClearButton = null!;

    public event Action<Guid>? AppearanceChanged;
    public event Action<string>? StatusChanged;
    public event Action<Guid, FacialExpressionState>? ExpressionPreviewRequested;
    public event Action<Guid>? BlinkRequested;
    public event Action<Guid, bool>? AutomaticBlinkChanged;
    public event Action<Guid, GazePreviewTargetKind, Guid?, float, float>? GazePreviewRequested;
    public event Action<Guid, SpeechMouthShape, float, float, float, float, float>? MouthPreviewRequested;
    public event Action<Guid>? SpeechShapeCycleRequested;
    public event Action? VoiceModelsRefreshRequested;
    public event Action<PlayerVoiceProfile>? VoiceProfileChanged;
    public event Action<PlayerVoiceProfile>? VoiceTestRequested;

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

    public Guid SelectedPlayerId => _selectedPlayerId;
    public string RosterVoiceSummary => _rosterVoiceStatus?.Text ?? string.Empty;
    public bool SaveSelectedVoice() => SaveVoiceProfile();
    public void ClearSelectedVoice() => ClearVoiceProfile();
    public void CopySelectedVoiceSettings() => CopyVoiceSettings();
    public void PasteSelectedVoiceSettings() => PasteVoiceSettings();
    public void RequestSelectedVoiceTest() => TestVoice();

    public void SelectPlayer(Guid playerId)
    {
        var index = _playerIds.IndexOf(playerId);
        if (index < 0) throw new KeyNotFoundException("The requested player is not in Player Studio.");
        _playerOption.Select(index);
        OnPlayerSelected(index);
    }

    public void SetVoiceModels(IReadOnlyList<PiperVoiceModelInfo> models)
    {
        _voiceModels = models ?? [];
        if (_voiceModel is null) return;
        RefreshVoiceModelOptions(_selectedPlayerId == Guid.Empty ? null : _project.VoiceProfileFor(_selectedPlayerId));
        if (_selectedPlayerId != Guid.Empty) RefreshVoiceRosterStatus();
    }

    public void SetVoiceTestStatus(TtsGenerationState state, string message)
    {
        if (_voiceTestStatus is null) return;
        _voiceTestStatus.Text = $"Test: {state} — {message}";
        _voiceTestButton.Disabled = state is TtsGenerationState.Queued or TtsGenerationState.Generating;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _playerOption.Disabled = !enabled;
        foreach (var control in new Control[] { _height, _bodyBuild, _shoulderWidth, _chestWidth, _waistWidth, _hipWidth, _armLength, _legLength, _skinTone, _hairStyle, _hairColor, _hairLength, _hairVolume, _hairlineHeight, _partPosition, _curlAmount, _ponytailLength, _ponytailVolume, _bunSize, _jerseyNumber, _primaryColor, _secondaryColor, _flagColor })
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
        foreach (var control in new Control[] { _mouthShape, _jawOpen, _speechLipWidth, _speechLipFullness, _upperLip, _lowerLip })
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _cycleSpeechButton.Disabled = !enabled;
        foreach (var control in new Control[] { _voiceBackend, _voiceModel, _voiceSpeaker, _voiceRate, _voicePitch, _voiceVolume, _voiceStyle, _voiceEmotion })
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        foreach (var button in new[] { _voiceAssignButton, _voiceTestButton, _voiceRefreshButton, _voiceCopyButton, _voicePasteButton, _voiceClearButton })
            button.Disabled = !enabled;
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

        _playerOption = new OptionButton { Name = "PlayerSelector", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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

        BuildHairEditor(tabs);
        BuildFaceEditor(tabs);
        BuildMouthEditor(tabs);
        BuildVoiceEditor(tabs);
    }

    private void RefreshPlayers()
    {
        _refreshing = true;
        _playerIds.Clear();
        _playerOption.Clear();
        foreach (var player in _game.Gold.Roster.Concat(_game.Navy.Roster))
        {
            _playerIds.Add(player.Id);
            _playerOption.AddItem(PlayerOptionText(player));
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
        RefreshHairEditor(appearance.Hair);
        _jerseyNumber.Value = player.JerseyNumber;
        _primaryColor.Color = ToGodot(uniform.PrimaryColor);
        _secondaryColor.Color = ToGodot(uniform.SecondaryColor);
        _flagColor.Color = ToGodot(uniform.FlagColor);
        foreach (var accessory in _accessoryChecks)
            accessory.Value.ButtonPressed = appearance.Accessories.HasFlag(accessory.Key);
        RefreshFaceEditor(appearance.Face);
        RefreshVoiceEditor();
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

    private void OnHairChanged(double _) => ApplyHairEditor();

    private void OnHairStyleChanged(long _) => ApplyHairEditor();

    private void OnHairColorChanged(Color color) =>
        UpdateAppearance(appearance => appearance.Hair.SetColor(ToDomain(color)));

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
            _playerOption.SetItemText(index, PlayerOptionText(player));
    }

    private void AddAccessory(Node parent, string label, PlayerAccessories accessory)
    {
        var check = new CheckButton { Text = label };
        check.Toggled += OnAccessoriesChanged;
        parent.AddChild(check);
        _accessoryChecks.Add(accessory, check);
    }

    private void BuildHairEditor(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Hair",
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

        _hairStyle = CreateHairStyleOption();
        _hairStyle.ItemSelected += OnHairStyleChanged;
        AddField(grid, "Style", _hairStyle);
        _hairColor = CreateColorButton();
        _hairColor.ColorChanged += OnHairColorChanged;
        AddField(grid, "Color", _hairColor);
        _hairLength = AddHairSpinBox(grid, "Length", HairAppearance.MinimumLength, HairAppearance.MaximumLength);
        _hairVolume = AddHairSpinBox(grid, "Volume", HairAppearance.MinimumVolume, HairAppearance.MaximumVolume);
        _hairlineHeight = AddHairSpinBox(grid, "Hairline height", HairAppearance.MinimumHairlineHeight, HairAppearance.MaximumHairlineHeight, " offset");
        _partPosition = AddHairSpinBox(grid, "Part position", HairAppearance.MinimumPartPosition, HairAppearance.MaximumPartPosition);
        _curlAmount = AddHairSpinBox(grid, "Curl / wave", HairAppearance.MinimumCurlAmount, HairAppearance.MaximumCurlAmount);

        stack.AddChild(new Label { Text = "Ponytail / bun" });
        var tiedGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tiedGrid.AddThemeConstantOverride("h_separation", 10);
        tiedGrid.AddThemeConstantOverride("v_separation", 4);
        stack.AddChild(tiedGrid);
        _ponytailLength = AddHairSpinBox(tiedGrid, "Ponytail length", HairAppearance.MinimumPonytailLength, HairAppearance.MaximumPonytailLength);
        _ponytailVolume = AddHairSpinBox(tiedGrid, "Ponytail volume", HairAppearance.MinimumPonytailVolume, HairAppearance.MaximumPonytailVolume);
        _bunSize = AddHairSpinBox(tiedGrid, "Bun size", HairAppearance.MinimumBunSize, HairAppearance.MaximumBunSize);
        stack.AddChild(new Label
        {
            Text = "Tied-hair controls affect Ponytail or Bun styles.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private SpinBox AddHairSpinBox(GridContainer grid, string label, double minimum, double maximum, string suffix = "×")
    {
        var editor = CreateSpinBox(minimum, maximum, 0.01, suffix);
        editor.ValueChanged += OnHairChanged;
        AddField(grid, label, editor);
        return editor;
    }

    private void ApplyHairEditor()
    {
        if (_refreshing || _selectedPlayerId == Guid.Empty)
            return;
        var hair = _project.AppearanceFor(_selectedPlayerId).Hair;
        hair.SetParameters(
            (HairStyle)_hairStyle.GetItemId(_hairStyle.Selected),
            (float)_hairLength.Value,
            (float)_hairVolume.Value,
            (float)_hairlineHeight.Value,
            (float)_partPosition.Value,
            (float)_curlAmount.Value,
            (float)_ponytailLength.Value,
            (float)_ponytailVolume.Value,
            (float)_bunSize.Value);
        _refreshing = true;
        RefreshHairEditor(hair);
        _refreshing = false;
        AppearanceChanged?.Invoke(_selectedPlayerId);
    }

    private void RefreshHairEditor(HairAppearance hair)
    {
        SelectItemById(_hairStyle, (int)hair.Style);
        _hairColor.Color = ToGodot(hair.Color);
        _hairLength.Value = hair.Length;
        _hairVolume.Value = hair.Volume;
        _hairlineHeight.Value = hair.HairlineHeight;
        _partPosition.Value = hair.PartPosition;
        _curlAmount.Value = hair.CurlAmount;
        _ponytailLength.Value = hair.PonytailLength;
        _ponytailVolume.Value = hair.PonytailVolume;
        _bunSize.Value = hair.BunSize;
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

    private void BuildMouthEditor(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Mouth",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(stack);
        stack.AddChild(new Label
        {
            Text = "Transient speech-shape preview",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 4);
        stack.AddChild(grid);

        _mouthShape = CreateMouthShapeOption();
        _mouthShape.ItemSelected += _ => OnMouthPreviewChanged();
        AddField(grid, "Mouth shape", _mouthShape);
        _jawOpen = CreateMouthSpinBox(grid, "Jaw open", 0, 1, 0);
        _speechLipWidth = CreateMouthSpinBox(grid, "Lip width", 0.75, 1.25, 1, "×");
        _speechLipFullness = CreateMouthSpinBox(grid, "Lip fullness", 0.75, 1.25, 1, "×");
        _upperLip = CreateMouthSpinBox(grid, "Upper lip", -1, 1, 0);
        _lowerLip = CreateMouthSpinBox(grid, "Lower lip", -1, 1, 0);

        _cycleSpeechButton = new Button { Text = "Cycle Speech Shapes" };
        _cycleSpeechButton.Pressed += () => SpeechShapeCycleRequested?.Invoke(_selectedPlayerId);
        stack.AddChild(_cycleSpeechButton);
        stack.AddChild(new Label
        {
            Text = "Speech previews layer over the selected facial expression and are not saved.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private void BuildVoiceEditor(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Voice",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        tabs.AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(stack);

        _rosterVoiceStatus = new Label { Name = "RosterVoiceStatus", Text = "Voices configured: 0 / 0" };
        _rosterVoiceStatus.AddThemeFontSizeOverride("font_size", 16);
        stack.AddChild(_rosterVoiceStatus);
        stack.AddChild(new Label { Text = "● configured   ○ intentionally silent" });
        _voiceConfiguredStatus = new Label
        {
            Text = "Not configured — ambient speech is intentionally silent",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stack.AddChild(_voiceConfiguredStatus);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 4);
        stack.AddChild(grid);

        _voiceBackend = CreateEnumOption<TtsBackendType>();
        _voiceBackend.Name = "VoiceBackend";
        _voiceBackend.ItemSelected += _ => RefreshVoiceModelStatus();
        AddField(grid, "Provider / backend", _voiceBackend);
        _voiceModel = new OptionButton { Name = "VoiceModel", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _voiceModel.ItemSelected += _ => RefreshVoiceModelStatus();
        AddField(grid, "Piper model", _voiceModel);
        _voiceSpeaker = new LineEdit { Name = "VoiceSpeaker", PlaceholderText = "Optional multi-speaker ID" };
        AddField(grid, "Speaker ID", _voiceSpeaker);
        _voiceReferenceAudio = new Label { Text = "None", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddField(grid, "Reference audio", _voiceReferenceAudio);
        _voiceRate = CreateSpinBox(0.5, 2, 0.05, "×"); _voiceRate.Name = "VoiceRate";
        AddField(grid, "Speaking rate", _voiceRate);
        _voicePitch = CreateSpinBox(-12, 12, 0.25, " st"); _voicePitch.Name = "VoicePitch";
        AddField(grid, "Pitch", _voicePitch);
        _voiceVolume = CreateSpinBox(0, 1, 0.05); _voiceVolume.Name = "VoiceVolume";
        AddField(grid, "Volume", _voiceVolume);
        _voiceStyle = new LineEdit { Name = "VoiceStyle", PlaceholderText = "Optional provider style" };
        AddField(grid, "Style", _voiceStyle);
        _voiceEmotion = new LineEdit { Name = "VoiceEmotion", PlaceholderText = "Optional provider emotion" };
        AddField(grid, "Emotion", _voiceEmotion);

        _voiceModelStatus = new Label
        {
            Text = "No Piper model selected",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stack.AddChild(_voiceModelStatus);

        var modelActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(modelActions);
        _voiceRefreshButton = AddVoiceButton(modelActions, "Refresh Models", () => VoiceModelsRefreshRequested?.Invoke());
        _voiceAssignButton = AddVoiceButton(modelActions, "Save Voice", () => SaveVoiceProfile());
        _voiceClearButton = AddVoiceButton(modelActions, "Clear Voice", ClearVoiceProfile);

        var convenienceActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(convenienceActions);
        _voiceCopyButton = AddVoiceButton(convenienceActions, "Copy Voice Settings", CopyVoiceSettings);
        _voicePasteButton = AddVoiceButton(convenienceActions, "Paste Voice Settings", PasteVoiceSettings);
        _voiceTestButton = AddVoiceButton(convenienceActions, "Test Voice", TestVoice);
        _voiceTestStatus = new Label
        {
            Text = "Test: Idle — generates “Ready for the next play.” locally",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stack.AddChild(_voiceTestStatus);
        stack.AddChild(new Label
        {
            Text = "Models are discovered from tools/tts/models. Flag Football Studio never downloads voices automatically.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private static Button AddVoiceButton(Node parent, string text, Action pressed)
    {
        var button = new Button { Name = text.Replace(" ", string.Empty), Text = text };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    private void RefreshVoiceEditor()
    {
        if (_selectedPlayerId == Guid.Empty || _voiceBackend is null) return;
        var profile = _project.VoiceProfileFor(_selectedPlayerId);
        SelectItemById(_voiceBackend, (int)profile.BackendType);
        _voiceSpeaker.Text = profile.SpeakerId ?? string.Empty;
        _voiceRate.Value = profile.DefaultSpeakingRate;
        _voicePitch.Value = profile.DefaultPitchAdjustment;
        _voiceVolume.Value = profile.DefaultSpeakingVolume;
        _voiceStyle.Text = profile.Style ?? string.Empty;
        _voiceEmotion.Text = profile.Emotion ?? string.Empty;
        _voiceReferenceAudio.Text = profile.ReferenceAudio?.RelativePath ?? "None";
        RefreshVoiceModelOptions(profile);
        RefreshVoiceRosterStatus();
    }

    private void RefreshVoiceModelOptions(PlayerVoiceProfile? profile)
    {
        if (_voiceModel is null) return;
        _displayVoiceModels.Clear();
        _voiceModel.Clear();
        foreach (var model in _voiceModels)
        {
            _displayVoiceModels.Add(model);
            _voiceModel.AddItem(model.IsCompatible ? model.Name : $"{model.Name} (incomplete)");
            _voiceModel.SetItemDisabled(_voiceModel.ItemCount - 1, !model.IsCompatible);
        }

        if (!string.IsNullOrWhiteSpace(profile?.ModelIdOrPath) &&
            !_displayVoiceModels.Any(model => SamePath(model.ModelPath, profile.ModelIdOrPath)))
        {
            var configured = InspectModel(profile.ModelIdOrPath);
            _displayVoiceModels.Add(configured);
            _voiceModel.AddItem(configured.IsCompatible
                ? $"{configured.Name} (configured)"
                : $"{configured.Name} (configured, missing)");
            _voiceModel.SetItemDisabled(_voiceModel.ItemCount - 1, !configured.IsCompatible);
        }

        var selected = profile is null ? -1 : _displayVoiceModels.FindIndex(model => SamePath(model.ModelPath, profile.ModelIdOrPath));
        if (selected >= 0) _voiceModel.Select(selected);
        else if (_voiceModel.ItemCount > 0) _voiceModel.Select(0);
        RefreshVoiceModelStatus();
    }

    private void RefreshVoiceModelStatus()
    {
        if (_voiceModelStatus is null) return;
        if ((TtsBackendType)_voiceBackend.GetSelectedId() == TtsBackendType.None)
        {
            _voiceModelStatus.Text = "TTS disabled. Ambient phrases for this player remain intentionally silent.";
            return;
        }
        if (_voiceModel.Selected < 0 || _voiceModel.Selected >= _displayVoiceModels.Count)
        {
            _voiceModelStatus.Text = "No installed Piper models found. Add an .onnx and matching .onnx.json, then refresh.";
            return;
        }
        var model = _displayVoiceModels[_voiceModel.Selected];
        _voiceModelStatus.Text = $"{model.Name}: {model.AvailabilityText}\n{model.ModelPath}";
    }

    private bool SaveVoiceProfile()
    {
        if (_selectedPlayerId == Guid.Empty) return false;
        try
        {
            var profile = _project.VoiceProfileFor(_selectedPlayerId);
            var backend = (TtsBackendType)_voiceBackend.GetSelectedId();
            string? modelPath = null;
            if (backend == TtsBackendType.PiperLocal)
            {
                if (_voiceModel.Selected < 0 || _voiceModel.Selected >= _displayVoiceModels.Count)
                    throw new InvalidOperationException("Select a discovered Piper model first.");
                var model = _displayVoiceModels[_voiceModel.Selected];
                if (!model.IsCompatible) throw new InvalidOperationException(model.AvailabilityText);
                modelPath = model.ModelPath;
            }
            profile.Update(profile.DisplayName, profile.Description, (float)_voiceVolume.Value,
                (float)_voicePitch.Value, (float)_voiceRate.Value);
            profile.ConfigureTts(backend, modelPath, _voiceSpeaker.Text, profile.ReferenceAudio,
                _voiceStyle.Text, _voiceEmotion.Text);
            VoiceProfileChanged?.Invoke(profile);
            RefreshAllPlayerVoiceLabels();
            StatusChanged?.Invoke($"Voice saved for {PlayerIdentity(SelectedPlayer())}");
            return true;
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(exception.Message);
            SetVoiceTestStatus(TtsGenerationState.Failed, exception.Message);
            return false;
        }
    }

    private void ClearVoiceProfile()
    {
        if (_selectedPlayerId == Guid.Empty) return;
        var profile = _project.VoiceProfileFor(_selectedPlayerId);
        profile.ConfigureTts(TtsBackendType.None, null, null, null, null, null);
        VoiceProfileChanged?.Invoke(profile);
        RefreshAllPlayerVoiceLabels();
        SetVoiceTestStatus(TtsGenerationState.Idle, "Voice cleared; ambient speech is intentionally silent");
        StatusChanged?.Invoke($"Voice cleared for {PlayerIdentity(SelectedPlayer())}");
    }

    private void CopyVoiceSettings()
    {
        if (_selectedPlayerId == Guid.Empty) return;
        _copiedVoiceSettings = VoiceSettingsSnapshot.From(_project.VoiceProfileFor(_selectedPlayerId));
        StatusChanged?.Invoke("Voice settings copied. Select another player and choose Paste Voice Settings.");
        _voicePasteButton.Disabled = false;
    }

    private void PasteVoiceSettings()
    {
        if (_selectedPlayerId == Guid.Empty || _copiedVoiceSettings is null)
        {
            StatusChanged?.Invoke("Copy voice settings from a player first.");
            return;
        }
        var profile = _project.VoiceProfileFor(_selectedPlayerId);
        _copiedVoiceSettings.Apply(profile);
        VoiceProfileChanged?.Invoke(profile);
        RefreshAllPlayerVoiceLabels();
        StatusChanged?.Invoke($"Voice settings pasted to {PlayerIdentity(SelectedPlayer())}");
    }

    private void TestVoice()
    {
        if (!SaveVoiceProfile() || _selectedPlayerId == Guid.Empty) return;
        var profile = _project.VoiceProfileFor(_selectedPlayerId);
        if (!IsVoiceConfigured(profile)) return;
        SetVoiceTestStatus(TtsGenerationState.Queued, $"Generation started with {PathName(profile.ModelIdOrPath)}");
        VoiceTestRequested?.Invoke(profile);
    }

    private void RefreshAllPlayerVoiceLabels()
    {
        foreach (var player in _game.Gold.Roster.Concat(_game.Navy.Roster))
            UpdatePlayerLabel(player);
        RefreshVoiceEditor();
    }

    private void RefreshVoiceRosterStatus()
    {
        var profiles = _game.Gold.Roster.Concat(_game.Navy.Roster)
            .Select(player => _project.VoiceProfileFor(player.Id)).ToArray();
        _rosterVoiceStatus.Text = $"Voices configured: {profiles.Count(IsVoiceConfigured)} / {profiles.Length}";
        var selected = _project.VoiceProfileFor(_selectedPlayerId);
        _voiceConfiguredStatus.Text = IsVoiceConfigured(selected)
            ? $"Configured — Piper / {PathName(selected.ModelIdOrPath)}"
            : "Not configured — ambient speech is intentionally silent";
    }

    private string PlayerOptionText(Player player) =>
        $"{(IsVoiceConfigured(_project.VoiceProfileFor(player.Id)) ? "●" : "○")} {PlayerIdentity(player)} {player.Name}";

    private static string PlayerIdentity(Player player) => $"{player.Team?.Name} #{player.JerseyNumber}";

    private static bool IsVoiceConfigured(PlayerVoiceProfile profile) =>
        profile.BackendType == TtsBackendType.PiperLocal && InspectModel(profile.ModelIdOrPath).IsCompatible;

    private static PiperVoiceModelInfo InspectModel(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return new PiperVoiceModelInfo("No model selected", string.Empty, string.Empty, false, false);
        var full = System.IO.Path.GetFullPath(modelPath);
        return new PiperVoiceModelInfo(System.IO.Path.GetFileNameWithoutExtension(full), full, full + ".json",
            System.IO.File.Exists(full), System.IO.File.Exists(full + ".json"));
    }

    private static bool SamePath(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static string PathName(string? path) => string.IsNullOrWhiteSpace(path)
        ? "no model" : System.IO.Path.GetFileNameWithoutExtension(path);

    private sealed record VoiceSettingsSnapshot(
        float Volume, float Pitch, float Rate, TtsBackendType Backend, string? Model,
        string? Speaker, VoiceAudioReference? Reference, string? Style, string? Emotion)
    {
        public static VoiceSettingsSnapshot From(PlayerVoiceProfile profile) => new(
            profile.DefaultSpeakingVolume, profile.DefaultPitchAdjustment, profile.DefaultSpeakingRate,
            profile.BackendType, profile.ModelIdOrPath, profile.SpeakerId, profile.ReferenceAudio,
            profile.Style, profile.Emotion);

        public void Apply(PlayerVoiceProfile profile)
        {
            profile.Update(profile.DisplayName, profile.Description, Volume, Pitch, Rate);
            profile.ConfigureTts(Backend, Model, Speaker, Reference, Style, Emotion);
        }
    }

    private SpinBox CreateMouthSpinBox(GridContainer grid, string label, double minimum, double maximum, double value, string suffix = "")
    {
        var editor = CreateSpinBox(minimum, maximum, 0.01, suffix);
        editor.Value = value;
        editor.ValueChanged += _ => OnMouthPreviewChanged();
        AddField(grid, label, editor);
        return editor;
    }

    private void OnMouthPreviewChanged()
    {
        if (_refreshing || _selectedPlayerId == Guid.Empty)
            return;
        MouthPreviewRequested?.Invoke(
            _selectedPlayerId,
            (SpeechMouthShape)_mouthShape.GetItemId(_mouthShape.Selected),
            (float)_jawOpen.Value,
            (float)_speechLipWidth.Value,
            (float)_speechLipFullness.Value,
            (float)_upperLip.Value,
            (float)_lowerLip.Value);
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

    private static OptionButton CreateHairStyleOption()
    {
        var option = new OptionButton();
        foreach (var style in new[]
        {
            HairStyle.None,
            HairStyle.BuzzCut,
            HairStyle.Short,
            HairStyle.Medium,
            HairStyle.Long,
            HairStyle.Curly,
            HairStyle.Ponytail,
            HairStyle.Bun,
            HairStyle.Mohawk
        })
            option.AddItem(style == HairStyle.Mohawk ? "Mohawk (Legacy)" : SplitName(style.ToString()), (int)style);
        return option;
    }

    private static OptionButton CreateMouthShapeOption()
    {
        var option = new OptionButton();
        foreach (var shape in Enum.GetValues<SpeechMouthShape>())
        {
            var label = shape switch
            {
                SpeechMouthShape.Mbp => "M / B / P",
                SpeechMouthShape.Fv => "F / V",
                SpeechMouthShape.Wq => "W / Q",
                _ => shape.ToString()
            };
            option.AddItem(label, (int)shape);
        }
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
