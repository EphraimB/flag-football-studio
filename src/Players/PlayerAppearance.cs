using System;

namespace FlagFootballStudio.Domain;

public enum BodyBuild
{
    Slim,
    Athletic,
    Stocky
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

    public void SetBodyBuild(BodyBuild bodyBuild) => BodyBuild = bodyBuild;
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
}
