using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlagFootballStudio.Domain;

public enum CharacterDirectorInputKind { Text, SpeechReference, SelectedRegion, ReferenceImage }

public sealed record CharacterDirectorInput(CharacterDirectorInputKind Kind, string ContentReference);

public sealed record PersonalityEditValues(
    float? Confidence = null,
    float? Talkativeness = null,
    float? Competitiveness = null,
    float? Encouragement = null,
    float? Playfulness = null,
    float? EmotionalIntensity = null,
    float? Calmness = null,
    float? Leadership = null);

public sealed record PlayerPersonalityEditPlan(Guid TransactionId, PersonalityEditValues Values, string? Explanation = null);

public sealed record CharacterDirectorRequest(
    Guid PlayerId,
    IReadOnlyList<CharacterDirectorInput> Inputs,
    CharacterSpecification CurrentSpecification,
    PlayerPersonalityProfile CurrentPersonality);

public sealed record CharacterDirectionProposal(
    CharacterEditPlan CharacterEdits,
    PlayerPersonalityEditPlan? PersonalityEdits,
    string Status,
    bool RequiresUserConfirmation = true);

/// <summary>
/// Provider-independent future boundary. Implementations return inspectable plans and never receive Godot nodes.
/// No implementation is registered in the current milestone.
/// </summary>
public interface ILocalCharacterDirector
{
    Task<CharacterDirectionProposal> ProposeAsync(CharacterDirectorRequest request, CancellationToken cancellationToken);
}
