using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
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
    private Game _game = null!;
    private PlayDefinition _play = null!;
    private FootballView _football = null!;
    private PlayDirectorPanel _director = null!;
    private Label _statusLabel = null!;
    private PlaySequenceController _sequence = null!;

    public override void _Ready()
    {
        _game = Game.CreatePrototype();
        _play = PlayDefinition.CreatePrototype(_game);
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

        var panel = new PanelContainer
        {
            OffsetLeft = 24,
            OffsetTop = 24,
            OffsetRight = 314,
            OffsetBottom = 116
        };
        canvas.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        margin.AddChild(stack);

        var title = new Label { Text = "GOLD vs NAVY", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        stack.AddChild(title);

        _statusLabel = new Label { Text = "Ready", HorizontalAlignment = HorizontalAlignment.Center };
        stack.AddChild(_statusLabel);

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
        _play = PlayDefinition.CreatePrototype(_game);
        _director.SetPlay(_play);
        ApplyFormation();
        _sequence.SetPlay(_play, _football.Position);
        _statusLabel.Text = "Formation reset";
    }

    private async void OnRunPlayRequested()
    {
        if (_sequence.IsRunning)
            return;
        ApplyFormation();
        _sequence.SetPlay(_play, _football.Position);
        _director.SetEditingEnabled(false);
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
        }
    }

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
}
