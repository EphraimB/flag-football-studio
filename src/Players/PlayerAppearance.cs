using System;

namespace FlagFootballStudio.Domain;

public enum BodyBuild
{
    Slim = 0,
    Athletic = 1,
    Heavy = 2,
    Average = 3,
    [Obsolete("Use Heavy. This alias remains for older project files.")]
    Stocky = Heavy
}

public enum HairStyle
{
    None,
    Short,
    Curly,
    Mohawk,
    Bun
}

[Flags]
public enum PlayerAccessories
{
    None = 0,
    Headband = 1,
    Wristbands = 2,
    Visor = 4,
    ArmSleeves = 8
}

public readonly record struct AppearanceColor(byte R, byte G, byte B);

public sealed class PlayerAppearance
{
    public PlayerAppearance(Guid playerId, int jerseyNumber)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("An appearance requires a player ID.", nameof(playerId));
        PlayerId = playerId;
        SetJerseyNumber(jerseyNumber);
        HeightMeters = 1.8f;
        BodyBuild = BodyBuild.Athletic;
        ShoulderWidth = 1f;
        ChestWidth = 1f;
        WaistWidth = 1f;
        HipWidth = 1f;
        ArmLength = 1f;
        LegLength = 1f;
        SkinTone = new AppearanceColor(214, 167, 122);
        HairStyle = HairStyle.Short;
        HairColor = new AppearanceColor(48, 31, 24);
        PrimaryUniformColor = new AppearanceColor(216, 169, 27);
        SecondaryUniformColor = new AppearanceColor(245, 245, 240);
        FlagColor = new AppearanceColor(255, 79, 100);
    }

    public Guid PlayerId { get; }
    public float HeightMeters { get; private set; }
    public BodyBuild BodyBuild { get; private set; }
    public float ShoulderWidth { get; private set; }
    public float ChestWidth { get; private set; }
    public float WaistWidth { get; private set; }
    public float HipWidth { get; private set; }
    public float ArmLength { get; private set; }
    public float LegLength { get; private set; }
    public AppearanceColor SkinTone { get; private set; }
    public HairStyle HairStyle { get; private set; }
    public AppearanceColor HairColor { get; private set; }
    public int JerseyNumber { get; private set; }
    public AppearanceColor PrimaryUniformColor { get; private set; }
    public AppearanceColor SecondaryUniformColor { get; private set; }
    public AppearanceColor FlagColor { get; private set; }
    public PlayerAccessories Accessories { get; private set; }

    public void SetHeight(float heightMeters)
    {
        if (heightMeters is < 1.4f or > 2.3f)
            throw new ArgumentOutOfRangeException(nameof(heightMeters), "Height must be between 1.4 and 2.3 meters.");
        HeightMeters = heightMeters;
    }

    public void SetBodyBuild(BodyBuild bodyBuild)
    {
        if (bodyBuild is not (BodyBuild.Slim or BodyBuild.Average or BodyBuild.Athletic or BodyBuild.Heavy))
            throw new ArgumentOutOfRangeException(nameof(bodyBuild));
        BodyBuild = bodyBuild;
    }

    public void SetBodyProportions(
        float shoulderWidth,
        float chestWidth,
        float waistWidth,
        float hipWidth,
        float armLength,
        float legLength)
    {
        ShoulderWidth = ValidateRatio(shoulderWidth, nameof(shoulderWidth));
        ChestWidth = ValidateRatio(chestWidth, nameof(chestWidth));
        WaistWidth = ValidateRatio(waistWidth, nameof(waistWidth));
        HipWidth = ValidateRatio(hipWidth, nameof(hipWidth));
        ArmLength = ValidateLengthRatio(armLength, nameof(armLength));
        LegLength = ValidateLengthRatio(legLength, nameof(legLength));
    }
    public void SetSkinTone(AppearanceColor color) => SkinTone = color;
    public void SetHair(HairStyle style, AppearanceColor color) { HairStyle = style; HairColor = color; }

    public void SetJerseyNumber(int jerseyNumber)
    {
        if (jerseyNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Jersey numbers must be between 0 and 99.");
        JerseyNumber = jerseyNumber;
    }

    public void SetUniformColors(AppearanceColor primary, AppearanceColor secondary)
    {
        PrimaryUniformColor = primary;
        SecondaryUniformColor = secondary;
    }

    public void SetFlagColor(AppearanceColor color) => FlagColor = color;
    public void SetAccessories(PlayerAccessories accessories) => Accessories = accessories;

    private static float ValidateRatio(float value, string parameterName)
    {
        if (value is < 0.7f or > 1.3f)
            throw new ArgumentOutOfRangeException(parameterName, "Body width proportions must be between 0.7 and 1.3.");
        return value;
    }

    private static float ValidateLengthRatio(float value, string parameterName)
    {
        if (value is < 0.75f or > 1.25f)
            throw new ArgumentOutOfRangeException(parameterName, "Limb length proportions must be between 0.75 and 1.25.");
        return value;
    }
}
