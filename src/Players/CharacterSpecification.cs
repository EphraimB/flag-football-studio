using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace FlagFootballStudio.Domain;

public enum GenesisStage
{
    Blueprint,
    BodyFrame,
    AnatomicalForm,
    IdentityAppearance,
    HairDetails,
    ClothingUniform,
    PersonalityVoice,
    Complete
}

public enum GenesisMaterializationState
{
    Waiting,
    Processing,
    Applying,
    Materializing,
    Ready,
    Failed
}

public enum DominantHand { Left, Right, Ambidextrous }

public enum BodyFrameDescription { Lean, Athletic, Muscular, Stocky, Average }

public enum CharacterIdentityCategory
{
    PhysicalFacts,
    BodyIdentity,
    FaceIdentity,
    SkinIdentity,
    EyeIdentity,
    HairIdentity,
    ClothingIdentity,
    AccessoryIdentity
}

public enum CharacterValueKind { Number, Integer, Text, Boolean, Color }

public readonly record struct CharacterPropertyValue(
    CharacterValueKind Kind,
    double NumberValue,
    int IntegerValue,
    string TextValue,
    bool BooleanValue,
    AppearanceColor ColorValue)
{
    public static CharacterPropertyValue FromNumber(double value) =>
        new(CharacterValueKind.Number, value, 0, string.Empty, false, default);
    public static CharacterPropertyValue FromInteger(int value) =>
        new(CharacterValueKind.Integer, 0, value, string.Empty, false, default);
    public static CharacterPropertyValue FromText(string value) =>
        new(CharacterValueKind.Text, 0, 0, value?.Trim() ?? string.Empty, false, default);
    public static CharacterPropertyValue FromBoolean(bool value) =>
        new(CharacterValueKind.Boolean, 0, 0, string.Empty, value, default);
    public static CharacterPropertyValue FromColor(AppearanceColor value) =>
        new(CharacterValueKind.Color, 0, 0, string.Empty, false, value);
}

public sealed class PlayerPhysicalFacts
{
    public const float MinimumHeightMeters = 1.4f;
    public const float MaximumHeightMeters = 2.3f;
    public const float MinimumWeightKilograms = 35f;
    public const float MaximumWeightKilograms = 220f;
    public const int MinimumAgeYears = 13;
    public const int MaximumAgeYears = 80;

    public PlayerPhysicalFacts(float heightMeters, float weightKilograms, int ageYears,
        PlayerPosition position, DominantHand dominantHand)
    {
        SetHeight(heightMeters);
        SetWeight(weightKilograms);
        SetAge(ageYears);
        SetPosition(position);
        SetDominantHand(dominantHand);
    }

    public float HeightMeters { get; private set; }
    public float WeightKilograms { get; private set; }
    public int AgeYears { get; private set; }
    public PlayerPosition Position { get; private set; }
    public DominantHand DominantHand { get; private set; }

    public void SetHeight(float value)
    {
        if (!float.IsFinite(value) || value is < MinimumHeightMeters or > MaximumHeightMeters)
            throw new ArgumentOutOfRangeException(nameof(value));
        HeightMeters = value;
    }

    public void SetWeight(float value)
    {
        if (!float.IsFinite(value) || value is < MinimumWeightKilograms or > MaximumWeightKilograms)
            throw new ArgumentOutOfRangeException(nameof(value));
        WeightKilograms = value;
    }

    public void SetAge(int value)
    {
        if (value is < MinimumAgeYears or > MaximumAgeYears)
            throw new ArgumentOutOfRangeException(nameof(value));
        AgeYears = value;
    }

    public void SetPosition(PlayerPosition value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Position = value;
    }

    public void SetDominantHand(DominantHand value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        DominantHand = value;
    }

    public PlayerPhysicalFacts Clone() => new(HeightMeters, WeightKilograms, AgeYears, Position, DominantHand);
}

public sealed class CharacterReferenceAssets
{
    public CharacterReferenceAssets(string? headIdentity = null, string? facialTextures = null,
        string? morphData = null, string? hairAsset = null, string? detailMaps = null)
    {
        HeadIdentity = Validate(headIdentity);
        FacialTextures = Validate(facialTextures);
        MorphData = Validate(morphData);
        HairAsset = Validate(hairAsset);
        DetailMaps = Validate(detailMaps);
    }

    public string? HeadIdentity { get; }
    public string? FacialTextures { get; }
    public string? MorphData { get; }
    public string? HairAsset { get; }
    public string? DetailMaps { get; }

    public CharacterReferenceAssets Clone() =>
        new(HeadIdentity, FacialTextures, MorphData, HairAsset, DetailMaps);

    private static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Contains("..", StringComparer.Ordinal))
            throw new ArgumentException("Character reference assets must use safe project-relative references.", nameof(value));
        return normalized;
    }
}

public sealed class CharacterSpecification
{
    public const int CurrentVersion = 1;
    private readonly Dictionary<string, CharacterPropertyValue> _properties;
    private readonly HashSet<CharacterIdentityCategory> _lockedCategories;
    private readonly HashSet<string> _lockedProperties;
    private readonly ReadOnlyDictionary<string, CharacterPropertyValue> _readOnlyProperties;

    public CharacterSpecification(Guid playerId, PlayerPhysicalFacts physicalFacts,
        IEnumerable<KeyValuePair<string, CharacterPropertyValue>>? properties = null,
        IEnumerable<CharacterIdentityCategory>? lockedCategories = null,
        IEnumerable<string>? lockedProperties = null,
        GenesisStage stage = GenesisStage.Complete,
        CharacterReferenceAssets? referenceAssets = null,
        int version = CurrentVersion)
    {
        if (playerId == Guid.Empty) throw new ArgumentException("A player ID is required.", nameof(playerId));
        if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), $"Unsupported character specification version {version}.");
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        PlayerId = playerId;
        Version = version;
        PhysicalFacts = physicalFacts?.Clone() ?? throw new ArgumentNullException(nameof(physicalFacts));
        Stage = stage;
        ReferenceAssets = referenceAssets?.Clone() ?? new CharacterReferenceAssets();
        _properties = properties?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) ??
            new Dictionary<string, CharacterPropertyValue>(StringComparer.Ordinal);
        _lockedCategories = lockedCategories?.ToHashSet() ?? [];
        _lockedProperties = lockedProperties?.ToHashSet(StringComparer.Ordinal) ?? [];
        _readOnlyProperties = new ReadOnlyDictionary<string, CharacterPropertyValue>(_properties);
        foreach (var pair in _properties) CharacterSemanticPropertyRegistry.Validate(pair.Key, pair.Value);
    }

    public int Version { get; }
    public Guid PlayerId { get; }
    public PlayerPhysicalFacts PhysicalFacts { get; }
    public GenesisStage Stage { get; private set; }
    public CharacterReferenceAssets ReferenceAssets { get; private set; }
    public IReadOnlyDictionary<string, CharacterPropertyValue> Properties => _readOnlyProperties;
    public IReadOnlySet<CharacterIdentityCategory> LockedCategories => _lockedCategories;
    public IReadOnlySet<string> LockedProperties => _lockedProperties;
    public float StageProgress => (float)Stage / (float)GenesisStage.Complete;
    public bool IsComplete => Stage == GenesisStage.Complete;

    public bool IsLocked(string path)
    {
        var definition = CharacterSemanticPropertyRegistry.Definition(path);
        return _lockedProperties.Contains(path) || _lockedCategories.Contains(definition.Category);
    }

    public CharacterPropertyValue Value(string path)
    {
        return path switch
        {
            CharacterSemanticPaths.Height => CharacterPropertyValue.FromNumber(PhysicalFacts.HeightMeters),
            CharacterSemanticPaths.Weight => CharacterPropertyValue.FromNumber(PhysicalFacts.WeightKilograms),
            CharacterSemanticPaths.Age => CharacterPropertyValue.FromInteger(PhysicalFacts.AgeYears),
            CharacterSemanticPaths.Position => CharacterPropertyValue.FromText(PhysicalFacts.Position.ToString()),
            CharacterSemanticPaths.DominantHand => CharacterPropertyValue.FromText(PhysicalFacts.DominantHand.ToString()),
            _ => _properties.TryGetValue(path, out var value)
                ? value
                : throw new KeyNotFoundException($"Character property '{path}' has no value.")
        };
    }

    internal void ApplyValidated(string path, CharacterPropertyValue value)
    {
        CharacterSemanticPropertyRegistry.Validate(path, value);
        switch (path)
        {
            case CharacterSemanticPaths.Height:
                PhysicalFacts.SetHeight((float)value.NumberValue);
                break;
            case CharacterSemanticPaths.Weight:
                PhysicalFacts.SetWeight((float)value.NumberValue);
                break;
            case CharacterSemanticPaths.Age:
                PhysicalFacts.SetAge(value.IntegerValue);
                break;
            case CharacterSemanticPaths.Position:
                PhysicalFacts.SetPosition(Enum.Parse<PlayerPosition>(value.TextValue, true));
                break;
            case CharacterSemanticPaths.DominantHand:
                PhysicalFacts.SetDominantHand(Enum.Parse<DominantHand>(value.TextValue, true));
                break;
            default:
                _properties[path] = value;
                break;
        }
    }

    internal void SetCategoryLock(CharacterIdentityCategory category, bool locked)
    {
        if (locked) _lockedCategories.Add(category);
        else _lockedCategories.Remove(category);
    }

    internal void SetPropertyLock(string path, bool locked)
    {
        CharacterSemanticPropertyRegistry.Definition(path);
        if (locked) _lockedProperties.Add(path);
        else _lockedProperties.Remove(path);
    }

    public void SetStage(GenesisStage stage)
    {
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        Stage = stage;
    }

    public void SetReferenceAssets(CharacterReferenceAssets assets) =>
        ReferenceAssets = (assets ?? throw new ArgumentNullException(nameof(assets))).Clone();

    public CharacterSpecification Clone() => new(PlayerId, PhysicalFacts, _properties,
        _lockedCategories, _lockedProperties, Stage, ReferenceAssets, Version);

    public static CharacterSpecification CreateDefault(Player player, PlayerAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(appearance);
        var estimatedWeight = Math.Clamp(75f * MathF.Pow(appearance.HeightMeters / 1.8f, 2),
            PlayerPhysicalFacts.MinimumWeightKilograms, PlayerPhysicalFacts.MaximumWeightKilograms);
        var specification = new CharacterSpecification(player.Id,
            new PlayerPhysicalFacts(appearance.HeightMeters, estimatedWeight, 21, player.Position, DominantHand.Right));
        CharacterSpecificationAppearanceAdapter.Capture(appearance, specification);
        return specification;
    }
}

public sealed class GenesisStageController
{
    public GenesisStageController(CharacterSpecification specification)
    {
        Specification = specification ?? throw new ArgumentNullException(nameof(specification));
        State = GenesisMaterializationState.Ready;
    }

    public CharacterSpecification Specification { get; }
    public GenesisMaterializationState State { get; private set; }
    public string? FailureReason { get; private set; }
    public float Progress => Specification.StageProgress;

    public GenesisStage Back() => Move(-1);
    public GenesisStage Continue() => Move(1);
    public void BeginProcessing() => SetTransientState(GenesisMaterializationState.Processing);
    public void BeginApplying() => SetTransientState(GenesisMaterializationState.Applying);
    public void BeginMaterializing() => SetTransientState(GenesisMaterializationState.Materializing);
    public void MarkReady() => SetTransientState(GenesisMaterializationState.Ready);

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A failure reason is required.", nameof(reason));
        FailureReason = reason.Trim();
        State = GenesisMaterializationState.Failed;
    }

    private GenesisStage Move(int direction)
    {
        var next = Math.Clamp((int)Specification.Stage + direction, 0, (int)GenesisStage.Complete);
        Specification.SetStage((GenesisStage)next);
        FailureReason = null;
        State = GenesisMaterializationState.Materializing;
        return Specification.Stage;
    }

    private void SetTransientState(GenesisMaterializationState state)
    {
        if (state == GenesisMaterializationState.Failed)
            throw new ArgumentException("Use Fail so a reason is retained.", nameof(state));
        FailureReason = null;
        State = state;
    }
}
