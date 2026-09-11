using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public sealed class GameProject
{
    private readonly List<PlayDefinition> _plays = [];
    private readonly ReadOnlyCollection<PlayDefinition> _readOnlyPlays;

    public GameProject(Guid id, string name, Team homeTeam, Team awayTeam)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A project ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A project name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
        AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
        if (HomeTeam.Id == AwayTeam.Id)
            throw new ArgumentException("Home and away teams must be different.", nameof(awayTeam));

        Possession = HomeTeam;
        Quarter = 1;
        GameClockSeconds = 10 * 60;
        Down = 1;
        Distance = 10;
        _readOnlyPlays = _plays.AsReadOnly();
    }

    public Guid Id { get; }
    public string Name { get; }
    public Team HomeTeam { get; }
    public Team AwayTeam { get; }
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }
    public int Quarter { get; private set; }
    public int GameClockSeconds { get; private set; }
    public int Down { get; private set; }
    public int Distance { get; private set; }
    public Team Possession { get; private set; }
    public IReadOnlyList<PlayDefinition> Plays => _readOnlyPlays;

    public void SetGameState(
        int homeScore,
        int awayScore,
        int quarter,
        int gameClockSeconds,
        int down,
        int distance,
        Guid possessionTeamId)
    {
        if (homeScore < 0 || awayScore < 0)
            throw new ArgumentOutOfRangeException(nameof(homeScore), "Scores cannot be negative.");
        if (quarter < 1)
            throw new ArgumentOutOfRangeException(nameof(quarter), "Quarter must be positive.");
        if (gameClockSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(gameClockSeconds), "Game clock cannot be negative.");
        if (down is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(down), "Down must be between 1 and 4.");
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance cannot be negative.");

        Possession = possessionTeamId == HomeTeam.Id
            ? HomeTeam
            : possessionTeamId == AwayTeam.Id
                ? AwayTeam
                : throw new ArgumentException("Possession must belong to the home or away team.", nameof(possessionTeamId));
        HomeScore = homeScore;
        AwayScore = awayScore;
        Quarter = quarter;
        GameClockSeconds = gameClockSeconds;
        Down = down;
        Distance = distance;
    }

    public void AddPlay(PlayDefinition play)
    {
        ArgumentNullException.ThrowIfNull(play);
        if (_plays.Any(existing => existing.Id == play.Id))
            throw new InvalidOperationException("The play is already in this project.");
        _plays.Add(play);
    }

    public bool RemovePlay(Guid playId)
    {
        var play = _plays.FirstOrDefault(candidate => candidate.Id == playId);
        return play is not null && _plays.Remove(play);
    }

    public void ReplacePlay(Guid playId, PlayDefinition replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var index = _plays.FindIndex(play => play.Id == playId);
        if (index < 0)
            throw new KeyNotFoundException("The play to replace is not in this project.");
        if (replacement.Id != playId)
            throw new ArgumentException("A replacement play must preserve the original play ID.", nameof(replacement));
        _plays[index] = replacement;
    }

    public PlayDefinition Play(Guid playId) =>
        _plays.FirstOrDefault(play => play.Id == playId)
        ?? throw new KeyNotFoundException("The requested play is not in this project.");

    public static GameProject CreatePrototype(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var project = new GameProject(Guid.NewGuid(), "Gold vs Navy", game.Gold, game.Navy);
        project.AddPlay(PlayDefinition.CreatePrototype(game));
        return project;
    }
}
