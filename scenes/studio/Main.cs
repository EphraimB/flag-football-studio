using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using FlagFootballStudio.Presentation;
using Godot;

namespace FlagFootballStudio.Studio;

public partial class Main : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];
    private readonly PackedScene _fieldScene = GD.Load<PackedScene>("res://scenes/games/field.tscn");
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/characters/player_pawn.tscn");
    private readonly JsonProjectFileStore _fileStore = new(new ProjectJsonSerializer());
    private readonly string _projectPath = ProjectSettings.GlobalizePath("user://flag-football-studio/game-project.json");
    private Game _game = null!;
    private GameProject _project = null!;
    private PlayDefinition _play = null!;
    private Guid _selectedCameraId;
    private FootballView _football = null!;
    private Camera3D _previewCamera = null!;
    private PlayDirectorPanel _director = null!;
    private GameDirectorPanel _gameDirector = null!;
    private CameraDirectorPanel _cameraDirector = null!;
    private PlayerStudioPanel _playerStudio = null!;
    private UniformStudioPanel _uniformStudio = null!;
    private CameraDirectorController _cameraController = null!;
    private Label _statusLabel = null!;
    private PlaySequenceController _sequence = null!;

    public override void _Ready()
    {
        _game = Game.CreatePrototype();
        _project = GameProject.CreatePrototype(_game);
        _play = _project.Plays[0];
        _selectedCameraId = _project.Cameras[0].Id;
        AddChild(_fieldScene.Instantiate());
        BuildLightingAndCamera();
        SpawnTeam(_game.Gold);
        SpawnTeam(_game.Navy);

        _football = new FootballView { Name = "Football" };
        AddChild(_football);
        ApplyFormation();
        BuildUi();

        _cameraController = new CameraDirectorController { Name = "CameraDirectorController" };
        AddChild(_cameraController);
        _cameraController.Configure(_previewCamera, this, _pawns, _football);
        PreviewSelectedCamera();

        _sequence = new PlaySequenceController { Name = "PlaySequenceController" };
        AddChild(_sequence);
        _sequence.Configure(_play, _pawns, _football, this, _statusLabel, OffenseTeam, DefenseTeam);

        _director.FormationChanged += OnFormationChanged;
        _director.ResetRequested += OnResetRequested;
        _director.RunRequested += OnRunPlayRequested;
        _gameDirector.PlaySelected += OnPlaySelected;
        _gameDirector.CreateRequested += OnCreatePlayRequested;
        _gameDirector.RenameRequested += OnRenamePlayRequested;
        _gameDirector.DuplicateRequested += OnDuplicatePlayRequested;
        _gameDirector.DeleteRequested += OnDeletePlayRequested;
        _gameDirector.SaveRequested += OnSaveRequested;
        _gameDirector.LoadRequested += OnLoadRequested;
        _cameraDirector.CameraSelected += OnCameraSelected;
        _cameraDirector.CreateRequested += OnCreateCameraRequested;
        _cameraDirector.RenameRequested += OnRenameCameraRequested;
        _cameraDirector.DuplicateRequested += OnDuplicateCameraRequested;
        _cameraDirector.DeleteRequested += OnDeleteCameraRequested;
        _cameraDirector.TypeChanged += OnCameraTypeChanged;
        _cameraDirector.FreeCameraChanged += OnFreeCameraChanged;
        _cameraDirector.PlayerPovChanged += OnPlayerPovChanged;
        _cameraDirector.PlayerPovSettingsChanged += OnPlayerPovSettingsChanged;
        _cameraDirector.PlayerPovRecenterRequested += OnPlayerPovRecenterRequested;
        _cameraDirector.PreviewRequested += OnCameraPreviewRequested;
        _cameraDirector.AddCutRequested += OnAddCameraCutRequested;
        _cameraDirector.DeleteCutRequested += OnDeleteCameraCutRequested;
        _playerStudio.AppearanceChanged += OnPlayerAppearanceChanged;
        _playerStudio.StatusChanged += message => _gameDirector.SetStatus(message);
        _playerStudio.ExpressionPreviewRequested += OnExpressionPreviewRequested;
        _playerStudio.BlinkRequested += OnBlinkRequested;
        _playerStudio.AutomaticBlinkChanged += OnAutomaticBlinkChanged;
        _playerStudio.GazePreviewRequested += OnGazePreviewRequested;
        _playerStudio.MouthPreviewRequested += OnMouthPreviewRequested;
        _playerStudio.SpeechShapeCycleRequested += OnSpeechShapeCycleRequested;
        _uniformStudio.UniformChanged += OnUniformChanged;
        _uniformStudio.StatusChanged += message => _gameDirector.SetStatus(message);

        if (OS.GetCmdlineUserArgs().Contains("--validate-pov-free-look"))
            CallDeferred(nameof(RunPovFreeLookValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-facing-pov"))
            CallDeferred(nameof(RunFacingPovValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-mouth"))
            CallDeferred(nameof(RunMouthValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-hair"))
            CallDeferred(nameof(RunHairValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-expressions"))
            CallDeferred(nameof(RunExpressionEyeValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-faces"))
            CallDeferred(nameof(RunFaceValidation));
        else if (OS.GetCmdlineUserArgs().Contains("--validate-humanoids"))
            CallDeferred(nameof(RunHumanoidValidation));
    }

    private async void RunPovFreeLookValidation()
    {
        var validator = new PovFreeLookValidator { Name = "PovFreeLookValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Player POV free-look validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunFacingPovValidation()
    {
        var validator = new FacingPovValidator { Name = "FacingPovValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Formation facing and Player POV validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunMouthValidation()
    {
        var validator = new MouthFoundationValidator { Name = "MouthFoundationValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Mouth foundation validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunHairValidation()
    {
        var validator = new HairFoundationValidator { Name = "HairFoundationValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Hair foundation validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunExpressionEyeValidation()
    {
        var validator = new ExpressionEyeValidator { Name = "ExpressionEyeValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Expression and eye validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunFaceValidation()
    {
        var validator = new FaceFoundationValidator { Name = "FaceFoundationValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Facial foundation validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void RunHumanoidValidation()
    {
        var validator = new HumanoidFoundationValidator { Name = "HumanoidFoundationValidator" };
        AddChild(validator);
        try
        {
            await validator.RunAsync();
            GD.Print("Humanoid foundation validation passed.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private void SpawnTeam(Team team)
    {
        for (var index = 0; index < team.Roster.Count; index++)
        {
            var pawn = _playerScene.Instantiate<PlayerPawn>();
            pawn.Name = $"{team.Name}_{team.Roster[index].Name}";
            pawn.Configure(
                team.Roster[index],
                _project.AppearanceFor(team.Roster[index].Id),
                _project.ActiveUniformFor(team.Id));
            AddChild(pawn);
            _pawns.Add(team.Roster[index].Id, pawn);
        }
    }

    private void BuildLightingAndCamera()
    {
        var environment = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("8dc7e8"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("dbeeff"),
                AmbientLightEnergy = 0.65f
            }
        };
        AddChild(environment);

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -30, 0),
            LightEnergy = 1.25f,
            ShadowEnabled = true
        };
        AddChild(sun);

        _previewCamera = new Camera3D
        {
            Position = new Vector3(15, 19, 24),
            Fov = 52,
            Current = true
        };
        AddChild(_previewCamera);
        _previewCamera.LookAt(new Vector3(0, 0, 2), Vector3.Up);
    }

    private void BuildUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _gameDirector = new GameDirectorPanel
        {
            Name = "GameDirector",
            OffsetLeft = 16,
            OffsetTop = 16,
            OffsetRight = 390,
            OffsetBottom = 410
        };
        canvas.AddChild(_gameDirector);
        _gameDirector.Configure(_project, _play.Id);
        _statusLabel = _gameDirector.StatusLabel;

        _cameraDirector = new CameraDirectorPanel
        {
            Name = "CameraDirector",
            OffsetLeft = 16,
            OffsetTop = 420,
            OffsetRight = 390,
            OffsetBottom = 984
        };
        canvas.AddChild(_cameraDirector);
        _cameraDirector.Configure(_project, _game, _play, _selectedCameraId);

        _playerStudio = new PlayerStudioPanel
        {
            Name = "PlayerStudio",
            OffsetLeft = 400,
            OffsetTop = 530,
            OffsetRight = 790,
            OffsetBottom = 984
        };
        canvas.AddChild(_playerStudio);
        _playerStudio.Configure(_project, _game);

        _uniformStudio = new UniformStudioPanel
        {
            Name = "UniformStudio",
            OffsetLeft = 800,
            OffsetTop = 430,
            OffsetRight = 1212,
            OffsetBottom = 984
        };
        canvas.AddChild(_uniformStudio);
        _uniformStudio.Configure(_project);

        _director = new PlayDirectorPanel
        {
            Name = "PlayDirector",
            AnchorLeft = 0.68f,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 12,
            OffsetRight = -12,
            OffsetBottom = -12,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        canvas.AddChild(_director);
        _director.Configure(_game, _play, _project.Possession.Id);
    }

    private void ApplyFormation()
    {
        foreach (var startingPosition in _play.StartingPositions)
        {
            _pawns[startingPosition.Key].Position = ToWorld(startingPosition.Value);
            if (_pawns[startingPosition.Key] is PlayerPawn pawn)
                pawn.ResetPresentationPose();
        }

        var direction = FormationFacing.Apply(_play, OffenseTeam, DefenseTeam, _pawns);
        if (_director is not null)
            _director.SetPossession(_project.Possession.Id);

        var snapper = FindSnapper(direction);
        var snapperPosition = _play.StartingPositions[snapper.Id];
        if (_football.GetParent() != this)
            _football.Reparent(this, false);
        _football.Position = new Vector3(
            snapperPosition.X,
            0.9f,
            snapperPosition.Y + PlayDirectionResolver.Sign(direction) * 0.35f);
    }

    private void OnFormationChanged()
    {
        if (_sequence.IsRunning)
            return;
        ApplyFormation();
        _statusLabel.Text = "Formation updated";
    }

    private void OnResetRequested()
    {
        if (_sequence.IsRunning)
            return;

        var resetPlay = PlayDefinition.CreatePrototype(_game, _play.Name, _play.Id);
        _project.ReplacePlay(_play.Id, resetPlay);
        SwitchToPlay(resetPlay.Id, "Formation reset");
    }

    private async void OnRunPlayRequested()
    {
        if (_sequence.IsRunning)
            return;
        ApplyFormation();
        _sequence.SetPlay(_play, _football.Position, OffenseTeam, DefenseTeam);
        _director.SetEditingEnabled(false);
        _gameDirector.SetInteractionEnabled(false);
        _cameraDirector.SetInteractionEnabled(false);
        _playerStudio.SetInteractionEnabled(false);
        _uniformStudio.SetInteractionEnabled(false);
        _cameraController.StopCuts();
        _ = _cameraController.PlayCutsAsync(_project, _play);
        try
        {
            await _sequence.RunAsync();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _statusLabel.Text = exception.Message;
        }
        finally
        {
            _director.SetEditingEnabled(true);
            _gameDirector.SetInteractionEnabled(true);
            _cameraDirector.SetInteractionEnabled(true);
            _playerStudio.SetInteractionEnabled(true);
            _uniformStudio.SetInteractionEnabled(true);
            _cameraController.StopCuts();
            PreviewSelectedCamera();
        }
    }

    private void OnPlaySelected(Guid playId)
    {
        if (!_sequence.IsRunning)
            SwitchToPlay(playId, $"Loaded {_project.Play(playId).Name}");
    }

    private void OnCreatePlayRequested()
    {
        if (_sequence.IsRunning)
            return;
        var play = PlayDefinition.CreatePrototype(_game, $"Play {_project.Plays.Count + 1}");
        _project.AddPlay(play);
        SwitchToPlay(play.Id, "New play created");
    }

    private void OnRenamePlayRequested(Guid playId, string name)
    {
        try
        {
            _project.Play(playId).Rename(name);
            _gameDirector.RefreshPlayList();
            _gameDirector.SetStatus("Play renamed");
        }
        catch (Exception exception)
        {
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void OnDuplicatePlayRequested(Guid playId)
    {
        var source = _project.Play(playId);
        var duplicate = source.Duplicate($"{source.Name} Copy");
        _project.AddPlay(duplicate);
        SwitchToPlay(duplicate.Id, "Play duplicated");
    }

    private void OnDeletePlayRequested(Guid playId)
    {
        if (_project.Plays.Count == 1)
        {
            _gameDirector.SetStatus("A project must keep at least one play");
            return;
        }

        var index = _project.Plays.ToList().FindIndex(play => play.Id == playId);
        _project.RemovePlay(playId);
        var nextIndex = Math.Clamp(index, 0, _project.Plays.Count - 1);
        SwitchToPlay(_project.Plays[nextIndex].Id, "Play deleted");
    }

    private void OnSaveRequested()
    {
        try
        {
            _fileStore.SaveProject(_projectPath, _project);
            _gameDirector.SetStatus("Project saved");
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _gameDirector.SetStatus($"Save failed: {exception.Message}");
        }
    }

    private void OnLoadRequested()
    {
        try
        {
            var loadedProject = _fileStore.LoadProject(_projectPath);
            if (loadedProject.Plays.Count == 0)
                throw new InvalidOperationException("The project file contains no plays.");
            LoadProject(loadedProject);
            _gameDirector.SetStatus("Project loaded");
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _gameDirector.SetStatus($"Load failed: {exception.Message}");
        }
    }

    private void SwitchToPlay(Guid playId, string status)
    {
        _play = _project.Play(playId);
        _director.SetPlay(_play);
        _gameDirector.SetActivePlay(_play.Id);
        _cameraDirector.SetPlay(_play);
        ApplyFormation();
        _sequence.SetPlay(_play, _football.Position, OffenseTeam, DefenseTeam);
        PreviewSelectedCamera();
        _gameDirector.SetStatus(status);
    }

    private void LoadProject(GameProject project)
    {
        if (_football.GetParent() != this)
            _football.Reparent(this, false);
        if (_previewCamera.GetParent() != this)
            _previewCamera.Reparent(this, true);
        foreach (var pawn in _pawns.Values)
        {
            RemoveChild(pawn);
            pawn.QueueFree();
        }
        _pawns.Clear();

        _project = project;
        _game = new Game(project.HomeTeam, project.AwayTeam);
        EnsureCameraLibrary();
        _selectedCameraId = _project.Cameras[0].Id;
        SpawnTeam(_game.Gold);
        SpawnTeam(_game.Navy);
        _play = project.Plays[0];
        _director.SetGameAndPlay(_game, _play, _project.Possession.Id);
        _gameDirector.SetProject(_project, _play.Id);
        _cameraDirector.SetProject(_project, _game, _play, _selectedCameraId);
        _playerStudio.SetProject(_project, _game);
        _uniformStudio.SetProject(_project);
        ApplyFormation();
        _sequence.Configure(_play, _pawns, _football, this, _statusLabel, OffenseTeam, DefenseTeam);
        _cameraController.Configure(_previewCamera, this, _pawns, _football);
        PreviewSelectedCamera();
    }

    private void OnCameraSelected(Guid cameraId)
    {
        _selectedCameraId = cameraId;
        PreviewSelectedCamera();
    }

    private void OnCreateCameraRequested()
    {
        var camera = new CameraDefinition(Guid.NewGuid(), $"Camera {_project.Cameras.Count + 1}", CameraType.BroadcastWide);
        _project.AddCamera(camera);
        SelectCamera(camera.Id, "Camera created");
    }

    private void OnRenameCameraRequested(Guid cameraId, string name)
    {
        try
        {
            _project.Camera(cameraId).Rename(name);
            _cameraDirector.RefreshAll();
            _gameDirector.SetStatus("Camera renamed");
        }
        catch (Exception exception)
        {
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void OnDuplicateCameraRequested(Guid cameraId)
    {
        var source = _project.Camera(cameraId);
        var duplicate = source.Duplicate($"{source.Name} Copy");
        _project.AddCamera(duplicate);
        SelectCamera(duplicate.Id, "Camera duplicated");
    }

    private void OnDeleteCameraRequested(Guid cameraId)
    {
        if (_project.Cameras.Count == 1)
        {
            _gameDirector.SetStatus("A project must keep at least one camera");
            return;
        }

        var index = _project.Cameras.ToList().FindIndex(camera => camera.Id == cameraId);
        _project.RemoveCamera(cameraId);
        var nextIndex = Math.Clamp(index, 0, _project.Cameras.Count - 1);
        SelectCamera(_project.Cameras[nextIndex].Id, "Camera deleted");
    }

    private void OnCameraTypeChanged(Guid cameraId, CameraType type)
    {
        var camera = _project.Camera(cameraId);
        camera.SetType(type);
        if (type == CameraType.PlayerPov && !camera.PlayerId.HasValue)
            camera.SetPlayer(_play.QuarterbackId);
        _cameraDirector.RefreshAll();
        PreviewSelectedCamera();
    }

    private void OnFreeCameraChanged(Guid cameraId, CameraVector position, CameraVector rotation, float fov)
    {
        try
        {
            _project.Camera(cameraId).SetFreeCamera(position, rotation, fov);
            PreviewSelectedCamera();
        }
        catch (Exception exception)
        {
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void OnPlayerPovChanged(Guid cameraId, Guid? playerId)
    {
        _project.Camera(cameraId).SetPlayer(playerId);
        PreviewSelectedCamera();
    }

    private void OnPlayerPovSettingsChanged(Guid cameraId, PlayerPovSettings settings)
    {
        try
        {
            var camera = _project.Camera(cameraId);
            var enteringFreeLook = camera.PovSettings.Mode != PlayerPovMode.FreeLook &&
                                   settings.Mode == PlayerPovMode.FreeLook;
            camera.SetPlayerPovSettings(settings);
            if (cameraId == _selectedCameraId)
                _cameraController.Preview(camera, _play, enteringFreeLook);
        }
        catch (Exception exception)
        {
            _cameraDirector.RefreshAll();
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void OnPlayerPovRecenterRequested(Guid cameraId)
    {
        if (cameraId != _selectedCameraId)
            return;
        _cameraController.RecenterPlayerPov();
        _gameDirector.SetStatus("Player POV recentering");
    }

    private void OnCameraPreviewRequested(Guid cameraId)
    {
        _selectedCameraId = cameraId;
        PreviewSelectedCamera();
    }

    private void OnAddCameraCutRequested(Guid cameraId, double timeSeconds)
    {
        try
        {
            _project.AddCameraCut(new CameraCut(Guid.NewGuid(), _play.Id, cameraId, timeSeconds));
            _cameraDirector.RefreshAll();
            _gameDirector.SetStatus($"Camera cut added at {timeSeconds:0.0}s");
        }
        catch (Exception exception)
        {
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void OnDeleteCameraCutRequested(Guid cutId)
    {
        if (_project.RemoveCameraCut(cutId))
        {
            _cameraDirector.RefreshAll();
            _gameDirector.SetStatus("Camera cut deleted");
        }
    }

    private void SelectCamera(Guid cameraId, string status)
    {
        _selectedCameraId = cameraId;
        _cameraDirector.SetActiveCamera(cameraId);
        PreviewSelectedCamera();
        _gameDirector.SetStatus(status);
    }

    private void PreviewSelectedCamera()
    {
        try
        {
            _cameraController.Preview(_project.Camera(_selectedCameraId), _play);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            _gameDirector.SetStatus(exception.Message);
        }
    }

    private void EnsureCameraLibrary()
    {
        if (_project.Cameras.Count > 0)
            return;
        _project.AddCamera(new CameraDefinition(Guid.NewGuid(), "Broadcast Wide", CameraType.BroadcastWide));
    }

    private void OnPlayerAppearanceChanged(Guid playerId)
    {
        var player = _game.Gold.Roster.Concat(_game.Navy.Roster).First(candidate => candidate.Id == playerId);
        RefreshTeamPresentation(player.Team!.Id);
        _director.RefreshAppearance();
        _cameraDirector.RefreshPlayerLabels();
        _gameDirector.SetStatus("Player appearance updated");
    }

    private void OnExpressionPreviewRequested(Guid playerId, FacialExpressionState expression)
    {
        PlayerPawnFor(playerId).SetFacialExpression(expression);
        _gameDirector.SetStatus($"Expression: {expression}");
    }

    private void OnBlinkRequested(Guid playerId)
    {
        PlayerPawnFor(playerId).TriggerBlink();
        _gameDirector.SetStatus("Blink triggered");
    }

    private void OnAutomaticBlinkChanged(Guid playerId, bool enabled)
    {
        PlayerPawnFor(playerId).SetAutomaticBlink(enabled);
        _gameDirector.SetStatus(enabled ? "Automatic blink enabled" : "Automatic blink disabled");
    }

    private void OnGazePreviewRequested(
        Guid playerId,
        GazePreviewTargetKind targetKind,
        Guid? targetPlayerId,
        float horizontal,
        float vertical)
    {
        var pawn = PlayerPawnFor(playerId);
        switch (targetKind)
        {
            case GazePreviewTargetKind.Football:
                pawn.LookAtFootball(_football);
                break;
            case GazePreviewTargetKind.Player when targetPlayerId.HasValue:
                pawn.LookAtPlayer(PlayerPawnFor(targetPlayerId.Value));
                break;
            default:
                pawn.SetManualGaze(horizontal, vertical);
                break;
        }
        _gameDirector.SetStatus("Gaze preview updated");
    }

    private void OnMouthPreviewRequested(
        Guid playerId,
        SpeechMouthShape shape,
        float jawOpen,
        float lipWidth,
        float lipFullness,
        float upperLip,
        float lowerLip)
    {
        var pawn = PlayerPawnFor(playerId);
        pawn.SetMouthShape(shape);
        pawn.SetMouthControls(jawOpen, lipWidth, lipFullness, upperLip, lowerLip);
        _gameDirector.SetStatus($"Mouth shape: {shape}");
    }

    private void OnSpeechShapeCycleRequested(Guid playerId)
    {
        PlayerPawnFor(playerId).StartSpeechShapeCycle();
        _gameDirector.SetStatus("Cycling speech shapes");
    }

    private void OnUniformChanged(Guid teamId)
    {
        RefreshTeamPresentation(teamId);
        _playerStudio.RefreshUniformFields();
        _gameDirector.SetStatus("Uniform updated");
    }

    private void RefreshTeamPresentation(Guid teamId)
    {
        var team = teamId == _game.Gold.Id ? _game.Gold : _game.Navy;
        var uniform = _project.ActiveUniformFor(teamId);
        foreach (var player in team.Roster)
        {
            if (_pawns.TryGetValue(player.Id, out var node) && node is PlayerPawn pawn)
                pawn.ApplyPresentation(_project.AppearanceFor(player.Id), uniform);
        }
    }

    private PlayerPawn PlayerPawnFor(Guid playerId) =>
        _pawns.TryGetValue(playerId, out var node) && node is PlayerPawn pawn
            ? pawn
            : throw new KeyNotFoundException("The requested player pawn is not in the preview.");

    private Team OffenseTeam => _project.Possession;

    private Team DefenseTeam => OffenseTeam.Id == _game.Gold.Id ? _game.Navy : _game.Gold;

    private Player FindSnapper(FieldDirection direction)
    {
        var center = OffenseTeam.Roster.FirstOrDefault(player =>
            player.Position == PlayerPosition.Center && _play.StartingPositions.ContainsKey(player.Id));
        if (center is not null)
            return center;

        return OffenseTeam.Roster
            .Where(player => _play.StartingPositions.ContainsKey(player.Id))
            .OrderByDescending(player => _play.StartingPositions[player.Id].Y * PlayDirectionResolver.Sign(direction))
            .First();
    }

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
}
