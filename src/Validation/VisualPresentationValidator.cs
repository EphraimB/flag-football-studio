using System;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class VisualPresentationValidator : Node3D
{
    public void Run()
    {
        ValidateMaterialReuseAndColors();

        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var simulator = new FootballPlaySimulator();
        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var simulationSnapshot = SimulationSnapshot(simulation);
        var qualityLayer = new FootballAnimationQualityLayer();
        var animationSnapshot = AnimationSnapshot(qualityLayer.Build(simulation));

        var camera = new Camera3D { Fov = 47 };
        AddChild(camera);
        var initialFov = camera.Fov;
        var lighting = new SportsLightingController { Name = "VisualValidationLighting" };
        AddChild(lighting);
        foreach (var preset in Enum.GetValues<SportsLightingPreset>())
        foreach (var quality in Enum.GetValues<PresentationQualityPreset>())
        {
            lighting.Apply(new VisualPresentationSettings(
                preset, quality, ShadowQualityPreset.Medium, 1));
            Require(lighting.EffectiveSceneLuminance is >= 0.2f and <= 1.45f,
                $"{preset}/{quality} produced an unsafe POV luminance estimate.");
            Require(Math.Abs(camera.Fov - initialFov) < 0.001f,
                "Visual quality changed camera FOV/zoom.");
            Require(SimulationSnapshot(simulation) == simulationSnapshot,
                "Lighting or quality mutated the authoritative simulation.");
            Require(AnimationSnapshot(qualityLayer.Build(simulation)) == animationSnapshot,
                "Lighting or quality changed animation cues or timing.");
        }

        Require(lighting.EnabledFieldLightCount == 4,
            "Night / Field Lights did not enable all reusable field lights.");
        lighting.Apply(VisualPresentationSettings.Default);
        Require(lighting.EnabledFieldLightCount == 0,
            "Day lighting left night field lights enabled.");

        lighting.QueueFree();
        camera.QueueFree();
    }

    private static void ValidateMaterialReuseAndColors()
    {
        StudioMaterialLibrary.SetQuality(PresentationQualityPreset.High);
        var before = StudioMaterialLibrary.CachedMaterialCount;
        var toneA = new Color("6f4028");
        var toneB = new Color("b87552");
        var first = StudioMaterialLibrary.Skin(toneA);
        for (var index = 0; index < 100; index++)
            Require(ReferenceEquals(first, StudioMaterialLibrary.Skin(toneA)),
                "Identical skin requests did not reuse one material resource.");
        Require(StudioMaterialLibrary.CachedMaterialCount <= before + 1,
            "Repeated material requests created excessive duplicate resources.");

        var second = StudioMaterialLibrary.Skin(toneB);
        Require(!ReferenceEquals(first, second) && !first.AlbedoColor.IsEqualApprox(second.AlbedoColor),
            "Distinct skin tones collapsed to one presentation material.");
        Require(first.Roughness is > 0.5f and < 0.75f,
            "Skin roughness remained flat/plastic.");
        Require(StudioMaterialLibrary.Hair(new Color("21170f")).Roughness > 0.68f,
            "Hair material remained overly glossy/plastic.");

        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var gold = project.ActiveUniformFor(game.Gold.Id);
        var navy = project.ActiveUniformFor(game.Navy.Id);
        var goldMaterial = StudioMaterialLibrary.Jersey(ToGodot(gold.JerseyBaseColor));
        var navyMaterial = StudioMaterialLibrary.Jersey(ToGodot(navy.JerseyBaseColor));
        Require(goldMaterial.AlbedoColor.IsEqualApprox(ToGodot(gold.JerseyBaseColor)) &&
                navyMaterial.AlbedoColor.IsEqualApprox(ToGodot(navy.JerseyBaseColor)),
            "Team uniform colors changed in the material adapter.");
        Require(!goldMaterial.AlbedoColor.IsEqualApprox(navyMaterial.AlbedoColor),
            "Gold and Navy uniforms are no longer visually distinct.");

        StudioMaterialLibrary.SetQuality(PresentationQualityPreset.Preview);
        Require(!goldMaterial.NormalEnabled, "Preview quality retained expensive fabric detail.");
        StudioMaterialLibrary.SetQuality(PresentationQualityPreset.Final);
        Require(goldMaterial.NormalEnabled && goldMaterial.NormalTexture is not null,
            "Final quality did not enable procedural fabric detail.");
        Require(first.SubsurfScatterEnabled && first.SubsurfScatterStrength > 0,
            "High/final skin quality did not enable the subtle subsurface approximation.");
    }

    private static string SimulationSnapshot(PlaySimulation simulation) => string.Join('|',
        simulation.Frames.SelectMany(frame => frame.Players.OrderBy(item => item.Key)
            .Select(item => $"{frame.TimeSeconds:R}:{item.Key}:{item.Value.Position}:{item.Value.FacingDirection}"))) +
        $"|{simulation.Outcome.Kind}:{simulation.Outcome.EndTimeSeconds:R}";

    private static string AnimationSnapshot(FootballAnimationTimeline timeline) => string.Join('|',
        timeline.Frames.SelectMany(frame => frame.Players.OrderBy(item => item.Key)
            .Select(item => $"{frame.TimeSeconds:R}:{item.Key}:{item.Value}")));

    private static Color ToGodot(AppearanceColor color) => Color.Color8(color.R, color.G, color.B);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
