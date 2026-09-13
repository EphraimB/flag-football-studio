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
    private readonly Dictionary<Guid, PlayerAppearance> _playerAppearances = [];
    private readonly List<UniformDefinition> _uniforms = [];
    private readonly Dictionary<Guid, Guid> _activeUniformIds = [];
    private readonly List<DialogueSequence> _dialogueSequences = [];
    private readonly ReadOnlyCollection<PlayDefinition> _readOnlyPlays;
    private readonly ReadOnlyCollection<CameraDefinition> _readOnlyCameras;
    private readonly ReadOnlyCollection<CameraCut> _readOnlyCameraCuts;
    private readonly ReadOnlyDictionary<Guid, PlayerAppearance> _readOnlyPlayerAppearances;
    private readonly ReadOnlyCollection<UniformDefinition> _readOnlyUniforms;
    private readonly ReadOnlyDictionary<Guid, Guid> _readOnlyActiveUniformIds;
    private readonly ReadOnlyCollection<DialogueSequence> _readOnlyDialogueSequences;

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
        _readOnlyPlayerAppearances = new ReadOnlyDictionary<Guid, PlayerAppearance>(_playerAppearances);
        _readOnlyUniforms = _uniforms.AsReadOnly();
        _readOnlyActiveUniformIds = new ReadOnlyDictionary<Guid, Guid>(_activeUniformIds);
        _readOnlyDialogueSequences = _dialogueSequences.AsReadOnly();
        foreach (var player in HomeTeam.Roster)
            _playerAppearances[player.Id] = CreateDefaultAppearance(player, true);
        foreach (var player in AwayTeam.Roster)
            _playerAppearances[player.Id] = CreateDefaultAppearance(player, false);
        var homeUniform = UniformDefinition.CreateTeamDefault(HomeTeam, true);
        var awayUniform = UniformDefinition.CreateTeamDefault(AwayTeam, false);
        AddUniform(homeUniform);
        AddUniform(awayUniform);
        SetActiveUniform(HomeTeam.Id, homeUniform.Id);
        SetActiveUniform(AwayTeam.Id, awayUniform.Id);
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
    public IReadOnlyDictionary<Guid, PlayerAppearance> PlayerAppearances => _readOnlyPlayerAppearances;
    public IReadOnlyList<UniformDefinition> Uniforms => _readOnlyUniforms;
    public IReadOnlyDictionary<Guid, Guid> ActiveUniformIds => _readOnlyActiveUniformIds;
    public IReadOnlyList<DialogueSequence> DialogueSequences => _readOnlyDialogueSequences;

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
        _dialogueSequences.RemoveAll(sequence => sequence.PlayId == playId);
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

    public void AddDialogueSequence(DialogueSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (sequence.PlayId.HasValue) Play(sequence.PlayId.Value);
        if (_dialogueSequences.Any(existing => existing.Id == sequence.Id))
            throw new InvalidOperationException("The dialogue sequence is already in this project.");
        var rosterIds = HomeTeam.Roster.Concat(AwayTeam.Roster).Select(player => player.Id).ToHashSet();
        foreach (var line in sequence.Lines)
            if (!rosterIds.Contains(line.SpeakerPlayerId))
                throw new ArgumentException("A dialogue speaker is not in this project.", nameof(sequence));
        _dialogueSequences.Add(sequence);
    }

    public bool RemoveDialogueSequence(Guid sequenceId) =>
        _dialogueSequences.RemoveAll(sequence => sequence.Id == sequenceId) > 0;

    public DialogueSequence DialogueSequence(Guid sequenceId) =>
        _dialogueSequences.FirstOrDefault(sequence => sequence.Id == sequenceId)
        ?? throw new KeyNotFoundException("The requested dialogue sequence is not in this project.");

    public IReadOnlyList<DialogueSequence> DialogueForPlay(Guid playId) =>
        _dialogueSequences.Where(sequence => sequence.PlayId == playId).ToArray();

    public PlayerAppearance AppearanceFor(Guid playerId) =>
        _playerAppearances.TryGetValue(playerId, out var appearance)
            ? appearance
            : throw new KeyNotFoundException("The requested player appearance is not in this project.");

    public void SetPlayerAppearance(PlayerAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var belongsToProject = HomeTeam.Roster.Concat(AwayTeam.Roster).Any(player => player.Id == appearance.PlayerId);
        if (!belongsToProject)
            throw new ArgumentException("The appearance player is not in this project.", nameof(appearance));
        _playerAppearances[appearance.PlayerId] = appearance;
    }

    public void AddUniform(UniformDefinition uniform)
    {
        ArgumentNullException.ThrowIfNull(uniform);
        ValidateTeam(uniform.TeamId);
        if (_uniforms.Any(existing => existing.Id == uniform.Id))
            throw new InvalidOperationException("The uniform is already in this project.");
        _uniforms.Add(uniform);
    }

    public bool RemoveUniform(Guid uniformId)
    {
        var uniform = _uniforms.FirstOrDefault(candidate => candidate.Id == uniformId);
        if (uniform is null)
            return false;
        var teamUniforms = UniformsFor(uniform.TeamId);
        if (teamUniforms.Count == 1)
            throw new InvalidOperationException("A team must keep at least one uniform.");
        _uniforms.Remove(uniform);
        if (_activeUniformIds.TryGetValue(uniform.TeamId, out var activeId) && activeId == uniformId)
            _activeUniformIds[uniform.TeamId] = UniformsFor(uniform.TeamId)[0].Id;
        return true;
    }

    public UniformDefinition Uniform(Guid uniformId) =>
        _uniforms.FirstOrDefault(uniform => uniform.Id == uniformId)
        ?? throw new KeyNotFoundException("The requested uniform is not in this project.");

    public IReadOnlyList<UniformDefinition> UniformsFor(Guid teamId)
    {
        ValidateTeam(teamId);
        return _uniforms.Where(uniform => uniform.TeamId == teamId).ToArray();
    }

    public void SetActiveUniform(Guid teamId, Guid uniformId)
    {
        ValidateTeam(teamId);
        var uniform = Uniform(uniformId);
        if (uniform.TeamId != teamId)
            throw new ArgumentException("The uniform belongs to a different team.", nameof(uniformId));
        _activeUniformIds[teamId] = uniformId;
    }

    public UniformDefinition ActiveUniformFor(Guid teamId)
    {
        ValidateTeam(teamId);
        return _activeUniformIds.TryGetValue(teamId, out var uniformId)
            ? Uniform(uniformId)
            : throw new InvalidOperationException("The team has no active uniform.");
    }

    public void ReplaceUniformLibrary(IEnumerable<UniformDefinition> uniforms, IReadOnlyDictionary<Guid, Guid> activeUniformIds)
    {
        ArgumentNullException.ThrowIfNull(uniforms);
        ArgumentNullException.ThrowIfNull(activeUniformIds);
        var replacements = uniforms.ToArray();
        if (!replacements.Any(uniform => uniform.TeamId == HomeTeam.Id) ||
            !replacements.Any(uniform => uniform.TeamId == AwayTeam.Id))
            throw new ArgumentException("Each team must have at least one uniform.", nameof(uniforms));

        _uniforms.Clear();
        _activeUniformIds.Clear();
        foreach (var uniform in replacements)
            AddUniform(uniform);
        SetActiveUniform(HomeTeam.Id, activeUniformIds[HomeTeam.Id]);
        SetActiveUniform(AwayTeam.Id, activeUniformIds[AwayTeam.Id]);
    }

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

    private static PlayerAppearance CreateDefaultAppearance(Player player, bool home)
    {
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);
        appearance.SetUniformColors(
            home ? new AppearanceColor(216, 169, 27) : new AppearanceColor(23, 59, 115),
            new AppearanceColor(245, 245, 240));
        return appearance;
    }

    private void ValidateTeam(Guid teamId)
    {
        if (teamId != HomeTeam.Id && teamId != AwayTeam.Id)
            throw new ArgumentException("The team is not in this project.", nameof(teamId));
    }
}
