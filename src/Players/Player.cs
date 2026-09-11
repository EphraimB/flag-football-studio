using System;

namespace FlagFootballStudio.Domain;

public sealed class Player
{
    public Player(Guid id, string name, int jerseyNumber, PlayerPosition position)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A player ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A player name is required.", nameof(name));
        if (jerseyNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Jersey numbers must be between 0 and 99.");

        Id = id;
        Name = name;
        JerseyNumber = jerseyNumber;
        Position = position;
    }

    public Guid Id { get; }
    public string Name { get; }
    public int JerseyNumber { get; private set; }
    public Team? Team { get; private set; }
    public PlayerPosition Position { get; }

    internal void AssignTo(Team team)
    {
        if (Team is not null && Team != team)
            throw new InvalidOperationException($"{Name} already belongs to {Team.Name}.");

        Team = team;
    }

    internal void SetJerseyNumber(int jerseyNumber)
    {
        if (jerseyNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Jersey numbers must be between 0 and 99.");
        JerseyNumber = jerseyNumber;
    }
}
