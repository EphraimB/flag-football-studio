using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public readonly record struct PlayPoint(float X, float Y);

public sealed class PlayDefinition
{
    private readonly HashSet<Guid> _playerIds;
    private readonly Dictionary<Guid, PlayPoint> _startingPositions = [];
    private readonly Dictionary<Guid, IReadOnlyList<PlayPoint>> _routes = [];
    private readonly Dictionary<Guid, Guid> _coverageAssignments = [];
    private readonly ReadOnlyDictionary<Guid, PlayPoint> _readOnlyStartingPositions;
    private readonly ReadOnlyDictionary<Guid, IReadOnlyList<PlayPoint>> _readOnlyRoutes;
    private readonly ReadOnlyDictionary<Guid, Guid> _readOnlyCoverageAssignments;

    public PlayDefinition(IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        _playerIds = new HashSet<Guid>(playerIds);
        if (_playerIds.Count == 0)
            throw new ArgumentException("A play requires at least one player.", nameof(playerIds));

        _readOnlyStartingPositions = new ReadOnlyDictionary<Guid, PlayPoint>(_startingPositions);
        _readOnlyRoutes = new ReadOnlyDictionary<Guid, IReadOnlyList<PlayPoint>>(_routes);
        _readOnlyCoverageAssignments = new ReadOnlyDictionary<Guid, Guid>(_coverageAssignments);
    }

    public IReadOnlyDictionary<Guid, PlayPoint> StartingPositions => _readOnlyStartingPositions;
    public IReadOnlyDictionary<Guid, IReadOnlyList<PlayPoint>> Routes => _readOnlyRoutes;
    public IReadOnlyDictionary<Guid, Guid> CoverageAssignments => _readOnlyCoverageAssignments;
    public Guid QuarterbackId { get; private set; }
    public Guid IntendedReceiverId { get; private set; }

    public void SetStartingPosition(Guid playerId, PlayPoint position)
    {
        ValidatePlayer(playerId);
        _startingPositions[playerId] = position;
    }

    public void SetRoute(Guid playerId, IEnumerable<PlayPoint> waypoints)
    {
        ValidatePlayer(playerId);
        ArgumentNullException.ThrowIfNull(waypoints);
        _routes[playerId] = Array.AsReadOnly(waypoints.ToArray());
    }

    public void AssignCoverage(Guid defenderId, Guid offensivePlayerId)
    {
        ValidatePlayer(defenderId);
        ValidatePlayer(offensivePlayerId);
        _coverageAssignments[defenderId] = offensivePlayerId;
    }

    public void SetQuarterback(Guid playerId)
    {
        ValidatePlayer(playerId);
        QuarterbackId = playerId;
    }

    public void SetIntendedReceiver(Guid playerId)
    {
        ValidatePlayer(playerId);
        IntendedReceiverId = playerId;
    }

    public static PlayDefinition CreatePrototype(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var players = game.Gold.Roster.Concat(game.Navy.Roster).ToArray();
        var play = new PlayDefinition(players.Select(player => player.Id));

        var goldPositions = new[]
        {
            new PlayPoint(0, -2),
            new PlayPoint(0, -5),
            new PlayPoint(-6, -2),
            new PlayPoint(6, -2),
            new PlayPoint(3, -5)
        };
        var navyPositions = new[]
        {
            new PlayPoint(-6, 1),
            new PlayPoint(-3, 1),
            new PlayPoint(0, 1),
            new PlayPoint(3, 1),
            new PlayPoint(6, 1)
        };

        for (var index = 0; index < game.Gold.Roster.Count; index++)
            play.SetStartingPosition(game.Gold.Roster[index].Id, goldPositions[index]);
        for (var index = 0; index < game.Navy.Roster.Count; index++)
            play.SetStartingPosition(game.Navy.Roster[index].Id, navyPositions[index]);

        var quarterback = game.Gold.Roster.Single(player => player.Position == PlayerPosition.Quarterback);
        var receiver = game.Gold.Roster.Single(player => player.Position == PlayerPosition.Receiver);
        play.SetQuarterback(quarterback.Id);
        play.SetIntendedReceiver(receiver.Id);
        play.SetRoute(receiver.Id, [new PlayPoint(-6, 2), new PlayPoint(-4.5f, 6.5f)]);
        play.AssignCoverage(game.Navy.Roster[0].Id, receiver.Id);
        return play;
    }

    private void ValidatePlayer(Guid playerId)
    {
        if (!_playerIds.Contains(playerId))
            throw new ArgumentException("The player is not part of this play.", nameof(playerId));
    }
}
