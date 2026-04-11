using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Attach to any GameObject in the scene.
/// Automatically assigns the skybox material, enables HDR post-processing on the
/// main camera, and creates a global Volume with Bloom + ACES Tonemapping + Vignette
/// so the aurora and moon halo glow realistically.
/// </summary>
[ExecuteAlways]
public class NightSkySetup : MonoBehaviour
{
    [Header("Skybox Material")]
    [Tooltip("Drag your Night Sky Aurora material here. It will be set as the active skybox.")]
    public Material nightSkyMaterial;

    [Header("Post-Processing  (auto-created Volume)")]
    [Tooltip("Create a global Volume with Bloom, Tonemapping and Vignette on Start.")]
    public bool autoSetupPostProcessing = true;
    [Tooltip("If another global Volume already exists, skip auto-creating/modifying post-processing from this script.")]
    public bool skipIfGlobalVolumeExists = true;
    [Range(0, 5)]  public float bloomIntensity = 1.5f;
    [Range(0, 3)]  public float bloomThreshold = 0.8f;
    [Range(0, 1)]  public float bloomScatter   = 0.7f;

    [Header("Moon Animation")]
    public bool animateMoonOrbit;
    [Range(0, 0.3f)] public float moonOrbitSpeed = 0.02f;

    [Header("Aurora Pulse")]
    public bool pulseAurora;
    [Range(0, 3)]  public float pulseSpeed = 0.5f;
    [Range(0, 5)]  public float pulseMin   = 0.5f;
    [Range(0, 5)]  public float pulseMax   = 2.0f;

    [Header("Night Environment")]
    [Tooltip("Configure ambient lighting, fog, and disable the default sun for a dark night scene.")]
    public bool autoConfigureEnvironment = true;
    [ColorUsage(false, false)]
    public Color ambientColor = new Color(0.01f, 0.015f, 0.04f);
    [Range(0f, 1f)] public float reflectionIntensity = 0.15f;

    [Header("Moonlight")]
    [Tooltip("Create a dim directional light aligned with the shader moon direction.")]
    public bool createMoonlight = true;
    [Range(0f, 2f)] public float moonlightIntensity = 0.12f;
    [ColorUsage(false, false)]
    public Color moonlightColor = new Color(0.55f, 0.62f, 0.78f);

    [Header("Ground Plane")]
    [Tooltip("Spawn a large dark ground plane so the scene has something to stand on.")]
    public bool createGroundPlane = true;
    [ColorUsage(false, false)]
    public Color groundColor = new Color(0.02f, 0.025f, 0.03f);

    [Header("Night Color Grade")]
    [Tooltip("When enabled, post-exposure darkens/brightens the whole frame (including skybox). Disable to preserve star/aurora detail.")]
    public bool applyPostExposure = false;
    [Range(-3f, 0f)] public float nightExposure = -0.5f;
    [ColorUsage(false, false)]
    public Color nightColorFilter = new Color(0.78f, 0.82f, 1.0f);

    Volume        _volume;
    Bloom         _bloom;
    VolumeProfile _runtimeProfile;
    Light         _moonlight;
    GameObject    _groundPlane;
    Material      _groundMat;

    void OnEnable()
    {
        ApplySceneSettings();
    }

    void OnValidate()
    {
        ApplySceneSettings();
    }

    /// <summary>
    /// Applies skybox assignment so it is visible in editor and runtime.
    /// </summary>
    void ApplySceneSettings()
    {
        if (nightSkyMaterial != null)
            RenderSettings.skybox = nightSkyMaterial;
    }

    void Start()
    {
        ApplySceneSettings();

        if (!Application.isPlaying) return;

        if (createMoonlight)
            SetupMoonlight();

        if (createGroundPlane)
            SpawnGroundPlane();
    }

    bool ShouldSkipPostProcessingSetup()
    {
        if (!skipIfGlobalVolumeExists)
            return false;

        foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (volume == null || !volume.enabled || !volume.isGlobal || volume.weight <= 0f)
                continue;

            if (volume.gameObject == gameObject)
                continue;

            if (volume.profile != null || volume.sharedProfile != null)
            {
                Debug.Log("[NightSky] Existing global Volume detected. Skipping NightSkySetup post-processing override.");
                return true;
            }
        }

        return false;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (nightSkyMaterial == null) return;

        if (animateMoonOrbit)
        {
            float x     = nightSkyMaterial.GetFloat("_MoonDirX");
            float z     = nightSkyMaterial.GetFloat("_MoonDirZ");
            float angle = Mathf.Atan2(z, x) + moonOrbitSpeed * Time.deltaTime;
            nightSkyMaterial.SetFloat("_MoonDirX", Mathf.Cos(angle));
            nightSkyMaterial.SetFloat("_MoonDirZ", Mathf.Sin(angle));
        }

        if (pulseAurora)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            nightSkyMaterial.SetFloat("_AuroraIntensity",
                Mathf.Lerp(pulseMin, pulseMax, t));
        }

        if (_moonlight != null)
            SyncMoonlightDirection();
    }

    void EnableCameraPostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data != null && !data.renderPostProcessing)
        {
            data.renderPostProcessing = true;
            Debug.Log("[NightSky] Enabled Post Processing on Main Camera.");
        }
    }

    void LogRenderingRecommendations()
    {
        RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.defaultRenderPipeline;

        var urp = pipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            Debug.LogWarning("[NightSky] No URP asset found. This shader requires the Universal Render Pipeline.");
            return;
        }

        if (!urp.supportsHDR)
        {
            Debug.LogWarning(
                "[NightSky] HDR is OFF on the URP asset — Bloom will not work.\n" +
                "Enable it at: Edit ▸ Project Settings ▸ Quality ▸ Rendering ▸ HDR");
        }

        Debug.Log(
            "[NightSky] Rendering checklist for best results:\n" +
            "  1. Enable HDR on your URP Asset\n" +
            "  2. Camera ▸ Post Processing = ON  (auto-done by this script)\n" +
            "  3. Player Settings ▸ Color Space = Linear\n" +
            "  4. Assign a moon texture to the material for detail");
    }

    void SetupPostProcessing()
    {
        _volume = GetComponent<Volume>();
        if (_volume == null)
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1;
        }

        if (_volume.profile == null)
        {
            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile  = _runtimeProfile;
        }
        else
        {
            _runtimeProfile = Instantiate(_volume.profile);
            _volume.profile = _runtimeProfile;
        }

        // Bloom — makes aurora and moon halo glow
        if (!_volume.profile.TryGet(out _bloom))
            _bloom = _volume.profile.Add<Bloom>();
        _bloom.active                  = true;
        _bloom.intensity.overrideState = true;
        _bloom.intensity.value         = bloomIntensity;
        _bloom.threshold.overrideState = true;
        _bloom.threshold.value         = bloomThreshold;
        _bloom.scatter.overrideState   = true;
        _bloom.scatter.value           = bloomScatter;

        // Tonemapping — ACES for cinematic HDR look
        if (!_volume.profile.TryGet(out Tonemapping tonemap))
            tonemap = _volume.profile.Add<Tonemapping>();
        tonemap.active              = true;
        tonemap.mode.overrideState  = true;
        tonemap.mode.value          = TonemappingMode.ACES;

        // Vignette — subtle night-vision framing
        if (!_volume.profile.TryGet(out Vignette vignette))
            vignette = _volume.profile.Add<Vignette>();
        vignette.active                  = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value         = 0.3f;
        vignette.color.overrideState     = true;
        vignette.color.value             = new Color(0.02f, 0.0f, 0.1f);

        // Color grade — cool tint + optional exposure
        if (!_volume.profile.TryGet(out ColorAdjustments colorAdj))
            colorAdj = _volume.profile.Add<ColorAdjustments>();
        colorAdj.active                     = true;
        colorAdj.postExposure.overrideState  = applyPostExposure;
        colorAdj.postExposure.value          = applyPostExposure ? nightExposure : 0f;
        colorAdj.colorFilter.overrideState   = true;
        colorAdj.colorFilter.value           = nightColorFilter;
        colorAdj.contrast.overrideState      = true;
        colorAdj.contrast.value              = 15f;
        colorAdj.saturation.overrideState    = true;
        colorAdj.saturation.value            = -10f;
    }

    void ConfigureNightEnvironment()
    {
        // Very dark flat ambient for a convincing night
        RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight     = ambientColor;
        RenderSettings.ambientIntensity = 0.3f;

        // Dim reflections so shiny surfaces don't glow
        RenderSettings.reflectionIntensity = reflectionIntensity;

        // Subtle dark fog for depth and atmosphere
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = new Color(0.01f, 0.015f, 0.03f);
        RenderSettings.fogDensity = 0.008f;

        // Disable any existing directional lights (the default "sun")
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional && light.gameObject != gameObject)
                light.enabled = false;
        }

        Debug.Log("[NightSky] Environment configured: dark ambient, fog, default sun disabled.");
    }

    void SetupMoonlight()
    {
        var go = new GameObject("Moonlight");
        go.transform.SetParent(transform);
        _moonlight            = go.AddComponent<Light>();
        _moonlight.type       = LightType.Directional;
        _moonlight.color      = moonlightColor;
        _moonlight.intensity  = moonlightIntensity;
        _moonlight.shadows    = LightShadows.Soft;
        _moonlight.shadowStrength = 0.6f;
        SyncMoonlightDirection();
        Debug.Log("[NightSky] Moonlight created (intensity " + moonlightIntensity + ").");
    }

    void SyncMoonlightDirection()
    {
        if (_moonlight == null || nightSkyMaterial == null) return;
        float x = nightSkyMaterial.GetFloat("_MoonDirX");
        float y = nightSkyMaterial.GetFloat("_MoonDirY");
        float z = nightSkyMaterial.GetFloat("_MoonDirZ");
        _moonlight.transform.rotation = Quaternion.LookRotation(new Vector3(-x, -y, -z));
    }

    void SpawnGroundPlane()
    {
        _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _groundPlane.name = "NightGround";
        _groundPlane.transform.position   = Vector3.zero;
        _groundPlane.transform.localScale = new Vector3(50f, 1f, 50f);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            _groundMat = new Material(shader);
            _groundMat.color = groundColor;
            _groundMat.SetFloat("_Smoothness", 0.15f);
            _groundMat.SetFloat("_Metallic", 0f);
            _groundPlane.GetComponent<Renderer>().material = _groundMat;
        }
    }

    void OnDestroy()
    {
        if (_runtimeProfile != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeProfile);
            else
                DestroyImmediate(_runtimeProfile);
        }
        if (_groundMat != null)
        {
            if (Application.isPlaying)
                Destroy(_groundMat);
            else
                DestroyImmediate(_groundMat);
        }
    }
}
