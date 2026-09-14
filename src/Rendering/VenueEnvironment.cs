using System;
using System.Collections.Generic;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

/// <summary>
/// Reusable presentation-only community sports venue. It deliberately knows nothing about play
/// assignments, simulation frames, or outcome rules.
/// </summary>
public partial class VenueEnvironment : Node3D
{
    private static readonly BoxMesh UnitBox = new() { Size = Vector3.One };
    private static readonly CylinderMesh UnitCylinder = new()
    {
        TopRadius = 0.5f,
        BottomRadius = 0.5f,
        Height = 1,
        RadialSegments = 10
    };
    private static readonly CylinderMesh UnitCone = new()
    {
        TopRadius = 0.08f,
        BottomRadius = 0.5f,
        Height = 1,
        RadialSegments = 10
    };

    private readonly List<VenueObstacle> _fixedGeometryBounds = [];
    private Node3D _geometry = null!;
    private ProceduralSpectatorSystem _spectators = null!;
    private VenueAudioHooks _audioHooks = null!;
    private Label3D _scoreLabel = null!;
    private Label3D _clockLabel = null!;
    private GameProject? _scoreboardProject;

    public VenuePresentationSettings Settings { get; private set; } = VenuePresentationSettings.Default;
    public PresentationQualityPreset Quality { get; private set; } = PresentationQualityPreset.Preview;
    public int FixedComponentCount { get; private set; }
    public int EquipmentComponentCount { get; private set; }
    public int BleacherSectionCount { get; private set; }
    public int SpectatorCount => _spectators.InstanceCount;
    public int SpectatorMultiMeshCount => _spectators.MultiMeshCount;
    public int SeatedSpectatorCount => _spectators.SeatedCount;
    public int StandingSpectatorCount => _spectators.StandingCount;
    public string SpectatorPlacementSignature => _spectators.PlacementSignature;
    public bool SpectatorsUseSharedResources => _spectators.UsesSharedResources;
    public IReadOnlyList<VenueObstacle> FixedGeometryBounds => _fixedGeometryBounds;
    public IReadOnlyList<Vector3> SpectatorViewpoints =>
        ProceduralSpectatorSystem.SpectatorViewpoints(Settings.Preset);
    public IReadOnlyDictionary<VenueAudioHookKind, Node3D> AudioAnchors => _audioHooks.Anchors;
    public string ScoreText => _scoreLabel.Text;
    public string ClockText => _clockLabel.Text;

    public override void _Ready()
    {
        _geometry = new Node3D { Name = "VenueGeometry" };
        AddChild(_geometry);
        _spectators = new ProceduralSpectatorSystem { Name = "Spectators" };
        AddChild(_spectators);
        _audioHooks = new VenueAudioHooks { Name = "FutureAudioHooks" };
        AddChild(_audioHooks);
        Apply(Settings, Quality);
    }

    public void Apply(VenuePresentationSettings settings, PresentationQualityPreset quality)
    {
        Settings = settings.Validated();
        Quality = quality;
        ClearGeometry();
        BuildGroundContext();
        BuildSidelines();
        BuildBoundaryAndEntries();
        BuildBleachers();
        BuildFieldLightFixtures();
        BuildScoreboard();
        _spectators.Configure(Settings.Preset, Settings.SpectatorDensity, quality, Settings.ShowSpectators);
        if (_scoreboardProject is not null)
            SyncScoreboard(_scoreboardProject);
    }

    public void SyncScoreboard(GameProject project)
    {
        _scoreboardProject = project ?? throw new ArgumentNullException(nameof(project));
        if (_scoreLabel is null)
            return;
        _scoreLabel.Text = $"{project.HomeTeam.Name.ToUpperInvariant()}  {project.HomeScore}    {project.AwayScore}  {project.AwayTeam.Name.ToUpperInvariant()}";
        var minutes = project.GameClockSeconds / 60;
        var seconds = project.GameClockSeconds % 60;
        _clockLabel.Text = $"Q{project.Quarter}     {minutes:00}:{seconds:00}";
    }

    public bool IntersectsFixedGeometry(Vector3 point, float clearance = 0) 
    {
        foreach (var obstacle in _fixedGeometryBounds)
        {
            var grow = new Vector3(clearance, clearance, clearance);
            var expanded = new Aabb(obstacle.Bounds.Position - grow, obstacle.Bounds.Size + grow * 2);
            if (expanded.HasPoint(point))
                return true;
        }
        return false;
    }

    private void ClearGeometry()
    {
        foreach (var child in _geometry.GetChildren())
        {
            _geometry.RemoveChild(child);
            child.QueueFree();
        }
        _fixedGeometryBounds.Clear();
        FixedComponentCount = 0;
        EquipmentComponentCount = 0;
        BleacherSectionCount = 0;
        _scoreLabel = null!;
        _clockLabel = null!;
    }

    private void BuildGroundContext()
    {
        AddBox("VenueGround", new Vector3(0, -0.12f, 0), new Vector3(48, 0.2f, 54),
            StudioMaterialLibrary.Generic(new Color("31433a")));
        AddBox("GoldStandingZone", new Vector3(-11.2f, 0.012f, 0), new Vector3(1.75f, 0.025f, 35),
            StudioMaterialLibrary.Generic(new Color("796725")));
        AddBox("NavyStandingZone", new Vector3(11.2f, 0.012f, 0), new Vector3(1.75f, 0.025f, 35),
            StudioMaterialLibrary.Generic(new Color("172a4c")));
        AddBox("NorthWalkway", new Vector3(0, 0.015f, 22.4f), new Vector3(45, 0.03f, 3.5f),
            StudioMaterialLibrary.Generic(new Color("79766e")));
        AddBox("SouthWalkway", new Vector3(0, 0.015f, -22.4f), new Vector3(45, 0.03f, 3.5f),
            StudioMaterialLibrary.Generic(new Color("79766e")));
    }

    private void BuildSidelines()
    {
        var detail = Quality switch
        {
            PresentationQualityPreset.Final => 2,
            PresentationQualityPreset.High => 1,
            _ => 0
        };
        BuildTeamSideline(-1, new Color("d5ad2d"), "Gold", detail);
        BuildTeamSideline(1, new Color("162a50"), "Navy", detail);
        foreach (var x in new[] { -10.7f, 10.7f })
        foreach (var z in new[] { -18.2f, 18.2f })
            AddCone($"BoundaryCone_{x}_{z}", new Vector3(x, 0.24f, z), new Vector3(0.24f, 0.48f, 0.24f),
                StudioMaterialLibrary.Generic(new Color("ed7b22")));
    }

    private void BuildTeamSideline(float side, Color teamColor, string teamName, int detail)
    {
        foreach (var z in new[] { -8.3f, 8.3f })
        {
            var x = side * 14.5f;
            AddBench($"{teamName}Bench_{z}", new Vector3(x, 0, z), teamColor);
        }

        // The center z=-4..4 corridor remains empty for the default x=+/-13 sideline cameras.
        if (!Settings.ShowSidelineEquipment)
            return;
        foreach (var z in new[] { -13.3f, 13.3f })
        {
            var x = side * 12.2f;
            AddCylinder($"{teamName}Cooler_{z}", new Vector3(x, 0.48f, z), new Vector3(0.46f, 0.85f, 0.46f),
                StudioMaterialLibrary.Generic(teamColor.Lightened(0.25f)));
            AddBox($"{teamName}Equipment_{z}", new Vector3(x + side * 0.75f, 0.32f, z),
                new Vector3(0.7f, 0.62f, 1.15f), StudioMaterialLibrary.Generic(new Color("545861")), true);
            EquipmentComponentCount += 2;
            if (detail > 0)
            {
                AddCylinder($"{teamName}Bottle_{z}", new Vector3(x - side * 0.58f, 0.25f, z),
                    new Vector3(0.1f, 0.42f, 0.1f), StudioMaterialLibrary.Generic(new Color("c8e6e9")));
                EquipmentComponentCount++;
            }
        }
    }

    private void AddBench(string name, Vector3 position, Color accent)
    {
        AddBox($"{name}_Seat", position + new Vector3(0, 0.63f, 0), new Vector3(0.68f, 0.12f, 5.1f),
            StudioMaterialLibrary.Generic(new Color("8d9295")), true);
        AddBox($"{name}_Back", position + new Vector3(position.X < 0 ? -0.28f : 0.28f, 1.08f, 0),
            new Vector3(0.1f, 0.85f, 5.1f), StudioMaterialLibrary.Generic(accent), true);
        foreach (var z in new[] { -2f, 2f })
            AddBox($"{name}_Leg_{z}", position + new Vector3(0, 0.31f, z), new Vector3(0.12f, 0.62f, 0.12f),
                StudioMaterialLibrary.Generic(new Color("565b60")), true);
    }

    private void BuildBoundaryAndEntries()
    {
        var fenceMaterial = StudioMaterialLibrary.Generic(new Color(0.28f, 0.32f, 0.34f, 0.72f), true);
        AddBox("LeftBoundaryFence", new Vector3(-23.2f, 1.3f, 0), new Vector3(0.08f, 2.6f, 48), fenceMaterial, true);
        AddBox("RightBoundaryFence", new Vector3(23.2f, 1.3f, 0), new Vector3(0.08f, 2.6f, 48), fenceMaterial, true);
        foreach (var z in new[] { -24.1f, 24.1f })
        {
            AddBox($"EndFenceLeft_{z}", new Vector3(-12.5f, 1.3f, z), new Vector3(21, 2.6f, 0.08f), fenceMaterial, true);
            AddBox($"EndFenceRight_{z}", new Vector3(12.5f, 1.3f, z), new Vector3(21, 2.6f, 0.08f), fenceMaterial, true);
            AddBox($"EntryHeader_{z}", new Vector3(0, 2.75f, z), new Vector3(4, 0.18f, 0.22f),
                StudioMaterialLibrary.Generic(new Color("454a4e")), true);
            AddBox($"EntryPostLeft_{z}", new Vector3(-2, 1.38f, z), new Vector3(0.18f, 2.75f, 0.18f),
                StudioMaterialLibrary.Generic(new Color("454a4e")), true);
            AddBox($"EntryPostRight_{z}", new Vector3(2, 1.38f, z), new Vector3(0.18f, 2.75f, 0.18f),
                StudioMaterialLibrary.Generic(new Color("454a4e")), true);
        }
    }

    private void BuildBleachers()
    {
        var sectionsPerSide = Settings.Preset switch
        {
            VenuePreset.PracticeField => 1,
            VenuePreset.CommunityField => 2,
            _ => 3
        };
        var rows = Settings.Preset switch
        {
            VenuePreset.PracticeField => 2,
            VenuePreset.CommunityField => 3,
            _ => 4
        };
        var sectionWidth = Settings.Preset == VenuePreset.CollegeField ? 8.4f : 7.2f;
        var metal = StudioMaterialLibrary.Generic(new Color("777f83"));
        foreach (var side in new[] { -1f, 1f })
        for (var section = 0; section < sectionsPerSide; section++)
        {
            var z = (section - (sectionsPerSide - 1) * 0.5f) * 10.5f;
            BleacherSectionCount++;
            for (var row = 0; row < rows; row++)
            {
                var x = side * (17.3f + row * 0.62f);
                var y = 0.5f + row * 0.42f;
                AddBox($"Bleacher_{side}_{section}_{row}", new Vector3(x, y, z),
                    new Vector3(0.72f, 0.12f, sectionWidth), metal, true);
                AddBox($"BleacherRiser_{side}_{section}_{row}", new Vector3(x + side * 0.24f, y * 0.5f, z),
                    new Vector3(0.1f, Math.Max(0.45f, y), sectionWidth), metal, true);
            }
        }
    }

    private void BuildFieldLightFixtures()
    {
        var pole = StudioMaterialLibrary.Generic(new Color("4f555a"));
        var lamp = StudioMaterialLibrary.Generic(new Color("d7e0e5"));
        for (var index = 0; index < VenueLayout.FieldLightPositions.Count; index++)
        {
            var lightPosition = VenueLayout.FieldLightPositions[index];
            AddCylinder($"LightPole_{index + 1}", new Vector3(lightPosition.X, 6.5f, lightPosition.Z),
                new Vector3(0.16f, 13, 0.16f), pole, true);
            AddBox($"LightBar_{index + 1}", lightPosition, new Vector3(2.2f, 0.35f, 0.45f), lamp, true);
            var fixtureCount = Quality == PresentationQualityPreset.Preview ? 2 : 4;
            for (var fixture = 0; fixture < fixtureCount; fixture++)
                AddBox($"LightFixture_{index + 1}_{fixture + 1}",
                    lightPosition + new Vector3((fixture - (fixtureCount - 1) * 0.5f) * 0.48f, -0.15f, -0.3f),
                    new Vector3(0.38f, 0.28f, 0.3f), lamp);
        }
    }

    private void BuildScoreboard()
    {
        var root = new Node3D { Name = "PhysicalScoreboard", Position = new Vector3(0, 0, 22.8f) };
        _geometry.AddChild(root);
        AddBoxTo(root, "Board", new Vector3(0, 5.25f, 0), new Vector3(7.8f, 3.25f, 0.38f),
            StudioMaterialLibrary.Generic(new Color("172126")));
        AddBoxTo(root, "LeftPost", new Vector3(-2.7f, 2, 0), new Vector3(0.22f, 4, 0.22f),
            StudioMaterialLibrary.Generic(new Color("60676a")));
        AddBoxTo(root, "RightPost", new Vector3(2.7f, 2, 0), new Vector3(0.22f, 4, 0.22f),
            StudioMaterialLibrary.Generic(new Color("60676a")));
        _scoreLabel = NewScoreLabel("Score", new Vector3(0, 5.75f, -0.22f), 58, new Color("ffd45a"));
        _clockLabel = NewScoreLabel("Clock", new Vector3(0, 4.75f, -0.22f), 52, new Color("f2f4f1"));
        root.AddChild(_scoreLabel);
        root.AddChild(_clockLabel);
        _fixedGeometryBounds.Add(new VenueObstacle("PhysicalScoreboard",
            new Aabb(new Vector3(-3.9f, 0, 22.61f), new Vector3(7.8f, 6.88f, 0.38f))));
        FixedComponentCount += 3;
    }

    private static Label3D NewScoreLabel(string name, Vector3 position, int fontSize, Color color) => new()
    {
        Name = name,
        Position = position,
        Text = "--",
        FontSize = fontSize,
        PixelSize = 0.006f,
        Modulate = color,
        OutlineSize = 8,
        NoDepthTest = false
    };

    private void AddBox(string name, Vector3 position, Vector3 size, Material material, bool obstacle = false)
    {
        var mesh = new MeshInstance3D { Name = name, Mesh = UnitBox, Position = position, Scale = size, MaterialOverride = material };
        _geometry.AddChild(mesh);
        FixedComponentCount++;
        if (obstacle)
            _fixedGeometryBounds.Add(new VenueObstacle(name, new Aabb(position - size * 0.5f, size)));
    }

    private static void AddBoxTo(Node3D parent, string name, Vector3 position, Vector3 size, Material material) =>
        parent.AddChild(new MeshInstance3D
        {
            Name = name, Mesh = UnitBox, Position = position, Scale = size, MaterialOverride = material
        });

    private void AddCylinder(string name, Vector3 position, Vector3 size, Material material, bool obstacle = false)
    {
        var mesh = new MeshInstance3D { Name = name, Mesh = UnitCylinder, Position = position, Scale = size, MaterialOverride = material };
        _geometry.AddChild(mesh);
        FixedComponentCount++;
        if (obstacle)
            _fixedGeometryBounds.Add(new VenueObstacle(name, new Aabb(position - size * 0.5f, size)));
    }

    private void AddCone(string name, Vector3 position, Vector3 size, Material material)
    {
        _geometry.AddChild(new MeshInstance3D
        {
            Name = name, Mesh = UnitCone, Position = position, Scale = size, MaterialOverride = material
        });
        FixedComponentCount++;
    }
}

public readonly record struct VenueObstacle(string Name, Aabb Bounds);
