using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public static class CharacterSemanticPaths
{
    public const string Height = "facts.height_meters";
    public const string Weight = "facts.weight_kilograms";
    public const string Age = "facts.age_years";
    public const string Position = "facts.position";
    public const string DominantHand = "facts.dominant_hand";
    public const string BodyFrame = "body.frame";
    public const string ShoulderWidth = "body.shoulder_width";
    public const string ChestWidth = "body.chest_width";
    public const string WaistWidth = "body.waist_width";
    public const string HipWidth = "body.hip_width";
    public const string ArmLength = "body.arm_length";
    public const string LegLength = "body.leg_length";
    public const string SkinTone = "skin.tone";
    public const string EyeColor = "eyes.color";
    public const string HairStyle = "hair.style";
    public const string HairLength = "hair.length";
    public const string HairVolume = "hair.volume";
    public const string HairlineHeight = "hair.hairline_height";
    public const string HairPart = "hair.part_position";
    public const string HairCurl = "hair.curl_amount";
    public const string HairColor = "hair.color";
    public const string PonytailLength = "hair.ponytail_length";
    public const string PonytailVolume = "hair.ponytail_volume";
    public const string BunSize = "hair.bun_size";
    public const string ClothingPresentation = "clothing.presentation";
    public const string Accessories = "accessories.flags";
}

public sealed record CharacterSemanticPropertyDefinition(
    string Path,
    CharacterIdentityCategory Category,
    CharacterValueKind Kind,
    double? Minimum = null,
    double? Maximum = null,
    IReadOnlySet<string>? AllowedText = null);

public static class CharacterSemanticPropertyRegistry
{
    private static readonly Dictionary<string, CharacterSemanticPropertyDefinition> Definitions = Build();

    public static IReadOnlyCollection<CharacterSemanticPropertyDefinition> All => Definitions.Values;

    public static CharacterSemanticPropertyDefinition Definition(string path) =>
        Definitions.TryGetValue(path ?? string.Empty, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown semantic character property '{path}'.");

    public static void Validate(string path, CharacterPropertyValue value)
    {
        var definition = Definition(path);
        if (definition.Kind != value.Kind)
            throw new ArgumentException($"Property '{path}' expects {definition.Kind}, not {value.Kind}.", nameof(value));
        if (value.Kind == CharacterValueKind.Number)
        {
            if (!double.IsFinite(value.NumberValue) ||
                (definition.Minimum.HasValue && value.NumberValue < definition.Minimum.Value) ||
                (definition.Maximum.HasValue && value.NumberValue > definition.Maximum.Value))
                throw new ArgumentOutOfRangeException(nameof(value), $"Property '{path}' is outside its supported range.");
        }
        else if (value.Kind == CharacterValueKind.Integer)
        {
            if ((definition.Minimum.HasValue && value.IntegerValue < definition.Minimum.Value) ||
                (definition.Maximum.HasValue && value.IntegerValue > definition.Maximum.Value))
                throw new ArgumentOutOfRangeException(nameof(value), $"Property '{path}' is outside its supported range.");
        }
        else if (value.Kind == CharacterValueKind.Text)
        {
            if (string.IsNullOrWhiteSpace(value.TextValue))
                throw new ArgumentException($"Property '{path}' cannot be empty.", nameof(value));
            if (definition.AllowedText is not null && !definition.AllowedText.Contains(value.TextValue))
                throw new ArgumentException($"Property '{path}' does not accept '{value.TextValue}'.", nameof(value));
        }
    }

    private static Dictionary<string, CharacterSemanticPropertyDefinition> Build()
    {
        var definitions = new Dictionary<string, CharacterSemanticPropertyDefinition>(StringComparer.Ordinal);
        Add(CharacterSemanticPaths.Height, CharacterIdentityCategory.PhysicalFacts, CharacterValueKind.Number,
            PlayerPhysicalFacts.MinimumHeightMeters, PlayerPhysicalFacts.MaximumHeightMeters);
        Add(CharacterSemanticPaths.Weight, CharacterIdentityCategory.PhysicalFacts, CharacterValueKind.Number,
            PlayerPhysicalFacts.MinimumWeightKilograms, PlayerPhysicalFacts.MaximumWeightKilograms);
        Add(CharacterSemanticPaths.Age, CharacterIdentityCategory.PhysicalFacts, CharacterValueKind.Integer,
            PlayerPhysicalFacts.MinimumAgeYears, PlayerPhysicalFacts.MaximumAgeYears);
        AddText(CharacterSemanticPaths.Position, CharacterIdentityCategory.PhysicalFacts, Enum.GetNames<PlayerPosition>());
        AddText(CharacterSemanticPaths.DominantHand, CharacterIdentityCategory.PhysicalFacts, Enum.GetNames<DominantHand>());
        AddText(CharacterSemanticPaths.BodyFrame, CharacterIdentityCategory.BodyIdentity, Enum.GetNames<BodyFrameDescription>());
        foreach (var path in new[] { CharacterSemanticPaths.ShoulderWidth, CharacterSemanticPaths.ChestWidth,
                     CharacterSemanticPaths.WaistWidth, CharacterSemanticPaths.HipWidth })
            Add(path, CharacterIdentityCategory.BodyIdentity, CharacterValueKind.Number, 0.7, 1.3);
        foreach (var path in new[] { CharacterSemanticPaths.ArmLength, CharacterSemanticPaths.LegLength })
            Add(path, CharacterIdentityCategory.BodyIdentity, CharacterValueKind.Number, 0.75, 1.25);
        Add(CharacterSemanticPaths.SkinTone, CharacterIdentityCategory.SkinIdentity, CharacterValueKind.Color);
        Add(CharacterSemanticPaths.EyeColor, CharacterIdentityCategory.EyeIdentity, CharacterValueKind.Color);
        AddText(CharacterSemanticPaths.HairStyle, CharacterIdentityCategory.HairIdentity, Enum.GetNames<HairStyle>());
        Add(CharacterSemanticPaths.HairLength, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumLength, HairAppearance.MaximumLength);
        Add(CharacterSemanticPaths.HairVolume, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumVolume, HairAppearance.MaximumVolume);
        Add(CharacterSemanticPaths.HairlineHeight, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumHairlineHeight, HairAppearance.MaximumHairlineHeight);
        Add(CharacterSemanticPaths.HairPart, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumPartPosition, HairAppearance.MaximumPartPosition);
        Add(CharacterSemanticPaths.HairCurl, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumCurlAmount, HairAppearance.MaximumCurlAmount);
        Add(CharacterSemanticPaths.HairColor, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Color);
        Add(CharacterSemanticPaths.PonytailLength, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumPonytailLength, HairAppearance.MaximumPonytailLength);
        Add(CharacterSemanticPaths.PonytailVolume, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumPonytailVolume, HairAppearance.MaximumPonytailVolume);
        Add(CharacterSemanticPaths.BunSize, CharacterIdentityCategory.HairIdentity, CharacterValueKind.Number,
            HairAppearance.MinimumBunSize, HairAppearance.MaximumBunSize);
        AddText(CharacterSemanticPaths.ClothingPresentation, CharacterIdentityCategory.ClothingIdentity,
            ["TeamUniform", "Training", "Sideline"]);
        Add(CharacterSemanticPaths.Accessories, CharacterIdentityCategory.AccessoryIdentity, CharacterValueKind.Integer, 0, 15);

        var scaleFace = new[] { "head_width", "head_height", "jaw_width", "jaw_height", "chin_width",
            "cheekbone_width", "cheek_fullness", "forehead_height", "eye_spacing", "eye_size",
            "eyebrow_height", "nose_width", "nose_length", "mouth_width", "lip_fullness", "ear_size" };
        foreach (var name in scaleFace)
            Add($"face.{name}", CharacterIdentityCategory.FaceIdentity, CharacterValueKind.Number,
                FaceAppearance.MinimumScale, FaceAppearance.MaximumScale);
        foreach (var name in new[] { "chin_projection", "eye_vertical_position", "nose_projection", "ear_position" })
            Add($"face.{name}", CharacterIdentityCategory.FaceIdentity, CharacterValueKind.Number,
                FaceAppearance.MinimumOffset, FaceAppearance.MaximumOffset);
        return definitions;

        void Add(string path, CharacterIdentityCategory category, CharacterValueKind kind,
            double? minimum = null, double? maximum = null) =>
            definitions.Add(path, new CharacterSemanticPropertyDefinition(path, category, kind, minimum, maximum));
        void AddText(string path, CharacterIdentityCategory category, IEnumerable<string> values) =>
            definitions.Add(path, new CharacterSemanticPropertyDefinition(path, category, CharacterValueKind.Text,
                AllowedText: values.ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }
}

public enum CharacterEditOperationKind
{
    SetPhysicalFact,
    AdjustBodyCharacteristic,
    AdjustFaceCharacteristic,
    SetHairCharacteristic,
    SetAppearanceCharacteristic,
    SetUniformCharacteristic,
    SetAccessoryCharacteristic,
    LockCharacteristic,
    UnlockCharacteristic
}

public sealed record CharacterEditOperation(
    CharacterEditOperationKind Kind,
    string Path,
    CharacterPropertyValue? Value = null,
    CharacterIdentityCategory? CategoryTarget = null)
{
    public static CharacterEditOperation LockProperty(string path) =>
        new(CharacterEditOperationKind.LockCharacteristic, path);
    public static CharacterEditOperation UnlockProperty(string path) =>
        new(CharacterEditOperationKind.UnlockCharacteristic, path);
    public static CharacterEditOperation LockCategory(CharacterIdentityCategory category) =>
        new(CharacterEditOperationKind.LockCharacteristic, string.Empty, null, category);
    public static CharacterEditOperation UnlockCategory(CharacterIdentityCategory category) =>
        new(CharacterEditOperationKind.UnlockCharacteristic, string.Empty, null, category);
}

public sealed record CharacterEditPlanValidation(bool IsValid, bool LockedCharacteristicsRespected,
    IReadOnlyList<string> Errors);

public sealed class CharacterEditPlan
{
    public CharacterEditPlan(Guid transactionId, IEnumerable<CharacterEditOperation> operations,
        string? explanation = null, CharacterEditPlanValidation? validationResult = null)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("A transaction ID is required.", nameof(transactionId));
        TransactionId = transactionId;
        RequestedOperations = (operations ?? throw new ArgumentNullException(nameof(operations))).ToArray();
        if (RequestedOperations.Count == 0) throw new ArgumentException("At least one edit operation is required.", nameof(operations));
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        ValidationResult = validationResult;
        AffectedCategories = RequestedOperations.Select(CategoryFor).Distinct().ToArray();
    }

    public Guid TransactionId { get; }
    public IReadOnlyList<CharacterEditOperation> RequestedOperations { get; }
    public IReadOnlyList<CharacterIdentityCategory> AffectedCategories { get; }
    public string? Explanation { get; }
    public CharacterEditPlanValidation? ValidationResult { get; }

    private static CharacterIdentityCategory CategoryFor(CharacterEditOperation operation) =>
        operation.CategoryTarget ?? CharacterSemanticPropertyRegistry.Definition(operation.Path).Category;
}

public sealed record CharacterEditResult(CharacterSpecification Specification,
    CharacterEditPlan Plan, CharacterEditPlanValidation Validation);

public static class CharacterEditService
{
    public static CharacterEditResult Apply(CharacterSpecification source, CharacterEditPlan plan)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        var candidate = source.Clone();
        var errors = new List<string>();
        var respectedLocks = true;
        foreach (var operation in plan.RequestedOperations)
        {
            try
            {
                ApplyOperation(candidate, operation);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                if (exception is CharacterLockException) respectedLocks = false;
            }
        }
        var validation = new CharacterEditPlanValidation(errors.Count == 0, respectedLocks, errors.AsReadOnly());
        return new CharacterEditResult(errors.Count == 0 ? candidate : source.Clone(),
            new CharacterEditPlan(plan.TransactionId, plan.RequestedOperations, plan.Explanation, validation), validation);
    }

    private static void ApplyOperation(CharacterSpecification specification, CharacterEditOperation operation)
    {
        if (operation.Kind is CharacterEditOperationKind.LockCharacteristic or CharacterEditOperationKind.UnlockCharacteristic)
        {
            var locked = operation.Kind == CharacterEditOperationKind.LockCharacteristic;
            if (operation.CategoryTarget.HasValue) specification.SetCategoryLock(operation.CategoryTarget.Value, locked);
            else specification.SetPropertyLock(operation.Path, locked);
            return;
        }

        if (!operation.Value.HasValue) throw new ArgumentException($"Edit '{operation.Kind}' requires a value.");
        var definition = CharacterSemanticPropertyRegistry.Definition(operation.Path);
        if (specification.IsLocked(operation.Path))
            throw new CharacterLockException($"Locked characteristic '{operation.Path}' was not changed.");
        ValidateOperationCategory(operation.Kind, definition.Category);
        var value = operation.Value.Value;
        if (operation.Kind is CharacterEditOperationKind.AdjustBodyCharacteristic or CharacterEditOperationKind.AdjustFaceCharacteristic)
        {
            if (value.Kind != CharacterValueKind.Number) throw new ArgumentException("Adjust operations require a numeric delta.");
            var current = specification.Value(operation.Path);
            value = CharacterPropertyValue.FromNumber(current.NumberValue + value.NumberValue);
        }
        specification.ApplyValidated(operation.Path, value);
    }

    private static void ValidateOperationCategory(CharacterEditOperationKind kind, CharacterIdentityCategory category)
    {
        var valid = kind switch
        {
            CharacterEditOperationKind.SetPhysicalFact => category == CharacterIdentityCategory.PhysicalFacts,
            CharacterEditOperationKind.AdjustBodyCharacteristic => category == CharacterIdentityCategory.BodyIdentity,
            CharacterEditOperationKind.AdjustFaceCharacteristic => category == CharacterIdentityCategory.FaceIdentity,
            CharacterEditOperationKind.SetHairCharacteristic => category == CharacterIdentityCategory.HairIdentity,
            CharacterEditOperationKind.SetUniformCharacteristic => category == CharacterIdentityCategory.ClothingIdentity,
            CharacterEditOperationKind.SetAccessoryCharacteristic => category == CharacterIdentityCategory.AccessoryIdentity,
            CharacterEditOperationKind.SetAppearanceCharacteristic => category is CharacterIdentityCategory.BodyIdentity or
                CharacterIdentityCategory.FaceIdentity or CharacterIdentityCategory.SkinIdentity or
                CharacterIdentityCategory.EyeIdentity or CharacterIdentityCategory.HairIdentity or
                CharacterIdentityCategory.ClothingIdentity or CharacterIdentityCategory.AccessoryIdentity,
            _ => false
        };
        if (!valid) throw new ArgumentException($"Operation {kind} cannot edit category {category}.");
    }
}

public sealed class CharacterLockException(string message) : InvalidOperationException(message);

public sealed record CharacterEditTransaction(Guid TransactionId, string Label,
    CharacterSpecification Before, CharacterSpecification After,
    IReadOnlyList<CharacterIdentityCategory> AffectedCategories);

public sealed class CharacterEditHistory
{
    private readonly List<CharacterEditTransaction> _undo = [];
    private readonly List<CharacterEditTransaction> _redo = [];

    public CharacterEditHistory(CharacterSpecification specification) =>
        Current = (specification ?? throw new ArgumentNullException(nameof(specification))).Clone();

    public CharacterSpecification Current { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public IReadOnlyList<CharacterEditTransaction> Transactions => new ReadOnlyCollection<CharacterEditTransaction>(_undo);

    public CharacterEditResult Apply(CharacterEditPlan plan, string? label = null)
    {
        var result = CharacterEditService.Apply(Current, plan);
        if (!result.Validation.IsValid) return result;
        var before = Current.Clone();
        Current = result.Specification.Clone();
        _undo.Add(new CharacterEditTransaction(plan.TransactionId,
            string.IsNullOrWhiteSpace(label) ? plan.Explanation ?? "Character edit" : label.Trim(),
            before, Current.Clone(), plan.AffectedCategories));
        _redo.Clear();
        return result with { Specification = Current.Clone() };
    }

    public CharacterSpecification Commit(string label, Action<CharacterSpecification> mutation,
        params CharacterIdentityCategory[] categories)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A transaction label is required.", nameof(label));
        ArgumentNullException.ThrowIfNull(mutation);
        var before = Current.Clone();
        var after = Current.Clone();
        mutation(after);
        Current = after;
        _undo.Add(new CharacterEditTransaction(Guid.NewGuid(), label.Trim(), before, after.Clone(), categories));
        _redo.Clear();
        return Current.Clone();
    }

    public CharacterSpecification Undo()
    {
        if (!CanUndo) return Current.Clone();
        var transaction = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(transaction);
        Current = transaction.Before.Clone();
        return Current.Clone();
    }

    public CharacterSpecification Redo()
    {
        if (!CanRedo) return Current.Clone();
        var transaction = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(transaction);
        Current = transaction.After.Clone();
        return Current.Clone();
    }
}

public static class CharacterSpecificationAppearanceAdapter
{
    public static void Capture(PlayerAppearance appearance, CharacterSpecification specification)
    {
        specification.ApplyValidated(CharacterSemanticPaths.BodyFrame,
            CharacterPropertyValue.FromText(FromBuild(appearance.BodyBuild).ToString()));
        SetNumber(CharacterSemanticPaths.ShoulderWidth, appearance.ShoulderWidth);
        SetNumber(CharacterSemanticPaths.ChestWidth, appearance.ChestWidth);
        SetNumber(CharacterSemanticPaths.WaistWidth, appearance.WaistWidth);
        SetNumber(CharacterSemanticPaths.HipWidth, appearance.HipWidth);
        SetNumber(CharacterSemanticPaths.ArmLength, appearance.ArmLength);
        SetNumber(CharacterSemanticPaths.LegLength, appearance.LegLength);
        specification.ApplyValidated(CharacterSemanticPaths.SkinTone, CharacterPropertyValue.FromColor(appearance.SkinTone));
        specification.ApplyValidated(CharacterSemanticPaths.EyeColor, CharacterPropertyValue.FromColor(appearance.Face.EyeColor));
        specification.ApplyValidated(CharacterSemanticPaths.HairStyle, CharacterPropertyValue.FromText(appearance.Hair.Style.ToString()));
        SetNumber(CharacterSemanticPaths.HairLength, appearance.Hair.Length);
        SetNumber(CharacterSemanticPaths.HairVolume, appearance.Hair.Volume);
        SetNumber(CharacterSemanticPaths.HairlineHeight, appearance.Hair.HairlineHeight);
        SetNumber(CharacterSemanticPaths.HairPart, appearance.Hair.PartPosition);
        SetNumber(CharacterSemanticPaths.HairCurl, appearance.Hair.CurlAmount);
        specification.ApplyValidated(CharacterSemanticPaths.HairColor, CharacterPropertyValue.FromColor(appearance.Hair.Color));
        SetNumber(CharacterSemanticPaths.PonytailLength, appearance.Hair.PonytailLength);
        SetNumber(CharacterSemanticPaths.PonytailVolume, appearance.Hair.PonytailVolume);
        SetNumber(CharacterSemanticPaths.BunSize, appearance.Hair.BunSize);
        specification.ApplyValidated(CharacterSemanticPaths.ClothingPresentation, CharacterPropertyValue.FromText("TeamUniform"));
        specification.ApplyValidated(CharacterSemanticPaths.Accessories, CharacterPropertyValue.FromInteger((int)appearance.Accessories));
        CaptureFace(appearance.Face, specification);
        return;

        void SetNumber(string path, float value) =>
            specification.ApplyValidated(path, CharacterPropertyValue.FromNumber(value));
    }

    public static void Apply(CharacterSpecification specification, PlayerAppearance appearance)
    {
        if (specification.PlayerId != appearance.PlayerId) throw new ArgumentException("Character and appearance players differ.");
        appearance.SetHeight(specification.PhysicalFacts.HeightMeters);
        appearance.SetBodyBuild(ToBuild(Enum.Parse<BodyFrameDescription>(specification.Value(CharacterSemanticPaths.BodyFrame).TextValue, true)));
        appearance.SetBodyProportions(Number(CharacterSemanticPaths.ShoulderWidth), Number(CharacterSemanticPaths.ChestWidth),
            Number(CharacterSemanticPaths.WaistWidth), Number(CharacterSemanticPaths.HipWidth),
            Number(CharacterSemanticPaths.ArmLength), Number(CharacterSemanticPaths.LegLength));
        appearance.SetSkinTone(specification.Value(CharacterSemanticPaths.SkinTone).ColorValue);
        appearance.Face.SetEyeColor(specification.Value(CharacterSemanticPaths.EyeColor).ColorValue);
        appearance.Hair.SetParameters(
            Enum.Parse<HairStyle>(specification.Value(CharacterSemanticPaths.HairStyle).TextValue, true),
            Number(CharacterSemanticPaths.HairLength), Number(CharacterSemanticPaths.HairVolume),
            Number(CharacterSemanticPaths.HairlineHeight), Number(CharacterSemanticPaths.HairPart),
            Number(CharacterSemanticPaths.HairCurl), Number(CharacterSemanticPaths.PonytailLength),
            Number(CharacterSemanticPaths.PonytailVolume), Number(CharacterSemanticPaths.BunSize));
        appearance.Hair.SetColor(specification.Value(CharacterSemanticPaths.HairColor).ColorValue);
        appearance.SetAccessories((PlayerAccessories)specification.Value(CharacterSemanticPaths.Accessories).IntegerValue);
        ApplyFace(specification, appearance.Face);
        return;

        float Number(string path) => (float)specification.Value(path).NumberValue;
    }

    private static void CaptureFace(FaceAppearance face, CharacterSpecification specification)
    {
        var values = new Dictionary<string, float>
        {
            ["face.head_width"] = face.HeadWidth, ["face.head_height"] = face.HeadHeight,
            ["face.jaw_width"] = face.JawWidth, ["face.jaw_height"] = face.JawHeight,
            ["face.chin_width"] = face.ChinWidth, ["face.chin_projection"] = face.ChinProjection,
            ["face.cheekbone_width"] = face.CheekboneWidth, ["face.cheek_fullness"] = face.CheekFullness,
            ["face.forehead_height"] = face.ForeheadHeight, ["face.eye_spacing"] = face.EyeSpacing,
            ["face.eye_size"] = face.EyeSize, ["face.eye_vertical_position"] = face.EyeVerticalPosition,
            ["face.eyebrow_height"] = face.EyebrowHeight, ["face.nose_width"] = face.NoseWidth,
            ["face.nose_length"] = face.NoseLength, ["face.nose_projection"] = face.NoseProjection,
            ["face.mouth_width"] = face.MouthWidth, ["face.lip_fullness"] = face.LipFullness,
            ["face.ear_size"] = face.EarSize, ["face.ear_position"] = face.EarPosition
        };
        foreach (var pair in values) specification.ApplyValidated(pair.Key, CharacterPropertyValue.FromNumber(pair.Value));
    }

    private static void ApplyFace(CharacterSpecification specification, FaceAppearance face)
    {
        float N(string name) => (float)specification.Value($"face.{name}").NumberValue;
        face.SetParameters(N("head_width"), N("head_height"), N("jaw_width"), N("jaw_height"),
            N("chin_width"), N("chin_projection"), N("cheekbone_width"), N("cheek_fullness"),
            N("forehead_height"), N("eye_spacing"), N("eye_size"), N("eye_vertical_position"),
            N("eyebrow_height"), N("nose_width"), N("nose_length"), N("nose_projection"),
            N("mouth_width"), N("lip_fullness"), N("ear_size"), N("ear_position"));
    }

    private static BodyFrameDescription FromBuild(BodyBuild build) => build switch
    {
        BodyBuild.Slim => BodyFrameDescription.Lean,
        BodyBuild.Heavy => BodyFrameDescription.Stocky,
        BodyBuild.Average => BodyFrameDescription.Average,
        _ => BodyFrameDescription.Athletic
    };

    private static BodyBuild ToBuild(BodyFrameDescription description) => description switch
    {
        BodyFrameDescription.Lean => BodyBuild.Slim,
        BodyFrameDescription.Stocky => BodyBuild.Heavy,
        BodyFrameDescription.Average => BodyBuild.Average,
        BodyFrameDescription.Muscular => BodyBuild.Athletic,
        _ => BodyBuild.Athletic
    };
}
