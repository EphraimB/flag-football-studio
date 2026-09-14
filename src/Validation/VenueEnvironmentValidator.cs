using System;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class VenueEnvironmentValidator : Node3D
{
    public void Run()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var simulator = new FootballPlaySimulator();
        var baseline = SimulationSnapshot(simulator.Simulate(play, game.Gold, game.Navy));
        var materialsBefore = StudioMaterialLibrary.CachedMaterialCount;

        var venue = new VenueEnvironment { Name = "EnvironmentUnderTest" };
        AddChild(venue);
        venue.SyncScoreboard(project);
        ValidateScoreboard(project, venue);
        ValidatePresetAndQualityComplexity(venue);
        ValidateCameraClearance(play, venue);
        ValidateVisibilityControls(venue);
        ValidateAudioHooks(venue);

        var deterministicSettings = new VenuePresentationSettings(
            VenuePreset.CommunityField, 0.73f, true, true);
        venue.Apply(deterministicSettings, PresentationQualityPreset.High);
        var signature = venue.SpectatorPlacementSignature;
        var second = new VenueEnvironment { Name = "DeterministicComparisonVenue" };
        AddChild(second);
        second.Apply(deterministicSettings, PresentationQualityPreset.High);
        Require(signature == second.SpectatorPlacementSignature,
            "Identical venue settings produced different spectator placement.");
        Require(venue.SpectatorsUseSharedResources && second.SpectatorsUseSharedResources,
            "Spectator batches stopped sharing reusable mesh resources.");
        Require(venue.SpectatorMultiMeshCount == 3 && second.SpectatorMultiMeshCount == 3,
            "Spectators are no longer bounded to the head/torso/legs MultiMesh batches.");
        Require(venue.StandingSpectatorCount > 0 && venue.SeatedSpectatorCount > 0,
            "The spectator foundation did not include seated and standing variants.");

        Require(StudioMaterialLibrary.CachedMaterialCount <= materialsBefore + 16,
            "Venue creation allocated an excessive number of material resources.");
        var after = SimulationSnapshot(simulator.Simulate(play, game.Gold, game.Navy));
        Require(after == baseline,
            "Presentation-only venue changes altered simulation coordinates, timing, or outcome.");

        second.QueueFree();
        venue.QueueFree();
    }

    private static void ValidateScoreboard(GameProject project, VenueEnvironment venue)
    {
        project.SetGameState(12, 7, 3, 134, 2, 6, project.HomeTeam.Id);
        venue.SyncScoreboard(project);
        Require(venue.ScoreText.Contains("12", StringComparison.Ordinal) &&
                venue.ScoreText.Contains("7", StringComparison.Ordinal) &&
                venue.ScoreText.Contains(project.HomeTeam.Name.ToUpperInvariant(), StringComparison.Ordinal) &&
                venue.ScoreText.Contains(project.AwayTeam.Name.ToUpperInvariant(), StringComparison.Ordinal),
            "Physical scoreboard did not mirror team names and scores.");
        Require(venue.ClockText.Contains("Q3", StringComparison.Ordinal) &&
                venue.ClockText.Contains("02:14", StringComparison.Ordinal),
            "Physical scoreboard did not mirror quarter and game clock.");
    }

    private static void ValidatePresetAndQualityComplexity(VenueEnvironment venue)
    {
        var fullCrowd = new VenuePresentationSettings(VenuePreset.CollegeField, 1, true, true);
        venue.Apply(fullCrowd, PresentationQualityPreset.Preview);
        var previewSpectators = venue.SpectatorCount;
        var previewComponents = venue.FixedComponentCount + venue.EquipmentComponentCount;
        venue.Apply(fullCrowd, PresentationQualityPreset.High);
        var highSpectators = venue.SpectatorCount;
        var highComponents = venue.FixedComponentCount + venue.EquipmentComponentCount;
        venue.Apply(fullCrowd, PresentationQualityPreset.Final);
        var finalSpectators = venue.SpectatorCount;
        var finalComponents = venue.FixedComponentCount + venue.EquipmentComponentCount;
        Require(previewSpectators < highSpectators && highSpectators < finalSpectators,
            "Quality presets did not progressively increase crowd density.");
        Require(previewComponents < highComponents && highComponents <= finalComponents,
            "Quality presets did not progressively increase venue detail.");

        venue.Apply(new VenuePresentationSettings(VenuePreset.PracticeField, 1, true, true),
            PresentationQualityPreset.Final);
        var practiceSections = venue.BleacherSectionCount;
        venue.Apply(new VenuePresentationSettings(VenuePreset.CommunityField, 1, true, true),
            PresentationQualityPreset.Final);
        var communitySections = venue.BleacherSectionCount;
        venue.Apply(fullCrowd, PresentationQualityPreset.Final);
        Require(practiceSections < communitySections && communitySections < venue.BleacherSectionCount,
            "Practice, Community, and College presets did not change venue complexity.");
        Require(venue.SpectatorViewpoints.Count > 0 && venue.SpectatorViewpoints.All(point => point.Y > 1),
            "Bleachers do not expose usable future spectator/360 viewpoints.");
    }

    private static void ValidateCameraClearance(PlayDefinition play, VenueEnvironment venue)
    {
        venue.Apply(VenuePresentationSettings.Default, PresentationQualityPreset.Final);
        foreach (var side in new[] { -1f, 1f })
        {
            var sideline = new Vector3(side * VenueLayout.SidelineCameraX, 3.2f, 0);
            Require(!venue.IntersectsFixedGeometry(sideline, 0.5f),
                $"Default {side} sideline camera intersects fixed venue geometry.");
        }
        foreach (var start in play.StartingPositions.Values)
        {
            var eye = new Vector3(start.X, 1.65f, start.Y);
            Require(!venue.IntersectsFixedGeometry(eye, 0.3f),
                "A Player POV formation start lies inside venue geometry.");
        }
        Require(!venue.IntersectsFixedGeometry(new Vector3(15, 19, 24), 0.5f),
            "Broadcast Wide camera lies inside venue geometry.");
        Require(venue.FixedGeometryBounds.All(obstacle =>
                Math.Abs(obstacle.Bounds.GetCenter().X) <= 24 &&
                Math.Abs(obstacle.Bounds.GetCenter().Z) <= 25),
            "Venue scale grew beyond the intended community/college framing envelope.");
    }

    private static void ValidateVisibilityControls(VenueEnvironment venue)
    {
        venue.Apply(new VenuePresentationSettings(VenuePreset.CommunityField, 1, false, false),
            PresentationQualityPreset.High);
        Require(venue.SpectatorCount == 0, "Show/hide spectators did not suppress crowd instances.");
        Require(venue.EquipmentComponentCount == 0,
            "Show/hide sideline equipment did not suppress equipment geometry.");
        venue.Apply(new VenuePresentationSettings(VenuePreset.CommunityField, 1, true, true),
            PresentationQualityPreset.High);
        Require(venue.SpectatorCount > 0 && venue.EquipmentComponentCount > 0,
            "Venue visibility controls did not restore spectators and equipment.");
    }

    private static void ValidateAudioHooks(VenueEnvironment venue)
    {
        foreach (var hook in Enum.GetValues<VenueAudioHookKind>())
            Require(venue.AudioAnchors.ContainsKey(hook), $"Missing future audio hook: {hook}.");
        Require(venue.AudioAnchors.Values.All(anchor => anchor.GetChildCount() == 0),
            "Venue audio hooks unexpectedly created streams or playback nodes.");
    }

    private static string SimulationSnapshot(PlaySimulation simulation) => string.Join('|',
        simulation.Frames.SelectMany(frame => frame.Players.OrderBy(item => item.Key)
            .Select(item => $"{frame.TimeSeconds:R}:{item.Key}:{item.Value.Position}:{item.Value.FacingDirection}"))) +
        $"|{simulation.Outcome.Kind}:{simulation.Outcome.EndTimeSeconds:R}:{simulation.Outcome.YardsGained}";

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
