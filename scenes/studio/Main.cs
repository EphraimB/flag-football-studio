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
    private static readonly Color GoldColor = new("d8a91b");
    private static readonly Color NavyColor = new("173b73");

    private readonly Dictionary<Guid, Node3D> _pawns = [];
    private readonly PackedScene _fieldScene = GD.Load<PackedScene>("res://scenes/games/field.tscn");
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/characters/player_pawn.tscn");
    private readonly JsonProjectFileStore _fileStore = new(new ProjectJsonSerializer());
    private readonly string _projectPath = ProjectSettings.GlobalizePath("user://flag-football-studio/game-project.json");
    private Game _game = null!;
    private GameProject _project = null!;
    private PlayDefinition _play = null!;
    private FootballView _football = null!;
    private PlayDirectorPanel _director = null!;
    private GameDirectorPanel _gameDirector = null!;
    private Label _statusLabel = null!;
    private PlaySequenceController _sequence = null!;

    public override void _Ready()
    {
        _game = Game.CreatePrototype();
        _project = GameProject.CreatePrototype(_game);
        _play = _project.Plays[0];
        AddChild(_fieldScene.Instantiate());
        BuildLightingAndCamera();
        SpawnTeam(_game.Gold, GoldColor, 0);
        SpawnTeam(_game.Navy, NavyColor, Mathf.Pi);

        _football = new FootballView { Name = "Football" };
        AddChild(_football);
        ApplyFormation();
        BuildUi();

        _sequence = new PlaySequenceController { Name = "PlaySequenceController" };
        AddChild(_sequence);
        _sequence.Configure(_play, _pawns, _football, this, _statusLabel);

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
    }

    private void SpawnTeam(Team team, Color color, float rotationY)
    {
        for (var index = 0; index < team.Roster.Count; index++)
        {
            var pawn = _playerScene.Instantiate<PlayerPawn>();
            pawn.Name = $"{team.Name}_{team.Roster[index].Name}";
            pawn.Configure(team.Roster[index], color);
            pawn.Rotation = new Vector3(0, rotationY, 0);
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

        var camera = new Camera3D
        {
            Position = new Vector3(15, 19, 24),
            Fov = 52,
            Current = true
        };
        AddChild(camera);
        camera.LookAt(new Vector3(0, 0, 2), Vector3.Up);
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

        _director = new PlayDirectorPanel
        {
            Name = "PlayDirector",
            AnchorLeft = 0.61f,
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
        _director.Configure(_game, _play);
    }

    private void ApplyFormation()
    {
        foreach (var startingPosition in _play.StartingPositions)
            _pawns[startingPosition.Key].Position = ToWorld(startingPosition.Value);

        var center = _game.Gold.Roster.First(player => player.Position == PlayerPosition.Center);
        var centerPosition = _play.StartingPositions[center.Id];
        if (_football.GetParent() != this)
            _football.Reparent(this, false);
        _football.Position = new Vector3(centerPosition.X, 0.9f, centerPosition.Y + 0.35f);
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
        _sequence.SetPlay(_play, _football.Position);
        _director.SetEditingEnabled(false);
        _gameDirector.SetInteractionEnabled(false);
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
        ApplyFormation();
        _sequence.SetPlay(_play, _football.Position);
        _gameDirector.SetStatus(status);
    }

    private void LoadProject(GameProject project)
    {
        if (_football.GetParent() != this)
            _football.Reparent(this, false);
        foreach (var pawn in _pawns.Values)
        {
            RemoveChild(pawn);
            pawn.QueueFree();
        }
        _pawns.Clear();

        _project = project;
        _game = new Game(project.HomeTeam, project.AwayTeam);
        SpawnTeam(_game.Gold, GoldColor, 0);
        SpawnTeam(_game.Navy, NavyColor, Mathf.Pi);
        _play = project.Plays[0];
        _director.SetGameAndPlay(_game, _play);
        _gameDirector.SetProject(_project, _play.Id);
        ApplyFormation();
        _sequence.Configure(_play, _pawns, _football, this, _statusLabel);
    }

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
}
