using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public sealed class GameProject
{
    private readonly List<PlayDefinition> _plays = [];
    private readonly List<CameraDefinition> _cameras = [];
    private readonly List<CameraCut> _cameraCuts = [];
    private readonly ReadOnlyCollection<PlayDefinition> _readOnlyPlays;
    private readonly ReadOnlyCollection<CameraDefinition> _readOnlyCameras;
    private readonly ReadOnlyCollection<CameraCut> _readOnlyCameraCuts;

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
        _readOnlyCameras = _cameras.AsReadOnly();
        _readOnlyCameraCuts = _cameraCuts.AsReadOnly();
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
    public IReadOnlyList<CameraDefinition> Cameras => _readOnlyCameras;
    public IReadOnlyList<CameraCut> CameraCuts => _readOnlyCameraCuts;

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
        if (play is null)
            return false;
        _cameraCuts.RemoveAll(cut => cut.PlayId == playId);
        return _plays.Remove(play);
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

    public void AddCamera(CameraDefinition camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (_cameras.Any(existing => existing.Id == camera.Id))
            throw new InvalidOperationException("The camera is already in this project.");
        _cameras.Add(camera);
    }

    public bool RemoveCamera(Guid cameraId)
    {
        var camera = _cameras.FirstOrDefault(candidate => candidate.Id == cameraId);
        if (camera is null)
            return false;
        _cameraCuts.RemoveAll(cut => cut.CameraId == cameraId);
        return _cameras.Remove(camera);
    }

    public CameraDefinition Camera(Guid cameraId) =>
        _cameras.FirstOrDefault(camera => camera.Id == cameraId)
        ?? throw new KeyNotFoundException("The requested camera is not in this project.");

    public void AddCameraCut(CameraCut cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        Play(cut.PlayId);
        Camera(cut.CameraId);
        if (_cameraCuts.Any(existing => existing.Id == cut.Id))
            throw new InvalidOperationException("The camera cut is already in this project.");
        _cameraCuts.Add(cut);
        _cameraCuts.Sort((left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
    }

    public bool RemoveCameraCut(Guid cutId)
    {
        var cut = _cameraCuts.FirstOrDefault(candidate => candidate.Id == cutId);
        return cut is not null && _cameraCuts.Remove(cut);
    }

    public IReadOnlyList<CameraCut> CameraCutsFor(Guid playId) =>
        _cameraCuts.Where(cut => cut.PlayId == playId).OrderBy(cut => cut.TimeSeconds).ToArray();

    public static GameProject CreatePrototype(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var project = new GameProject(Guid.NewGuid(), "Gold vs Navy", game.Gold, game.Navy);
        var play = PlayDefinition.CreatePrototype(game);
        project.AddPlay(play);
        var broadcast = new CameraDefinition(Guid.NewGuid(), "Broadcast Wide", CameraType.BroadcastWide);
        var sideline = new CameraDefinition(Guid.NewGuid(), "Sideline Low", CameraType.SidelineLow);
        var playerPov = new CameraDefinition(Guid.NewGuid(), "Quarterback POV", CameraType.PlayerPov);
        playerPov.SetPlayer(play.QuarterbackId);
        project.AddCamera(broadcast);
        project.AddCamera(sideline);
        project.AddCamera(playerPov);
        project.AddCameraCut(new CameraCut(Guid.NewGuid(), play.Id, broadcast.Id, 0));
        return project;
    }
}
