using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Persistence;

public sealed class ProjectJsonSerializer
{
    private const int CurrentFormatVersion = 1;
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SerializePlay(PlayDefinition play)
    {
        ArgumentNullException.ThrowIfNull(play);
        return JsonSerializer.Serialize(PlayData.FromDomain(play), _options);
    }

    public PlayDefinition DeserializePlay(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Play JSON is required.", nameof(json));
        var data = JsonSerializer.Deserialize<PlayData>(json, _options)
            ?? throw new JsonException("The play file was empty.");
        ValidateVersion(data.FormatVersion);
        return data.ToDomain();
    }

    public string SerializeProject(GameProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonSerializer.Serialize(GameProjectData.FromDomain(project), _options);
    }

    public GameProject DeserializeProject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Project JSON is required.", nameof(json));
        var data = JsonSerializer.Deserialize<GameProjectData>(json, _options)
            ?? throw new JsonException("The game project file was empty.");
        ValidateVersion(data.FormatVersion);
        return data.ToDomain();
    }

    private static void ValidateVersion(int version)
    {
        if (version != CurrentFormatVersion)
            throw new JsonException($"Unsupported project format version {version}.");
    }

    private sealed class GameProjectData
    {
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TeamData HomeTeam { get; set; } = new();
        public TeamData AwayTeam { get; set; } = new();
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int Quarter { get; set; }
        public int GameClockSeconds { get; set; }
        public int Down { get; set; }
        public int Distance { get; set; }
        public Guid PossessionTeamId { get; set; }
        public List<PlayData> Plays { get; set; } = [];
        public List<CameraData> Cameras { get; set; } = [];
        public List<CameraCutData> CameraCuts { get; set; } = [];

        public static GameProjectData FromDomain(GameProject project) => new()
        {
            Id = project.Id,
            Name = project.Name,
            HomeTeam = TeamData.FromDomain(project.HomeTeam),
            AwayTeam = TeamData.FromDomain(project.AwayTeam),
            HomeScore = project.HomeScore,
            AwayScore = project.AwayScore,
            Quarter = project.Quarter,
            GameClockSeconds = project.GameClockSeconds,
            Down = project.Down,
            Distance = project.Distance,
            PossessionTeamId = project.Possession.Id,
            Plays = project.Plays.Select(PlayData.FromDomain).ToList(),
            Cameras = project.Cameras.Select(CameraData.FromDomain).ToList(),
            CameraCuts = project.CameraCuts.Select(CameraCutData.FromDomain).ToList()
        };

        public GameProject ToDomain()
        {
            var project = new GameProject(Id, Name, HomeTeam.ToDomain(), AwayTeam.ToDomain());
            project.SetGameState(HomeScore, AwayScore, Quarter, GameClockSeconds, Down, Distance, PossessionTeamId);
            foreach (var play in Plays)
                project.AddPlay(play.ToDomain());
            foreach (var camera in Cameras)
                project.AddCamera(camera.ToDomain());
            foreach (var cut in CameraCuts)
                project.AddCameraCut(cut.ToDomain());
            return project;
        }
    }

    private sealed class CameraData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CameraType Type { get; set; }
        public CameraVector Position { get; set; }
        public CameraVector RotationDegrees { get; set; }
        public float FieldOfView { get; set; }
        public Guid? PlayerId { get; set; }

        public static CameraData FromDomain(CameraDefinition camera) => new()
        {
            Id = camera.Id,
            Name = camera.Name,
            Type = camera.Type,
            Position = camera.Position,
            RotationDegrees = camera.RotationDegrees,
            FieldOfView = camera.FieldOfView,
            PlayerId = camera.PlayerId
        };

        public CameraDefinition ToDomain()
        {
            var camera = new CameraDefinition(Id, Name, Type);
            camera.SetFreeCamera(Position, RotationDegrees, FieldOfView);
            camera.SetPlayer(PlayerId);
            return camera;
        }
    }

    private sealed class CameraCutData
    {
        public Guid Id { get; set; }
        public Guid PlayId { get; set; }
        public Guid CameraId { get; set; }
        public double TimeSeconds { get; set; }

        public static CameraCutData FromDomain(CameraCut cut) => new()
        {
            Id = cut.Id,
            PlayId = cut.PlayId,
            CameraId = cut.CameraId,
            TimeSeconds = cut.TimeSeconds
        };

        public CameraCut ToDomain() => new(Id, PlayId, CameraId, TimeSeconds);
    }

    private sealed class TeamData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<PlayerData> Roster { get; set; } = [];

        public static TeamData FromDomain(Team team) => new()
        {
            Id = team.Id,
            Name = team.Name,
            Roster = team.Roster.Select(PlayerData.FromDomain).ToList()
        };

        public Team ToDomain()
        {
            var team = new Team(Id, Name);
            foreach (var player in Roster)
                team.AddPlayer(player.ToDomain());
            return team;
        }
    }

    private sealed class PlayerData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int JerseyNumber { get; set; }
        public PlayerPosition Position { get; set; }

        public static PlayerData FromDomain(Player player) => new()
        {
            Id = player.Id,
            Name = player.Name,
            JerseyNumber = player.JerseyNumber,
            Position = player.Position
        };

        public Player ToDomain() => new(Id, Name, JerseyNumber, Position);
    }

    private sealed class PlayData
    {
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Guid> PlayerIds { get; set; } = [];
        public Dictionary<Guid, PlayPoint> StartingPositions { get; set; } = [];
        public Dictionary<Guid, List<PlayPoint>> Routes { get; set; } = [];
        public Dictionary<Guid, Guid> CoverageAssignments { get; set; } = [];
        public Guid QuarterbackId { get; set; }
        public Guid IntendedReceiverId { get; set; }

        public static PlayData FromDomain(PlayDefinition play) => new()
        {
            Id = play.Id,
            Name = play.Name,
            PlayerIds = play.PlayerIds.ToList(),
            StartingPositions = play.StartingPositions.ToDictionary(entry => entry.Key, entry => entry.Value),
            Routes = play.Routes.ToDictionary(entry => entry.Key, entry => entry.Value.ToList()),
            CoverageAssignments = play.CoverageAssignments.ToDictionary(entry => entry.Key, entry => entry.Value),
            QuarterbackId = play.QuarterbackId,
            IntendedReceiverId = play.IntendedReceiverId
        };

        public PlayDefinition ToDomain()
        {
            ValidateVersion(FormatVersion);
            var play = new PlayDefinition(PlayerIds, Name, Id);
            foreach (var position in StartingPositions)
                play.SetStartingPosition(position.Key, position.Value);
            foreach (var route in Routes)
                play.SetRoute(route.Key, route.Value);
            foreach (var assignment in CoverageAssignments)
                play.AssignCoverage(assignment.Key, assignment.Value);
            if (QuarterbackId != Guid.Empty)
                play.SetQuarterback(QuarterbackId);
            if (IntendedReceiverId != Guid.Empty)
                play.SetIntendedReceiver(IntendedReceiverId);
            return play;
        }
    }
}
