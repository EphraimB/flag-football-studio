using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlagFootballStudio.Domain;

public sealed class Team
{
    private readonly List<Player> _roster = [];
    private readonly ReadOnlyCollection<Player> _readOnlyRoster;

    public Team(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A team ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A team name is required.", nameof(name));

        Id = id;
        Name = name;
        _readOnlyRoster = _roster.AsReadOnly();
    }

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<Player> Roster => _readOnlyRoster;

    public void AddPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (_roster.Contains(player))
            return;
        if (_roster.Exists(existing => existing.JerseyNumber == player.JerseyNumber))
            throw new InvalidOperationException($"Jersey number {player.JerseyNumber} is already in use on {Name}.");

        player.AssignTo(this);
        _roster.Add(player);
    }
}
