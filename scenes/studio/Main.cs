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

    private readonly Dictionary<Player, PlayerPawn> _pawns = [];
    private readonly PackedScene _fieldScene = GD.Load<PackedScene>("res://scenes/games/field.tscn");
    private readonly PackedScene _playerScene = GD.Load<PackedScene>("res://scenes/characters/player_pawn.tscn");
    private Button _runPlayButton = null!;
    private Label _statusLabel = null!;
    private PlaySequenceController _sequence = null!;
    private PlayerPawn _quarterback = null!;

    public override void _Ready()
    {
        var game = Game.CreatePrototype();
        AddChild(_fieldScene.Instantiate());
        BuildLightingAndCamera();
        BuildUi();

        SpawnTeam(game.Gold, GoldColor, new[]
        {
            new Vector3(0, 0.08f, -2),
            new Vector3(0, 0.08f, -5),
            new Vector3(-6, 0.08f, -2),
            new Vector3(6, 0.08f, -2),
            new Vector3(3, 0.08f, -5)
        }, 0);

        SpawnTeam(game.Navy, NavyColor, new[]
        {
            new Vector3(-6, 0.08f, 1),
            new Vector3(-3, 0.08f, 1),
            new Vector3(0, 0.08f, 1),
            new Vector3(3, 0.08f, 1),
            new Vector3(6, 0.08f, 1)
        }, Mathf.Pi);

        var football = new FootballView { Name = "Football", Position = new Vector3(0, 0.9f, -1.65f) };
        AddChild(football);

        _quarterback = PawnFor(game.Gold, PlayerPosition.Quarterback);
        var receiver = PawnFor(game.Gold, PlayerPosition.Receiver);
        var defender = _pawns[game.Navy.Roster[0]];

        _sequence = new PlaySequenceController { Name = "PlaySequenceController" };
        AddChild(_sequence);
        _sequence.Configure(receiver, defender, football, this, _statusLabel);
        _runPlayButton.Pressed += OnRunPlayPressed;
    }

    private PlayerPawn PawnFor(Team team, PlayerPosition position) =>
        _pawns[team.Roster.Single(player => player.Position == position)];

    private void SpawnTeam(Team team, Color color, IReadOnlyList<Vector3> positions, float rotationY)
    {
        for (var index = 0; index < team.Roster.Count; index++)
        {
            var pawn = _playerScene.Instantiate<PlayerPawn>();
            pawn.Name = $"{team.Name}_{team.Roster[index].Name}";
            pawn.Configure(team.Roster[index], color);
            pawn.Position = positions[index];
            pawn.Rotation = new Vector3(0, rotationY, 0);
            AddChild(pawn);
            _pawns.Add(team.Roster[index], pawn);
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
            Position = new Vector3(18, 20, 23),
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
            OffsetBottom = 166
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

        _runPlayButton = new Button { Text = "Run Play" };
        stack.AddChild(_runPlayButton);

        _statusLabel = new Label { Text = "Ready", HorizontalAlignment = HorizontalAlignment.Center };
        stack.AddChild(_statusLabel);
    }

    private async void OnRunPlayPressed()
    {
        if (_sequence.IsRunning)
            return;

        _runPlayButton.Disabled = true;
        try
        {
            await _sequence.RunAsync(_quarterback);
        }
        finally
        {
            _runPlayButton.Disabled = false;
        }
    }
}
