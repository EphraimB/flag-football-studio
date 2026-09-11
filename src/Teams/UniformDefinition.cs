using System;

namespace FlagFootballStudio.Domain;

public enum UniformDesignation
{
    Home,
    Away
}

public sealed class UniformDefinition
{
    public UniformDefinition(Guid id, Guid teamId, string name, UniformDesignation designation)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A uniform ID cannot be empty.", nameof(id));
        if (teamId == Guid.Empty)
            throw new ArgumentException("A uniform requires a team ID.", nameof(teamId));
        Id = id;
        TeamId = teamId;
        Name = ValidateText(name, "Uniform name");
        Designation = designation;
        PrimaryColor = new AppearanceColor(32, 64, 112);
        SecondaryColor = new AppearanceColor(245, 245, 240);
        AccentColor = new AppearanceColor(220, 175, 35);
        JerseyBaseColor = PrimaryColor;
        SleeveTrimColor = AccentColor;
        CollarTrimColor = SecondaryColor;
        NumberColor = SecondaryColor;
        NumberOutlineColor = new AppearanceColor(15, 20, 30);
        ShortsColor = PrimaryColor;
        FlagColor = new AppearanceColor(255, 79, 100);
        TeamWordmark = "TEAM";
    }

    public Guid Id { get; }
    public Guid TeamId { get; }
    public string Name { get; private set; }
    public AppearanceColor PrimaryColor { get; private set; }
    public AppearanceColor SecondaryColor { get; private set; }
    public AppearanceColor AccentColor { get; private set; }
    public AppearanceColor JerseyBaseColor { get; private set; }
    public AppearanceColor SleeveTrimColor { get; private set; }
    public AppearanceColor CollarTrimColor { get; private set; }
    public AppearanceColor NumberColor { get; private set; }
    public AppearanceColor NumberOutlineColor { get; private set; }
    public AppearanceColor ShortsColor { get; private set; }
    public AppearanceColor FlagColor { get; private set; }
    public string TeamWordmark { get; private set; }
    public bool ShowPlayerNameOnBack { get; private set; }
    public UniformDesignation Designation { get; private set; }

    public void Rename(string name) => Name = ValidateText(name, "Uniform name");
    public void SetDesignation(UniformDesignation designation) => Designation = designation;
    public void SetPrimaryColors(AppearanceColor primary, AppearanceColor secondary, AppearanceColor accent)
        => (PrimaryColor, SecondaryColor, AccentColor) = (primary, secondary, accent);
    public void SetJerseyColors(AppearanceColor jerseyBase, AppearanceColor sleeveTrim, AppearanceColor collarTrim)
        => (JerseyBaseColor, SleeveTrimColor, CollarTrimColor) = (jerseyBase, sleeveTrim, collarTrim);
    public void SetNumberColors(AppearanceColor number, AppearanceColor outline)
        => (NumberColor, NumberOutlineColor) = (number, outline);
    public void SetShortsColor(AppearanceColor color) => ShortsColor = color;
    public void SetFlagColor(AppearanceColor color) => FlagColor = color;
    public void SetWordmark(string wordmark) => TeamWordmark = ValidateText(wordmark, "Team wordmark");
    public void SetShowPlayerNameOnBack(bool show) => ShowPlayerNameOnBack = show;

    public UniformDefinition Duplicate(string name)
    {
        var copy = new UniformDefinition(Guid.NewGuid(), TeamId, name, Designation);
        copy.SetPrimaryColors(PrimaryColor, SecondaryColor, AccentColor);
        copy.SetJerseyColors(JerseyBaseColor, SleeveTrimColor, CollarTrimColor);
        copy.SetNumberColors(NumberColor, NumberOutlineColor);
        copy.SetShortsColor(ShortsColor);
        copy.SetFlagColor(FlagColor);
        copy.SetWordmark(TeamWordmark);
        copy.SetShowPlayerNameOnBack(ShowPlayerNameOnBack);
        return copy;
    }

    public static UniformDefinition CreateTeamDefault(Team team, bool home)
    {
        ArgumentNullException.ThrowIfNull(team);
        var gold = new AppearanceColor(216, 169, 27);
        var navy = new AppearanceColor(23, 59, 115);
        var white = new AppearanceColor(245, 245, 240);
        var teamColor = team.Name.Equals("Gold", StringComparison.OrdinalIgnoreCase) ? gold : navy;
        var uniform = new UniformDefinition(Guid.NewGuid(), team.Id, home ? "Home" : "Away", home ? UniformDesignation.Home : UniformDesignation.Away);
        uniform.SetPrimaryColors(teamColor, white, home ? navy : gold);
        uniform.SetJerseyColors(home ? teamColor : white, home ? navy : teamColor, home ? white : teamColor);
        uniform.SetNumberColors(home ? white : teamColor, home ? navy : white);
        uniform.SetShortsColor(home ? teamColor : white);
        uniform.SetFlagColor(home ? new AppearanceColor(255, 79, 100) : gold);
        uniform.SetWordmark(team.Name.ToUpperInvariant());
        uniform.SetShowPlayerNameOnBack(true);
        return uniform;
    }

    private static string ValidateText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} is required.", nameof(value));
        return value.Trim();
    }
}
