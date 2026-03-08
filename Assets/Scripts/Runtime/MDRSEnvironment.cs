using UnityEngine;
using UnityEngine.Rendering;

// Controls the fog, ambient light, and sun to match the Utah desert atmosphere.
// Attach to any active GameObject in the scene.
public class MDRSEnvironment : MonoBehaviour
{
    [Header("Fog")]
    public Color fogColor   = new Color(0.82f, 0.65f, 0.45f);
    [Range(0f, 0.005f)]
    public float fogDensity = 0.0004f;

    [Header("Ambient Light")]
    public Color skyColor      = new Color(0.55f, 0.72f, 0.90f);
    public Color equatorColor  = new Color(0.65f, 0.55f, 0.40f);
    public Color groundColor   = new Color(0.45f, 0.30f, 0.18f);

    [Header("Sun")]
    public Light sun;
    public Color sunColor      = new Color(1f, 0.93f, 0.78f);
    [Range(0.5f, 3f)]
    public float sunIntensity  = 1.2f;

    void Start() => Apply();

#if UNITY_EDITOR
    void OnValidate() => Apply();
#endif

    void Apply()
    {
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = skyColor;
        RenderSettings.ambientEquatorColor = equatorColor;
        RenderSettings.ambientGroundColor  = groundColor;

        if (sun != null)
        {
            sun.color     = sunColor;
            sun.intensity = sunIntensity;
        }
    }
}
