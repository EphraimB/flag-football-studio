using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PlayerGenesisValidator : Node
{
    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var originalVoice = project.VoiceProfileFor(player.Id);
        var specification = project.CharacterSpecificationFor(player.Id);
        var personality = project.PersonalityFor(player.Id);

        ValidateStages(specification);
        ValidateSemanticEdits(project, player.Id);
        ValidatePersonalityIsolation(game, project, player.Id, originalVoice);
        ValidateDirectorContract(project, player.Id);
        ValidatePersistence(project, player.Id);
        ValidateOlderProjectDefaults(project, player.Id);
        await ValidatePresentationAsync(project, game, player.Id);
    }

    private static void ValidateStages(CharacterSpecification specification)
    {
        var staged = specification.Clone();
        staged.SetStage(GenesisStage.Blueprint);
        var controller = new GenesisStageController(staged);
        foreach (var expected in new[] { GenesisStage.BodyFrame, GenesisStage.AnatomicalForm,
                     GenesisStage.IdentityAppearance, GenesisStage.HairDetails, GenesisStage.ClothingUniform,
                     GenesisStage.PersonalityVoice, GenesisStage.Complete })
        {
            Require(controller.Continue() == expected, $"Genesis did not advance to {expected}.");
            Require(controller.State == GenesisMaterializationState.Materializing, "Stage transition did not enter Materializing.");
            controller.MarkReady();
        }
        Require(controller.Progress == 1 && controller.Continue() == GenesisStage.Complete,
            "Genesis completion did not clamp safely.");
        Require(controller.Back() == GenesisStage.PersonalityVoice, "Genesis could not move backward without changing identity.");
        controller.BeginProcessing();
        controller.BeginApplying();
        controller.Fail("validation failure");
        Require(controller.State == GenesisMaterializationState.Failed && controller.FailureReason == "validation failure",
            "Genesis failure state did not retain its reason.");
    }

    private static void ValidateSemanticEdits(GameProject project, Guid playerId)
    {
        var history = new CharacterEditHistory(project.CharacterSpecificationFor(playerId));
        var setHeight = history.Apply(Plan(CharacterEditOperationKind.SetPhysicalFact,
            CharacterSemanticPaths.Height, CharacterPropertyValue.FromNumber(1.68), "Exact height"));
        Require(setHeight.Validation.IsValid && BitConverter.SingleToInt32Bits(history.Current.PhysicalFacts.HeightMeters) ==
                BitConverter.SingleToInt32Bits(1.68f), "Exact height edit was not deterministic.");

        var lockHeight = history.Apply(new CharacterEditPlan(Guid.NewGuid(),
            [CharacterEditOperation.LockProperty(CharacterSemanticPaths.Height)], "Lock height"));
        Require(lockHeight.Validation.IsValid && history.Current.IsLocked(CharacterSemanticPaths.Height), "Height lock failed.");
        var heightBits = BitConverter.SingleToInt32Bits(history.Current.PhysicalFacts.HeightMeters);
        var hairEdit = history.Apply(Plan(CharacterEditOperationKind.SetHairCharacteristic,
            CharacterSemanticPaths.HairStyle, CharacterPropertyValue.FromText(HairStyle.Curly.ToString()), "Set curly hair"));
        Require(hairEdit.Validation.IsValid && BitConverter.SingleToInt32Bits(history.Current.PhysicalFacts.HeightMeters) == heightBits,
            "An unrelated appearance edit changed locked height.");
        var blockedHeight = history.Apply(Plan(CharacterEditOperationKind.SetPhysicalFact,
            CharacterSemanticPaths.Height, CharacterPropertyValue.FromNumber(1.9), "Illegal height change"));
        Require(!blockedHeight.Validation.IsValid && BitConverter.SingleToInt32Bits(history.Current.PhysicalFacts.HeightMeters) == heightBits,
            "A locked physical fact was modified.");

        var faceEdit = history.Apply(Plan(CharacterEditOperationKind.AdjustFaceCharacteristic,
            "face.jaw_width", CharacterPropertyValue.FromNumber(0.08), "Widen jaw"));
        Require(faceEdit.Validation.IsValid, "Face edit failed.");
        var jaw = history.Current.Value("face.jaw_width");
        Require(history.Apply(new CharacterEditPlan(Guid.NewGuid(),
            [CharacterEditOperation.LockCategory(CharacterIdentityCategory.FaceIdentity)], "Lock face")).Validation.IsValid,
            "Face category lock failed.");
        Require(history.Apply(Plan(CharacterEditOperationKind.SetHairCharacteristic,
            CharacterSemanticPaths.HairCurl, CharacterPropertyValue.FromNumber(0.86), "Curl hair")).Validation.IsValid,
            "Hair edit failed while face was locked.");
        Require(history.Current.Value("face.jaw_width") == jaw, "Hair editing changed a locked face.");

        var beforeHair = history.Current.Value(CharacterSemanticPaths.HairLength);
        var changedHair = CharacterPropertyValue.FromNumber(1.31);
        Require(history.Apply(Plan(CharacterEditOperationKind.SetHairCharacteristic,
            CharacterSemanticPaths.HairLength, changedHair, "Lengthen hair")).Validation.IsValid,
            "Hair transaction failed.");
        Require(history.Current.Value(CharacterSemanticPaths.HairLength) == changedHair, "Hair edit was not applied exactly.");
        Require(history.Undo().Value(CharacterSemanticPaths.HairLength) == beforeHair, "Undo did not restore exact hair identity.");
        Require(history.Redo().Value(CharacterSemanticPaths.HairLength) == changedHair, "Redo did not restore exact hair edit.");

        project.SetCharacterSpecification(history.Current);
        CharacterSpecificationAppearanceAdapter.Apply(history.Current, project.AppearanceFor(playerId));
        Require(BitConverter.SingleToInt32Bits(project.AppearanceFor(playerId).HeightMeters) == heightBits &&
                project.AppearanceFor(playerId).Hair.Style == HairStyle.Curly,
            "Character specification did not adapt to the established appearance model.");
    }

    private static void ValidatePersonalityIsolation(Game game, GameProject project, Guid playerId, PlayerVoiceProfile voice)
    {
        var serializer = new ProjectJsonSerializer();
        var specificationBefore = SpecificationSignature(project.CharacterSpecificationFor(playerId));
        var voiceBefore = (voice.Id, voice.BackendType, voice.ModelIdOrPath, voice.DefaultSpeakingRate,
            voice.DefaultPitchAdjustment, voice.DefaultSpeakingVolume);
        var simulator = new FootballPlaySimulator();
        var simulationBefore = simulator.Simulate(project.Plays[0], game.Gold, game.Navy);

        var personality = project.PersonalityFor(playerId);
        personality.SetTraits(0.91f, 0.18f, 0.84f, 0.79f, 0.62f, 0.73f, 0.76f, 0.88f);
        var presentation = PersonalityPresentationMapper.Map(personality);
        Require(personality.DerivedStyleLabels.Contains("Quiet") && personality.DerivedStyleLabels.Contains("Supportive") &&
                presentation.BodyLanguageConfidence == personality.Confidence,
            "Personality traits did not produce bounded presentation metadata.");
        RequireThrows(() => personality.SetTraits(1.01f, 0, 0, 0, 0, 0, 0, 0),
            "Out-of-range personality traits were accepted.");

        var simulationAfter = simulator.Simulate(project.Plays[0], game.Gold, game.Navy);
        Require(SpecificationSignature(project.CharacterSpecificationFor(playerId)) == specificationBefore,
            "Personality changed visual CharacterSpecification.");
        Require((voice.Id, voice.BackendType, voice.ModelIdOrPath, voice.DefaultSpeakingRate,
                    voice.DefaultPitchAdjustment, voice.DefaultSpeakingVolume) == voiceBefore,
            "Personality changed PlayerVoiceProfile.");
        Require(SimulationsEqual(simulationBefore, simulationAfter),
            "Personality altered authoritative football simulation output.");
        _ = serializer;
    }

    private static void ValidateDirectorContract(GameProject project, Guid playerId)
    {
        var plan = Plan(CharacterEditOperationKind.SetHairCharacteristic, CharacterSemanticPaths.HairStyle,
            CharacterPropertyValue.FromText(HairStyle.Bun.ToString()), "Future provider proposal");
        var request = new CharacterDirectorRequest(playerId,
            [new CharacterDirectorInput(CharacterDirectorInputKind.Text, "Change only the hairstyle")],
            project.CharacterSpecificationFor(playerId).Clone(), project.PersonalityFor(playerId).Clone());
        var proposal = new CharacterDirectionProposal(plan, null, "Inspectable deterministic proposal");
        var result = CharacterEditService.Apply(request.CurrentSpecification, proposal.CharacterEdits);
        Require(result.Validation.IsValid && proposal.RequiresUserConfirmation &&
                result.Specification.Value(CharacterSemanticPaths.HairStyle).TextValue == HairStyle.Bun.ToString(),
            "Character Director contract could not produce an inspectable validated edit plan.");
    }

    private static void ValidatePersistence(GameProject project, Guid playerId)
    {
        var serializer = new ProjectJsonSerializer();
        var json = serializer.SerializeProject(project);
        var loaded = serializer.DeserializeProject(json);
        var before = project.CharacterSpecificationFor(playerId);
        var after = loaded.CharacterSpecificationFor(playerId);
        Require(before.Version == after.Version && SpecificationSignature(before) == SpecificationSignature(after),
            "CharacterSpecification project JSON round trip failed.");
        Require(PersonalitySignature(project.PersonalityFor(playerId)) == PersonalitySignature(loaded.PersonalityFor(playerId)),
            "PlayerPersonalityProfile project JSON round trip failed.");
        Require(after.LockedProperties.Contains(CharacterSemanticPaths.Height) &&
                after.LockedCategories.Contains(CharacterIdentityCategory.FaceIdentity),
            "Character locks did not persist.");
    }

    private static void ValidateOlderProjectDefaults(GameProject project, Guid playerId)
    {
        var serializer = new ProjectJsonSerializer();
        var root = JsonNode.Parse(serializer.SerializeProject(project))!.AsObject();
        root.Remove("characterSpecifications");
        root.Remove("playerPersonalities");
        var loaded = serializer.DeserializeProject(root.ToJsonString());
        var migrated = loaded.CharacterSpecificationFor(playerId);
        Require(migrated.Version == CharacterSpecification.CurrentVersion && migrated.Stage == GenesisStage.Complete &&
                migrated.PhysicalFacts.HeightMeters == loaded.AppearanceFor(playerId).HeightMeters,
            "Existing project migration did not create safe Genesis defaults.");
        Require(loaded.PersonalityFor(playerId).DerivedStyleLabels.Contains("Balanced"),
            "Existing project migration did not create an independent default personality.");
    }

    private async Task ValidatePresentationAsync(GameProject project, Game game, Guid playerId)
    {
        var player = game.Gold.Roster.First(item => item.Id == playerId);
        var pawn = new PlayerPawn { Name = "GenesisPresentationValidation", Visible = false };
        pawn.Configure(player, project.AppearanceFor(playerId), project.ActiveUniformFor(game.Gold.Id));
        AddChild(pawn);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var eyeBefore = pawn.EyeAnchor.GlobalPosition;
        pawn.SetGenesisPresentation(GenesisStage.Blueprint, GenesisMaterializationState.Materializing,
            project.CharacterSpecificationFor(playerId).PhysicalFacts.HeightMeters);
        Require(pawn.GenesisHologramActive && pawn.GenesisStage == GenesisStage.Blueprint &&
                pawn.GenesisMaterializationState == GenesisMaterializationState.Materializing,
            "Holographic blueprint did not activate through CharacterVisualController.");
        Require(pawn.EyeAnchor.GlobalPosition == eyeBefore && pawn.MouthAudioAnchor is not null &&
                pawn.LeftHandAnchor is not null && pawn.RightHandAnchor is not null,
            "Genesis presentation disturbed stable character anchors.");
        var pov = new Camera3D { CullMask = uint.MaxValue & ~HumanoidRig.FirstPersonHeadLayerMask };
        AddChild(pov);
        pawn.SetFirstPersonView(true);
        Require(!pawn.HeadGeometryVisibleTo(pov) && pawn.BodyGeometryVisibleTo(pov),
            "Genesis presentation regressed camera-specific POV visibility.");
        pawn.SetGenesisPresentation(GenesisStage.Complete, GenesisMaterializationState.Ready,
            project.CharacterSpecificationFor(playerId).PhysicalFacts.HeightMeters);
        Require(!pawn.GenesisHologramActive, "Complete Genesis player remained in hologram mode.");

        var panel = new PlayerStudioPanel { Name = "GenesisWorkspaceValidation", Visible = false };
        AddChild(panel);
        panel.Configure(project, game);
        Require(panel.FindChild("Genesis", true, false) is ScrollContainer &&
                panel.FindChild("Personality", true, false) is ScrollContainer &&
                panel.GenesisStageSummary.Contains("STAGE", StringComparison.Ordinal),
            "Players workspace did not expose the Genesis-first identity workflow.");
        panel.QueueFree();
        pov.QueueFree();
        pawn.QueueFree();
    }

    private static CharacterEditPlan Plan(CharacterEditOperationKind kind, string path,
        CharacterPropertyValue value, string explanation) =>
        new(Guid.NewGuid(), [new CharacterEditOperation(kind, path, value)], explanation);

    private static string SpecificationSignature(CharacterSpecification specification) =>
        string.Join('|', specification.PlayerId, specification.Version, specification.Stage,
            specification.PhysicalFacts.HeightMeters.ToString("R"), specification.PhysicalFacts.WeightKilograms.ToString("R"),
            specification.PhysicalFacts.AgeYears, specification.PhysicalFacts.Position, specification.PhysicalFacts.DominantHand,
            string.Join(';', specification.Properties.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")),
            string.Join(',', specification.LockedCategories.OrderBy(value => value)),
            string.Join(',', specification.LockedProperties.OrderBy(value => value)));

    private static string PersonalitySignature(PlayerPersonalityProfile profile) =>
        string.Join('|', profile.PlayerId, profile.Confidence.ToString("R"), profile.Talkativeness.ToString("R"),
            profile.Competitiveness.ToString("R"), profile.Encouragement.ToString("R"), profile.Playfulness.ToString("R"),
            profile.EmotionalIntensity.ToString("R"), profile.Calmness.ToString("R"), profile.Leadership.ToString("R"));

    private static bool SimulationsEqual(PlaySimulation left, PlaySimulation right)
    {
        if (left.Outcome != right.Outcome || left.Events.Count != right.Events.Count || left.Frames.Count != right.Frames.Count)
            return false;
        for (var index = 0; index < left.Events.Count; index++)
            if (left.Events[index] != right.Events[index]) return false;
        for (var index = 0; index < left.Frames.Count; index++)
        {
            var a = left.Frames[index];
            var b = right.Frames[index];
            if (a.TimeSeconds != b.TimeSeconds || a.Ball != b.Ball || a.Possession != b.Possession || a.Players.Count != b.Players.Count)
                return false;
            foreach (var pair in a.Players)
                if (!b.Players.TryGetValue(pair.Key, out var state) || pair.Value != state) return false;
        }
        return true;
    }

    private static void RequireThrows(Action action, string message)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
