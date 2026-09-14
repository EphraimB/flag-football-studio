using System;
using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class SportsLightingController : Node3D
{
    private readonly List<SpotLight3D> _fieldLights = [];
    private WorldEnvironment _worldEnvironment = null!;
    private Godot.Environment _environment = null!;
    private ProceduralSkyMaterial _skyMaterial = null!;
    private DirectionalLight3D _sun = null!;

    public VisualPresentationSettings Settings { get; private set; } = VisualPresentationSettings.Default;
    public float AmbientEnergy => _environment.AmbientLightEnergy;
    public float SunEnergy => _sun.LightEnergy;
    public int EnabledFieldLightCount => _fieldLights.FindAll(light => light.Visible).Count;
    public float Exposure => _environment.TonemapExposure;
    public float EffectiveSceneLuminance =>
        Math.Clamp((AmbientEnergy * 0.62f + SunEnergy * 0.32f + EnabledFieldLightCount * 0.08f) * Exposure, 0, 2);

    public override void _Ready()
    {
        _skyMaterial = new ProceduralSkyMaterial();
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            Sky = new Sky { SkyMaterial = _skyMaterial },
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        };
        _worldEnvironment = new WorldEnvironment { Name = "OutdoorSportsEnvironment", Environment = _environment };
        AddChild(_worldEnvironment);

        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90
        };
        AddChild(_sun);
        BuildFieldLights();
        Apply(Settings);
    }

    public void Apply(VisualPresentationSettings settings)
    {
        Settings = settings.Validated();
        StudioMaterialLibrary.SetQuality(Settings.Quality);
        ApplyLightingPreset(Settings.Lighting);
        ApplyQuality(Settings.Quality, Settings.Shadows);
        _environment.TonemapExposure = Settings.Exposure;
    }

    private void ApplyLightingPreset(SportsLightingPreset preset)
    {
        foreach (var light in _fieldLights)
            light.Visible = preset == SportsLightingPreset.NightFieldLights;

        switch (preset)
        {
            case SportsLightingPreset.GoldenHour:
                SetSky(new Color("315379"), new Color("ef9a59"), new Color("4b4038"));
                _environment.AmbientLightEnergy = 0.48f;
                _sun.LightColor = new Color("ffd09a");
                _sun.LightEnergy = 1.2f;
                _sun.RotationDegrees = new Vector3(-20, -62, 0);
                _sun.ShadowOpacity = 0.82f;
                break;
            case SportsLightingPreset.Overcast:
                SetSky(new Color("8998a5"), new Color("d4d7d8"), new Color("5d6263"));
                _environment.AmbientLightEnergy = 0.82f;
                _sun.LightColor = new Color("e8eef2");
                _sun.LightEnergy = 0.46f;
                _sun.RotationDegrees = new Vector3(-62, -24, 0);
                _sun.ShadowOpacity = 0.42f;
                break;
            case SportsLightingPreset.NightFieldLights:
                SetSky(new Color("071326"), new Color("18263a"), new Color("071018"));
                _environment.AmbientLightEnergy = 0.28f;
                _sun.LightColor = new Color("9eb7d8");
                _sun.LightEnergy = 0.16f;
                _sun.RotationDegrees = new Vector3(-48, 35, 0);
                _sun.ShadowOpacity = 0.5f;
                break;
            default:
                SetSky(new Color("4388bd"), new Color("b8d9e8"), new Color("5e756f"));
                _environment.AmbientLightEnergy = 0.62f;
                _sun.LightColor = new Color("fff4dc");
                _sun.LightEnergy = 1.12f;
                _sun.RotationDegrees = new Vector3(-52, -32, 0);
                _sun.ShadowOpacity = 0.72f;
                break;
        }
    }

    private void ApplyQuality(PresentationQualityPreset quality, ShadowQualityPreset shadows)
    {
        GetViewport().Msaa3D = quality switch
        {
            PresentationQualityPreset.Final => Viewport.Msaa.Msaa4X,
            PresentationQualityPreset.High => Viewport.Msaa.Msaa2X,
            _ => Viewport.Msaa.Disabled
        };
        var requestedShadowSize = shadows switch
        {
            ShadowQualityPreset.High => 4096,
            ShadowQualityPreset.Medium => 2048,
            _ => 1024
        };
        var qualityCap = quality switch
        {
            PresentationQualityPreset.Final => 4096,
            PresentationQualityPreset.High => 2048,
            _ => 1024
        };
        RenderingServer.DirectionalShadowAtlasSetSize(
            Math.Min(requestedShadowSize, qualityCap), quality == PresentationQualityPreset.Final);
        _sun.ShadowBlur = shadows switch
        {
            ShadowQualityPreset.High => 1.6f,
            ShadowQualityPreset.Medium => 1.15f,
            _ => 0.75f
        };
        _sun.DirectionalShadowMaxDistance = quality == PresentationQualityPreset.Preview ? 62 : 90;
        foreach (var light in _fieldLights)
            light.ShadowEnabled = quality != PresentationQualityPreset.Preview && shadows != ShadowQualityPreset.Low;
    }

    private void SetSky(Color top, Color horizon, Color ground)
    {
        _skyMaterial.SkyTopColor = top;
        _skyMaterial.SkyHorizonColor = horizon;
        _skyMaterial.GroundHorizonColor = horizon.Darkened(0.25f);
        _skyMaterial.GroundBottomColor = ground;
    }

    private void BuildFieldLights()
    {
        var positions = VenueLayout.FieldLightPositions;
        for (var index = 0; index < positions.Count; index++)
        {
            var light = new SpotLight3D
            {
                Name = $"FieldLight{index + 1}",
                Position = positions[index],
                LightColor = new Color("e4edff"),
                LightEnergy = 5.2f,
                SpotRange = 48,
                SpotAngle = 52,
                SpotAngleAttenuation = 0.55f,
                ShadowEnabled = false,
                Visible = false
            };
            AddChild(light);
            light.LookAt(new Vector3(0, 0, positions[index].Z * 0.25f), Vector3.Up);
            _fieldLights.Add(light);
        }
    }
}
