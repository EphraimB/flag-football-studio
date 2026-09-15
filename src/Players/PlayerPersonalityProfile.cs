using System;
using System.Collections.Generic;

namespace FlagFootballStudio.Domain;

public sealed class PlayerPersonalityProfile
{
    public PlayerPersonalityProfile(Guid playerId, float confidence = 0.5f, float talkativeness = 0.5f,
        float competitiveness = 0.5f, float encouragement = 0.5f, float playfulness = 0.5f,
        float emotionalIntensity = 0.5f, float calmness = 0.5f, float leadership = 0.5f)
    {
        if (playerId == Guid.Empty) throw new ArgumentException("A player ID is required.", nameof(playerId));
        PlayerId = playerId;
        SetTraits(confidence, talkativeness, competitiveness, encouragement, playfulness,
            emotionalIntensity, calmness, leadership);
    }

    public Guid PlayerId { get; }
    public float Confidence { get; private set; }
    public float Talkativeness { get; private set; }
    public float Competitiveness { get; private set; }
    public float Encouragement { get; private set; }
    public float Playfulness { get; private set; }
    public float EmotionalIntensity { get; private set; }
    public float Calmness { get; private set; }
    public float Leadership { get; private set; }

    public IReadOnlyList<string> DerivedStyleLabels
    {
        get
        {
            var labels = new List<string>();
            if (Talkativeness <= 0.3f) labels.Add("Quiet");
            if (Encouragement >= 0.7f) labels.Add("Supportive");
            if (Calmness >= 0.7f && Leadership >= 0.55f) labels.Add("Tactical");
            if (EmotionalIntensity >= 0.7f || Playfulness >= 0.75f) labels.Add("Energetic");
            if (Competitiveness >= 0.7f) labels.Add("Competitive");
            if (labels.Count == 0) labels.Add("Balanced");
            return labels.AsReadOnly();
        }
    }

    public void SetTraits(float confidence, float talkativeness, float competitiveness,
        float encouragement, float playfulness, float emotionalIntensity, float calmness, float leadership)
    {
        Confidence = Trait(confidence, nameof(confidence));
        Talkativeness = Trait(talkativeness, nameof(talkativeness));
        Competitiveness = Trait(competitiveness, nameof(competitiveness));
        Encouragement = Trait(encouragement, nameof(encouragement));
        Playfulness = Trait(playfulness, nameof(playfulness));
        EmotionalIntensity = Trait(emotionalIntensity, nameof(emotionalIntensity));
        Calmness = Trait(calmness, nameof(calmness));
        Leadership = Trait(leadership, nameof(leadership));
    }

    public PlayerPersonalityProfile Clone() => new(PlayerId, Confidence, Talkativeness,
        Competitiveness, Encouragement, Playfulness, EmotionalIntensity, Calmness, Leadership);

    private static float Trait(float value, string name)
    {
        if (!float.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, "Personality traits must be normalized from 0 through 1.");
        return value;
    }
}

/// <summary>Presentation-only interpretation. It has no access to plays, outcomes, or simulator state.</summary>
public readonly record struct PersonalityPresentationProfile(
    float AmbientSpeechFrequency,
    float BodyLanguageConfidence,
    float CelebrationIntensity,
    float FrustrationIntensity,
    float DeliveryIntensity,
    float EncouragementBias);

public static class PersonalityPresentationMapper
{
    public static PersonalityPresentationProfile Map(PlayerPersonalityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new PersonalityPresentationProfile(
            0.35f + profile.Talkativeness * 0.9f,
            profile.Confidence,
            Math.Clamp(profile.EmotionalIntensity * 0.65f + profile.Competitiveness * 0.35f, 0, 1),
            Math.Clamp(profile.EmotionalIntensity * (1f - profile.Calmness), 0, 1),
            Math.Clamp(profile.Confidence * 0.45f + profile.EmotionalIntensity * 0.55f, 0, 1),
            profile.Encouragement);
    }
}
