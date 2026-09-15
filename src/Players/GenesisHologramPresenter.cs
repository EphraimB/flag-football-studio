using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

/// <summary>Presentation-only Genesis overlay. It never owns player identity or mesh deformation.</summary>
public partial class GenesisHologramPresenter : Node3D
{
    private readonly Dictionary<MeshInstance3D, Material?> _originalOverlays = [];
    private readonly Dictionary<MeshInstance3D, float> _originalTransparency = [];
    private readonly List<MeshInstance3D> _meshes = [];
    private ShaderMaterial _hologramMaterial = null!;
    private MeshInstance3D _measurementGuide = null!;
    private Label3D _heightLabel = null!;
    private GenesisStage _stage = GenesisStage.Complete;
    private GenesisMaterializationState _state = GenesisMaterializationState.Ready;
    private float _heightMeters = 1.8f;
    private float _targetTransparency;
    private float _currentTransparency;
    private float _scanPhase;

    public GenesisStage Stage => _stage;
    public GenesisMaterializationState MaterializationState => _state;
    public bool HologramActive => _stage != GenesisStage.Complete || _state == GenesisMaterializationState.Failed;

    public void Configure(Node3D visualRoot, float heightMeters)
    {
        ArgumentNullException.ThrowIfNull(visualRoot);
        _heightMeters = heightMeters;
        _hologramMaterial = CreateHologramMaterial();
        foreach (var mesh in Descendants(visualRoot).OfType<MeshInstance3D>())
        {
            _meshes.Add(mesh);
            _originalOverlays[mesh] = mesh.MaterialOverlay;
            _originalTransparency[mesh] = mesh.Transparency;
        }
        BuildMeasurementGuide();
        SetPresentation(GenesisStage.Complete, GenesisMaterializationState.Ready, heightMeters);
        SetProcess(true);
    }

    public void SetPresentation(GenesisStage stage, GenesisMaterializationState state, float heightMeters)
    {
        _stage = stage;
        _state = state;
        _heightMeters = Math.Clamp(heightMeters, PlayerPhysicalFacts.MinimumHeightMeters, PlayerPhysicalFacts.MaximumHeightMeters);
        _targetTransparency = TransparencyFor(stage, state);
        var active = HologramActive;
        _measurementGuide.Visible = active;
        _heightLabel.Text = $"{_heightMeters:0.00} m\n{stage.DisplayName()}";
        RebuildGuideMesh();
        foreach (var mesh in _meshes)
            mesh.MaterialOverlay = active ? _hologramMaterial : _originalOverlays[mesh];
        ApplyVisualState(active);
    }

    public override void _Process(double delta)
    {
        _scanPhase = (_scanPhase + (float)delta * 0.32f) % 1f;
        _currentTransparency = Mathf.MoveToward(_currentTransparency, _targetTransparency, (float)delta * 2.8f);
        if (_hologramMaterial is not null)
        {
            _hologramMaterial.SetShaderParameter("scan_phase", _scanPhase);
            _hologramMaterial.SetShaderParameter("reveal_progress", RevealFor(_stage));
            _hologramMaterial.SetShaderParameter("body_height", _heightMeters);
            _hologramMaterial.SetShaderParameter("failed", _state == GenesisMaterializationState.Failed ? 1f : 0f);
            _hologramMaterial.SetShaderParameter("activity", ActivityFor(_state));
        }
        ApplyVisualState(HologramActive);
    }

    private void ApplyVisualState(bool active)
    {
        foreach (var mesh in _meshes)
            if (GodotObject.IsInstanceValid(mesh))
                mesh.Transparency = active
                    ? Math.Clamp(_originalTransparency[mesh] + _currentTransparency, 0, 0.92f)
                    : _originalTransparency[mesh];
    }

    private void BuildMeasurementGuide()
    {
        _measurementGuide = new MeshInstance3D
        {
            Name = "GenesisMeasurementGuide",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_measurementGuide);
        _heightLabel = new Label3D
        {
            Name = "GenesisHeightLabel",
            Position = new Vector3(-0.98f, _heightMeters + 0.12f, 0),
            FontSize = 28,
            OutlineSize = 7,
            Modulate = new Color(0.35f, 0.95f, 1f),
            NoDepthTest = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(_heightLabel);
        RebuildGuideMesh();
    }

    private void RebuildGuideMesh()
    {
        if (_measurementGuide is null) return;
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.2f, 0.9f, 1f, 0.82f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        var x = -0.82f;
        Line(new Vector3(x, 0, 0), new Vector3(x, _heightMeters, 0));
        foreach (var ratio in new[] { 0f, 0.5f, 0.78f, 1f })
        {
            var y = _heightMeters * ratio;
            Line(new Vector3(x - 0.12f, y, 0), new Vector3(x + 0.12f, y, 0));
        }
        mesh.SurfaceEnd();
        _measurementGuide.Mesh = mesh;
        if (_heightLabel is not null) _heightLabel.Position = new Vector3(x - 0.12f, _heightMeters + 0.12f, 0);
        return;

        void Line(Vector3 from, Vector3 to)
        {
            mesh.SurfaceAddVertex(from);
            mesh.SurfaceAddVertex(to);
        }
    }

    private static ShaderMaterial CreateHologramMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type spatial;
                render_mode unshaded, blend_mix, depth_prepass_alpha, cull_back;

                uniform vec4 hologram_color : source_color = vec4(0.08, 0.82, 1.0, 1.0);
                uniform float scan_phase = 0.0;
                uniform float reveal_progress = 1.0;
                uniform float body_height = 1.8;
                uniform float activity = 0.2;
                uniform float failed = 0.0;
                varying float local_height;

                void vertex() {
                    local_height = VERTEX.y;
                }

                void fragment() {
                    vec3 tint = mix(hologram_color.rgb, vec3(1.0, 0.16, 0.2), failed);
                    float normalized_height = clamp(local_height / max(body_height, 0.1), 0.0, 1.0);
                    float scan = 0.5 + 0.5 * sin((normalized_height - scan_phase) * 110.0);
                    float grid = step(0.82, scan);
                    float rim = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), 2.1);
                    float reveal = smoothstep(reveal_progress - 0.18, reveal_progress + 0.03, normalized_height);
                    ALBEDO = tint;
                    EMISSION = tint * (0.45 + rim * 1.6 + grid * (0.18 + activity * 0.35));
                    ALPHA = clamp(0.12 + rim * 0.48 + grid * 0.12 + reveal * 0.08, 0.08, 0.78);
                }
                """
        };
        return new ShaderMaterial { Shader = shader };
    }

    private static float TransparencyFor(GenesisStage stage, GenesisMaterializationState state)
    {
        if (state == GenesisMaterializationState.Failed) return 0.52f;
        return stage switch
        {
            GenesisStage.Blueprint => 0.78f,
            GenesisStage.BodyFrame => 0.66f,
            GenesisStage.AnatomicalForm => 0.52f,
            GenesisStage.IdentityAppearance => 0.39f,
            GenesisStage.HairDetails => 0.28f,
            GenesisStage.ClothingUniform => 0.17f,
            GenesisStage.PersonalityVoice => 0.08f,
            _ => 0f
        };
    }

    private static float RevealFor(GenesisStage stage) => Math.Clamp(((int)stage + 1f) / 8f, 0.12f, 1f);

    private static float ActivityFor(GenesisMaterializationState state) => state switch
    {
        GenesisMaterializationState.Processing => 1f,
        GenesisMaterializationState.Applying => 0.85f,
        GenesisMaterializationState.Materializing => 0.7f,
        GenesisMaterializationState.Failed => 0.65f,
        _ => 0.2f
    };

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}

public static class GenesisStageDisplayExtensions
{
    public static string DisplayName(this GenesisStage stage) => stage switch
    {
        GenesisStage.BodyFrame => "Body Frame",
        GenesisStage.AnatomicalForm => "Anatomical Form",
        GenesisStage.IdentityAppearance => "Identity / Appearance",
        GenesisStage.HairDetails => "Hair / Details",
        GenesisStage.ClothingUniform => "Clothing / Uniform",
        GenesisStage.PersonalityVoice => "Personality / Voice",
        _ => stage.ToString()
    };
}
