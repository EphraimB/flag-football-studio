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
        public List<PlayerAppearanceData> PlayerAppearances { get; set; } = [];
        public List<UniformData> Uniforms { get; set; } = [];
        public Dictionary<Guid, Guid> ActiveUniformIds { get; set; } = [];

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
            CameraCuts = project.CameraCuts.Select(CameraCutData.FromDomain).ToList(),
            PlayerAppearances = project.PlayerAppearances.Values.Select(PlayerAppearanceData.FromDomain).ToList(),
            Uniforms = project.Uniforms.Select(UniformData.FromDomain).ToList(),
            ActiveUniformIds = project.ActiveUniformIds.ToDictionary(entry => entry.Key, entry => entry.Value)
        };

        public GameProject ToDomain()
        {
            var project = new GameProject(Id, Name, HomeTeam.ToDomain(), AwayTeam.ToDomain());
            project.SetGameState(HomeScore, AwayScore, Quarter, GameClockSeconds, Down, Distance, PossessionTeamId);
            foreach (var appearance in PlayerAppearances)
                project.SetPlayerAppearance(appearance.ToDomain());
            if (Uniforms.Count > 0)
                project.ReplaceUniformLibrary(Uniforms.Select(uniform => uniform.ToDomain()), ActiveUniformIds);
            foreach (var play in Plays)
                project.AddPlay(play.ToDomain());
            foreach (var camera in Cameras)
                project.AddCamera(camera.ToDomain());
            foreach (var cut in CameraCuts)
                project.AddCameraCut(cut.ToDomain());
            return project;
        }
    }

    private sealed class UniformData
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public string Name { get; set; } = string.Empty;
        public AppearanceColor PrimaryColor { get; set; }
        public AppearanceColor SecondaryColor { get; set; }
        public AppearanceColor AccentColor { get; set; }
        public AppearanceColor JerseyBaseColor { get; set; }
        public AppearanceColor SleeveTrimColor { get; set; }
        public AppearanceColor CollarTrimColor { get; set; }
        public AppearanceColor NumberColor { get; set; }
        public AppearanceColor NumberOutlineColor { get; set; }
        public AppearanceColor ShortsColor { get; set; }
        public AppearanceColor FlagColor { get; set; }
        public string TeamWordmark { get; set; } = string.Empty;
        public bool ShowPlayerNameOnBack { get; set; }
        public UniformDesignation Designation { get; set; }

        public static UniformData FromDomain(UniformDefinition uniform) => new()
        {
            Id = uniform.Id,
            TeamId = uniform.TeamId,
            Name = uniform.Name,
            PrimaryColor = uniform.PrimaryColor,
            SecondaryColor = uniform.SecondaryColor,
            AccentColor = uniform.AccentColor,
            JerseyBaseColor = uniform.JerseyBaseColor,
            SleeveTrimColor = uniform.SleeveTrimColor,
            CollarTrimColor = uniform.CollarTrimColor,
            NumberColor = uniform.NumberColor,
            NumberOutlineColor = uniform.NumberOutlineColor,
            ShortsColor = uniform.ShortsColor,
            FlagColor = uniform.FlagColor,
            TeamWordmark = uniform.TeamWordmark,
            ShowPlayerNameOnBack = uniform.ShowPlayerNameOnBack,
            Designation = uniform.Designation
        };

        public UniformDefinition ToDomain()
        {
            var uniform = new UniformDefinition(Id, TeamId, Name, Designation);
            uniform.SetPrimaryColors(PrimaryColor, SecondaryColor, AccentColor);
            uniform.SetJerseyColors(JerseyBaseColor, SleeveTrimColor, CollarTrimColor);
            uniform.SetNumberColors(NumberColor, NumberOutlineColor);
            uniform.SetShortsColor(ShortsColor);
            uniform.SetFlagColor(FlagColor);
            uniform.SetWordmark(TeamWordmark);
            uniform.SetShowPlayerNameOnBack(ShowPlayerNameOnBack);
            return uniform;
        }
    }

    private sealed class PlayerAppearanceData
    {
        public Guid PlayerId { get; set; }
        public float HeightMeters { get; set; }
        public BodyBuild BodyBuild { get; set; }
        public float ShoulderWidth { get; set; }
        public float ChestWidth { get; set; }
        public float WaistWidth { get; set; }
        public float HipWidth { get; set; }
        public float ArmLength { get; set; }
        public float LegLength { get; set; }
        public AppearanceColor SkinTone { get; set; }
        public HairStyle HairStyle { get; set; }
        public AppearanceColor HairColor { get; set; }
        public HairAppearanceData? Hair { get; set; }
        public int JerseyNumber { get; set; }
        public AppearanceColor PrimaryUniformColor { get; set; }
        public AppearanceColor SecondaryUniformColor { get; set; }
        public AppearanceColor FlagColor { get; set; }
        public PlayerAccessories Accessories { get; set; }
        public FaceAppearanceData? Face { get; set; }

        public static PlayerAppearanceData FromDomain(PlayerAppearance appearance) => new()
        {
            PlayerId = appearance.PlayerId,
            HeightMeters = appearance.HeightMeters,
            BodyBuild = appearance.BodyBuild,
            ShoulderWidth = appearance.ShoulderWidth,
            ChestWidth = appearance.ChestWidth,
            WaistWidth = appearance.WaistWidth,
            HipWidth = appearance.HipWidth,
            ArmLength = appearance.ArmLength,
            LegLength = appearance.LegLength,
            SkinTone = appearance.SkinTone,
            HairStyle = appearance.HairStyle,
            HairColor = appearance.HairColor,
            Hair = HairAppearanceData.FromDomain(appearance.Hair),
            JerseyNumber = appearance.JerseyNumber,
            PrimaryUniformColor = appearance.PrimaryUniformColor,
            SecondaryUniformColor = appearance.SecondaryUniformColor,
            FlagColor = appearance.FlagColor,
            Accessories = appearance.Accessories,
            Face = FaceAppearanceData.FromDomain(appearance.Face)
        };

        public PlayerAppearance ToDomain()
        {
            var appearance = new PlayerAppearance(PlayerId, JerseyNumber);
            appearance.SetHeight(HeightMeters);
            appearance.SetBodyBuild(BodyBuild);
            appearance.SetBodyProportions(
                DefaultRatio(ShoulderWidth),
                DefaultRatio(ChestWidth),
                DefaultRatio(WaistWidth),
                DefaultRatio(HipWidth),
                DefaultRatio(ArmLength),
                DefaultRatio(LegLength));
            appearance.SetSkinTone(SkinTone);
            appearance.SetHair(HairStyle, HairColor);
            Hair?.ApplyTo(appearance.Hair);
            appearance.SetUniformColors(PrimaryUniformColor, SecondaryUniformColor);
            appearance.SetFlagColor(FlagColor);
            appearance.SetAccessories(Accessories);
            Face?.ApplyTo(appearance.Face);
            return appearance;
        }

        private static float DefaultRatio(float ratio) => ratio == 0 ? 1f : ratio;
    }

    private sealed class HairAppearanceData
    {
        public HairStyle Style { get; set; }
        public float Length { get; set; }
        public float Volume { get; set; }
        public float HairlineHeight { get; set; }
        public float PartPosition { get; set; }
        public float CurlAmount { get; set; }
        public AppearanceColor Color { get; set; }
        public float PonytailLength { get; set; }
        public float PonytailVolume { get; set; }
        public float BunSize { get; set; }

        public static HairAppearanceData FromDomain(HairAppearance hair) => new()
        {
            Style = hair.Style,
            Length = hair.Length,
            Volume = hair.Volume,
            HairlineHeight = hair.HairlineHeight,
            PartPosition = hair.PartPosition,
            CurlAmount = hair.CurlAmount,
            Color = hair.Color,
            PonytailLength = hair.PonytailLength,
            PonytailVolume = hair.PonytailVolume,
            BunSize = hair.BunSize
        };

        public void ApplyTo(HairAppearance hair)
        {
            hair.SetParameters(
                Style,
                Length,
                Volume,
                HairlineHeight,
                PartPosition,
                CurlAmount,
                PonytailLength,
                PonytailVolume,
                BunSize);
            hair.SetColor(Color);
        }
    }

    private sealed class FaceAppearanceData
    {
        public float HeadWidth { get; set; }
        public float HeadHeight { get; set; }
        public float JawWidth { get; set; }
        public float JawHeight { get; set; }
        public float ChinWidth { get; set; }
        public float ChinProjection { get; set; }
        public float CheekboneWidth { get; set; }
        public float CheekFullness { get; set; }
        public float ForeheadHeight { get; set; }
        public float EyeSpacing { get; set; }
        public float EyeSize { get; set; }
        public float EyeVerticalPosition { get; set; }
        public float EyebrowHeight { get; set; }
        public float NoseWidth { get; set; }
        public float NoseLength { get; set; }
        public float NoseProjection { get; set; }
        public float MouthWidth { get; set; }
        public float LipFullness { get; set; }
        public float EarSize { get; set; }
        public float EarPosition { get; set; }
        public AppearanceColor? EyeColor { get; set; }

        public static FaceAppearanceData FromDomain(FaceAppearance face) => new()
        {
            HeadWidth = face.HeadWidth,
            HeadHeight = face.HeadHeight,
            JawWidth = face.JawWidth,
            JawHeight = face.JawHeight,
            ChinWidth = face.ChinWidth,
            ChinProjection = face.ChinProjection,
            CheekboneWidth = face.CheekboneWidth,
            CheekFullness = face.CheekFullness,
            ForeheadHeight = face.ForeheadHeight,
            EyeSpacing = face.EyeSpacing,
            EyeSize = face.EyeSize,
            EyeVerticalPosition = face.EyeVerticalPosition,
            EyebrowHeight = face.EyebrowHeight,
            NoseWidth = face.NoseWidth,
            NoseLength = face.NoseLength,
            NoseProjection = face.NoseProjection,
            MouthWidth = face.MouthWidth,
            LipFullness = face.LipFullness,
            EarSize = face.EarSize,
            EarPosition = face.EarPosition,
            EyeColor = face.EyeColor
        };

        public void ApplyTo(FaceAppearance face)
        {
            face.SetParameters(
                HeadWidth,
                HeadHeight,
                JawWidth,
                JawHeight,
                ChinWidth,
                ChinProjection,
                CheekboneWidth,
                CheekFullness,
                ForeheadHeight,
                EyeSpacing,
                EyeSize,
                EyeVerticalPosition,
                EyebrowHeight,
                NoseWidth,
                NoseLength,
                NoseProjection,
                MouthWidth,
                LipFullness,
                EarSize,
                EarPosition);
            if (EyeColor.HasValue)
                face.SetEyeColor(EyeColor.Value);
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
