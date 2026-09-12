using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class CameraDirectorPanel : PanelContainer
{
    private readonly List<Guid> _cameraIds = [];
    private readonly List<Guid> _playerIds = [];
    private readonly List<Guid> _cutIds = [];
    private readonly List<Button> _buttons = [];
    private readonly SpinBox[] _position = new SpinBox[3];
    private readonly SpinBox[] _rotation = new SpinBox[3];
    private readonly SpinBox[] _povWorldTarget = new SpinBox[3];
    private GameProject _project = null!;
    private Game _game = null!;
    private PlayDefinition _play = null!;
    private Guid _activeCameraId;
    private bool _refreshing;
    private ItemList _cameraList = null!;
    private ItemList _cutList = null!;
    private LineEdit _nameEdit = null!;
    private OptionButton _typeOption = null!;
    private OptionButton _playerOption = null!;
    private OptionButton _povModeOption = null!;
    private OptionButton _povTargetKindOption = null!;
    private OptionButton _povTargetPlayerOption = null!;
    private SpinBox _mouseSensitivity = null!;
    private SpinBox _controllerSensitivity = null!;
    private SpinBox _stabilization = null!;
    private SpinBox _headBob = null!;
    private SpinBox _pitchOffset = null!;
    private SpinBox _forwardOffset = null!;
    private SpinBox _nearClip = null!;
    private Button _recenterButton = null!;
    private SpinBox _fov = null!;
    private SpinBox _cutTime = null!;

    public event Action<Guid>? CameraSelected;
    public event Action? CreateRequested;
    public event Action<Guid, string>? RenameRequested;
    public event Action<Guid>? DuplicateRequested;
    public event Action<Guid>? DeleteRequested;
    public event Action<Guid, CameraType>? TypeChanged;
    public event Action<Guid, CameraVector, CameraVector, float>? FreeCameraChanged;
    public event Action<Guid, Guid?>? PlayerPovChanged;
    public event Action<Guid, PlayerPovSettings>? PlayerPovSettingsChanged;
    public event Action<Guid>? PlayerPovRecenterRequested;
    public event Action<Guid>? PreviewRequested;
    public event Action<Guid, double>? AddCutRequested;
    public event Action<Guid>? DeleteCutRequested;

    public void Configure(GameProject project, Game game, PlayDefinition play, Guid activeCameraId)
    {
        BuildUi();
        SetProject(project, game, play, activeCameraId);
    }

    public void SetProject(GameProject project, Game game, PlayDefinition play, Guid activeCameraId)
    {
        _project = project;
        _game = game;
        _play = play;
        _activeCameraId = activeCameraId;
        RefreshPlayers();
        RefreshAll();
    }

    public void SetPlay(PlayDefinition play)
    {
        _play = play;
        RefreshCuts();
    }

    public void RefreshPlayerLabels()
    {
        _refreshing = true;
        RefreshPlayers();
        RefreshEditor();
        _refreshing = false;
    }

    public void SetActiveCamera(Guid cameraId)
    {
        _activeCameraId = cameraId;
        RefreshAll();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        foreach (var button in _buttons)
            button.Disabled = !enabled;
        _cameraList.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _cutList.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _nameEdit.Editable = enabled;
        _typeOption.Disabled = !enabled;
        UpdateFieldAvailability(enabled);
    }

    public void RefreshAll()
    {
        _refreshing = true;
        _cameraIds.Clear();
        _cameraList.Clear();
        var selectedIndex = -1;
        for (var index = 0; index < _project.Cameras.Count; index++)
        {
            var camera = _project.Cameras[index];
            _cameraIds.Add(camera.Id);
            _cameraList.AddItem(camera.Name);
            if (camera.Id == _activeCameraId)
                selectedIndex = index;
        }
        if (selectedIndex >= 0)
            _cameraList.Select(selectedIndex);
        RefreshEditor();
        RefreshCuts();
        _refreshing = false;
    }

    private void BuildUi()
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(scroll);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 5);
        margin.AddChild(stack);

        var title = new Label { Text = "CAMERA DIRECTOR", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(title);

        _cameraList = new ItemList { CustomMinimumSize = new Vector2(0, 62), SelectMode = ItemList.SelectModeEnum.Single };
        _cameraList.ItemSelected += OnCameraSelected;
        stack.AddChild(_cameraList);

        _nameEdit = new LineEdit { PlaceholderText = "Camera name" };
        stack.AddChild(_nameEdit);

        var cameraActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(cameraActions);
        AddButton(cameraActions, "New", () => CreateRequested?.Invoke());
        AddButton(cameraActions, "Rename", RequestRename);
        AddButton(cameraActions, "Duplicate", RequestDuplicate);
        AddButton(cameraActions, "Delete", RequestDelete);

        var typeRow = new HBoxContainer();
        stack.AddChild(typeRow);
        typeRow.AddChild(new Label { Text = "Type", CustomMinimumSize = new Vector2(68, 0) });
        _typeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var type in Enum.GetValues<CameraType>())
            _typeOption.AddItem(DisplayName(type), (int)type);
        _typeOption.ItemSelected += OnTypeSelected;
        typeRow.AddChild(_typeOption);

        var playerRow = new HBoxContainer();
        stack.AddChild(playerRow);
        playerRow.AddChild(new Label { Text = "POV Player", CustomMinimumSize = new Vector2(68, 0) });
        _playerOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _playerOption.ItemSelected += OnPlayerSelected;
        playerRow.AddChild(_playerOption);

        var povModeRow = new HBoxContainer();
        stack.AddChild(povModeRow);
        povModeRow.AddChild(new Label { Text = "POV Mode", CustomMinimumSize = new Vector2(68, 0) });
        _povModeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var mode in Enum.GetValues<PlayerPovMode>())
            _povModeOption.AddItem(PovModeName(mode), (int)mode);
        _povModeOption.ItemSelected += OnPovSettingsValueChanged;
        povModeRow.AddChild(_povModeOption);
        _recenterButton = AddButton(povModeRow, "Recenter", RequestPovRecenter);

        var povSettings = new GridContainer { Columns = 4 };
        stack.AddChild(povSettings);
        _mouseSensitivity = AddPovField(povSettings, "Mouse", 0.01, 1, 0.01);
        _controllerSensitivity = AddPovField(povSettings, "Controller", 10, 360, 5);
        _stabilization = AddPovField(povSettings, "Stabilize", 0, 1, 0.05);
        _headBob = AddPovField(povSettings, "Head bob", 0, 1, 0.05);
        _pitchOffset = AddPovField(povSettings, "Pitch", -30, 30, 1);
        _forwardOffset = AddPovField(povSettings, "Forward", 0, 0.25, 0.005);
        _nearClip = AddPovField(povSettings, "Near clip", 0.005, 0.2, 0.005);

        var targetRow = new HBoxContainer();
        stack.AddChild(targetRow);
        targetRow.AddChild(new Label { Text = "Look At", CustomMinimumSize = new Vector2(68, 0) });
        _povTargetKindOption = new OptionButton { CustomMinimumSize = new Vector2(105, 0) };
        foreach (var kind in Enum.GetValues<PlayerPovTargetKind>())
            _povTargetKindOption.AddItem(PovTargetName(kind), (int)kind);
        _povTargetKindOption.ItemSelected += OnPovSettingsValueChanged;
        targetRow.AddChild(_povTargetKindOption);
        _povTargetPlayerOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _povTargetPlayerOption.ItemSelected += OnPovSettingsValueChanged;
        targetRow.AddChild(_povTargetPlayerOption);

        BuildPovTargetRow(stack);

        BuildVectorRow(stack, "Position", _position, -50, 50);
        BuildVectorRow(stack, "Rotation", _rotation, -360, 360);

        var fovRow = new HBoxContainer();
        stack.AddChild(fovRow);
        fovRow.AddChild(new Label { Text = "FOV", CustomMinimumSize = new Vector2(68, 0) });
        _fov = CreateSpinBox(10, 120);
        _fov.ValueChanged += OnFreeValueChanged;
        fovRow.AddChild(_fov);
        AddButton(fovRow, "Preview", RequestPreview);

        stack.AddChild(new HSeparator());
        stack.AddChild(new Label { Text = "TIMED CUTS" });
        _cutList = new ItemList { CustomMinimumSize = new Vector2(0, 52), SelectMode = ItemList.SelectModeEnum.Single };
        stack.AddChild(_cutList);

        var cutActions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddChild(cutActions);
        cutActions.AddChild(new Label { Text = "At" });
        _cutTime = CreateSpinBox(0, 60, 0.1);
        _cutTime.Suffix = " s";
        cutActions.AddChild(_cutTime);
        AddButton(cutActions, "Add Cut", RequestAddCut);
        AddButton(cutActions, "Delete Cut", RequestDeleteCut);
    }

    private void BuildVectorRow(Node parent, string label, SpinBox[] values, double minimum, double maximum)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(68, 0) });
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = CreateSpinBox(minimum, maximum, 0.1);
            values[index].Prefix = index switch { 0 => "X ", 1 => "Y ", _ => "Z " };
            values[index].ValueChanged += OnFreeValueChanged;
            row.AddChild(values[index]);
        }
    }

    private SpinBox AddPovField(GridContainer grid, string label, double minimum, double maximum, double step)
    {
        grid.AddChild(new Label { Text = label });
        var field = CreateSpinBox(minimum, maximum, step);
        field.CustomMinimumSize = new Vector2(60, 0);
        field.ValueChanged += OnPovSettingsValueChanged;
        grid.AddChild(field);
        return field;
    }

    private void BuildPovTargetRow(Node parent)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label { Text = "Target XYZ", CustomMinimumSize = new Vector2(68, 0) });
        for (var index = 0; index < _povWorldTarget.Length; index++)
        {
            _povWorldTarget[index] = CreateSpinBox(-100, 100, 0.25);
            _povWorldTarget[index].Prefix = index switch { 0 => "X ", 1 => "Y ", _ => "Z " };
            _povWorldTarget[index].ValueChanged += OnPovSettingsValueChanged;
            row.AddChild(_povWorldTarget[index]);
        }
    }

    private static SpinBox CreateSpinBox(double minimum, double maximum, double step = 1) => new()
    {
        MinValue = minimum,
        MaxValue = maximum,
        Step = step,
        CustomMinimumSize = new Vector2(72, 0),
        AllowGreater = true,
        AllowLesser = true
    };

    private void RefreshPlayers()
    {
        _playerIds.Clear();
        _playerOption.Clear();
        _povTargetPlayerOption.Clear();
        foreach (var player in _game.Gold.Roster.Concat(_game.Navy.Roster))
        {
            _playerIds.Add(player.Id);
            _playerOption.AddItem($"{player.Team?.Name} #{player.JerseyNumber} {player.Name}");
            _povTargetPlayerOption.AddItem($"{player.Team?.Name} #{player.JerseyNumber} {player.Name}");
        }
    }

    private void RefreshEditor()
    {
        if (_activeCameraId == Guid.Empty || !_project.Cameras.Any(camera => camera.Id == _activeCameraId))
            return;
        var camera = _project.Camera(_activeCameraId);
        _nameEdit.Text = camera.Name;
        _typeOption.Select((int)camera.Type);
        SetVector(_position, camera.Position);
        SetVector(_rotation, camera.RotationDegrees);
        _fov.Value = camera.FieldOfView;
        var settings = camera.PovSettings;
        _povModeOption.Select((int)settings.Mode);
        _mouseSensitivity.Value = settings.MouseSensitivity;
        _controllerSensitivity.Value = settings.ControllerSensitivity;
        _stabilization.Value = settings.StabilizationStrength;
        _headBob.Value = settings.HeadBobStrength;
        _pitchOffset.Value = settings.PitchOffsetDegrees;
        _forwardOffset.Value = settings.ForwardOffset;
        _nearClip.Value = settings.NearClip;
        _povTargetKindOption.Select((int)settings.TargetKind);
        SetVector(_povWorldTarget, settings.WorldTarget);
        var playerIndex = camera.PlayerId.HasValue ? _playerIds.IndexOf(camera.PlayerId.Value) : -1;
        if (playerIndex >= 0)
            _playerOption.Select(playerIndex);
        else
            _playerOption.Select(-1);
        var targetPlayerIndex = settings.TargetPlayerId.HasValue ? _playerIds.IndexOf(settings.TargetPlayerId.Value) : -1;
        _povTargetPlayerOption.Select(targetPlayerIndex);
        UpdateFieldAvailability(true);
    }

    private void RefreshCuts()
    {
        _cutIds.Clear();
        _cutList.Clear();
        foreach (var cut in _project.CameraCutsFor(_play.Id))
        {
            _cutIds.Add(cut.Id);
            _cutList.AddItem($"{cut.TimeSeconds:0.0}s  {_project.Camera(cut.CameraId).Name}");
        }
    }

    private void UpdateFieldAvailability(bool panelEnabled)
    {
        if (_activeCameraId == Guid.Empty || !_project.Cameras.Any(camera => camera.Id == _activeCameraId))
            return;
        var type = _project.Camera(_activeCameraId).Type;
        var free = panelEnabled && type == CameraType.FreeCamera;
        foreach (var spinBox in _position.Concat(_rotation))
            spinBox.Editable = free;
        _fov.Editable = free;
        var pov = panelEnabled && type == CameraType.PlayerPov;
        _playerOption.Disabled = !pov;
        _povModeOption.Disabled = !pov;
        foreach (var field in new[]
                 {
                     _mouseSensitivity, _controllerSensitivity, _stabilization, _headBob,
                     _pitchOffset, _forwardOffset, _nearClip
                 })
            field.Editable = pov;
        var lookAt = pov && SelectedPovMode() == PlayerPovMode.LookAtTarget;
        _povTargetKindOption.Disabled = !lookAt;
        _povTargetPlayerOption.Disabled = !lookAt || SelectedPovTargetKind() != PlayerPovTargetKind.Player;
        foreach (var field in _povWorldTarget)
            field.Editable = lookAt && SelectedPovTargetKind() == PlayerPovTargetKind.WorldPoint;
        _recenterButton.Disabled = !pov || SelectedPovMode() != PlayerPovMode.FreeLook;
    }

    private void OnCameraSelected(long index)
    {
        if (_refreshing || index < 0 || index >= _cameraIds.Count)
            return;
        _activeCameraId = _cameraIds[(int)index];
        _refreshing = true;
        RefreshEditor();
        _refreshing = false;
        CameraSelected?.Invoke(_activeCameraId);
    }

    private void OnTypeSelected(long index)
    {
        if (_refreshing || _activeCameraId == Guid.Empty)
            return;
        TypeChanged?.Invoke(_activeCameraId, (CameraType)_typeOption.GetItemId((int)index));
    }

    private void OnPlayerSelected(long index)
    {
        if (_refreshing || _activeCameraId == Guid.Empty)
            return;
        var playerId = index >= 0 && index < _playerIds.Count ? _playerIds[(int)index] : (Guid?)null;
        PlayerPovChanged?.Invoke(_activeCameraId, playerId);
    }

    private void OnPovSettingsValueChanged(long _) => RequestPovSettingsChange();

    private void OnPovSettingsValueChanged(double _) => RequestPovSettingsChange();

    private void RequestPovSettingsChange()
    {
        if (_refreshing || _activeCameraId == Guid.Empty)
            return;
        var targetIndex = _povTargetPlayerOption.Selected;
        var targetPlayer = targetIndex >= 0 && targetIndex < _playerIds.Count
            ? _playerIds[targetIndex]
            : (Guid?)null;
        PlayerPovSettingsChanged?.Invoke(
            _activeCameraId,
            new PlayerPovSettings(
                SelectedPovMode(),
                (float)_mouseSensitivity.Value,
                (float)_controllerSensitivity.Value,
                (float)_stabilization.Value,
                (float)_headBob.Value,
                (float)_pitchOffset.Value,
                (float)_forwardOffset.Value,
                (float)_nearClip.Value,
                SelectedPovTargetKind(),
                targetPlayer,
                ReadVector(_povWorldTarget)));
        UpdateFieldAvailability(true);
    }

    private void OnFreeValueChanged(double _)
    {
        if (_refreshing || _activeCameraId == Guid.Empty)
            return;
        FreeCameraChanged?.Invoke(
            _activeCameraId,
            ReadVector(_position),
            ReadVector(_rotation),
            (float)_fov.Value);
    }

    private void RequestRename() => RenameRequested?.Invoke(_activeCameraId, _nameEdit.Text);
    private void RequestDuplicate() => DuplicateRequested?.Invoke(_activeCameraId);
    private void RequestDelete() => DeleteRequested?.Invoke(_activeCameraId);
    private void RequestPreview() => PreviewRequested?.Invoke(_activeCameraId);
    private void RequestPovRecenter() => PlayerPovRecenterRequested?.Invoke(_activeCameraId);
    private void RequestAddCut() => AddCutRequested?.Invoke(_activeCameraId, _cutTime.Value);

    private void RequestDeleteCut()
    {
        var selected = _cutList.GetSelectedItems();
        if (selected.Length > 0 && selected[0] >= 0 && selected[0] < _cutIds.Count)
            DeleteCutRequested?.Invoke(_cutIds[selected[0]]);
    }

    private Button AddButton(Node parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        _buttons.Add(button);
        return button;
    }

    private static CameraVector ReadVector(IReadOnlyList<SpinBox> values) =>
        new((float)values[0].Value, (float)values[1].Value, (float)values[2].Value);

    private static void SetVector(IReadOnlyList<SpinBox> values, CameraVector vector)
    {
        values[0].Value = vector.X;
        values[1].Value = vector.Y;
        values[2].Value = vector.Z;
    }

    private static string DisplayName(CameraType type) => type switch
    {
        CameraType.BroadcastWide => "Broadcast Wide",
        CameraType.SidelineLow => "Sideline Low",
        CameraType.EndZone => "End Zone",
        CameraType.PlayerPov => "Player POV",
        CameraType.FreeCamera => "Free Camera",
        _ => type.ToString()
    };

    private PlayerPovMode SelectedPovMode() =>
        (PlayerPovMode)_povModeOption.GetItemId(_povModeOption.Selected);

    private PlayerPovTargetKind SelectedPovTargetKind() =>
        (PlayerPovTargetKind)_povTargetKindOption.GetItemId(_povTargetKindOption.Selected);

    private static string PovModeName(PlayerPovMode mode) => mode switch
    {
        PlayerPovMode.LockedForward => "Locked Forward",
        PlayerPovMode.FreeLook => "Free Look",
        PlayerPovMode.LookAtTarget => "Look At Target",
        _ => mode.ToString()
    };

    private static string PovTargetName(PlayerPovTargetKind kind) => kind switch
    {
        PlayerPovTargetKind.Player => "Player",
        PlayerPovTargetKind.Football => "Football",
        PlayerPovTargetKind.WorldPoint => "World Point",
        _ => kind.ToString()
    };
}
