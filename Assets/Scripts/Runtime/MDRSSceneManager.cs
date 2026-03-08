using UnityEngine;
using CesiumForUnity;

// Attached to CesiumGeoreference. Holds references to everything in the MDRS scene
// and exposes controls for radius, quality, and camera height at runtime.
[DefaultExecutionOrder(-1)]
public class MDRSSceneManager : MonoBehaviour
{
    [Header("Scene Objects")]
    public Cesium3DTileset tileset;
    public MDRSAreaExcluder areaExcluder;
    public Camera sceneCamera;
    public Light sun;
    public MDRSEnvironment environment;

    [Header("Area")]
    [Tooltip("Tile loading radius in meters. Lower = better performance.")]
    [Range(1000f, 10000f)]
    public float radiusMeters = 4000f;

    [Header("Camera")]
    [Range(50f, 2000f)]
    public float cameraHeight = 300f;

    [Header("Quality")]
    [Tooltip("Lower = sharper tiles but heavier. 1 = ultra, 16 = default.")]
    [Range(1f, 16f)]
    public float tileQuality = 2f;

    void Start()
    {
        ApplySettings();
    }

    void OnValidate()
    {
        ApplySettings();
    }

    void ApplySettings()
    {
        if (areaExcluder != null)
            areaExcluder.radiusMeters = radiusMeters;

        if (tileset != null)
            tileset.maximumScreenSpaceError = tileQuality;

        if (sceneCamera != null)
            sceneCamera.transform.localPosition = new Vector3(0, cameraHeight, 0);
    }

    // Call from other scripts to temporarily expand the area, e.g. for cutscenes.
    public void SetRadius(float meters)
    {
        radiusMeters = meters;
        ApplySettings();
    }

    public void SetCameraHeight(float meters)
    {
        cameraHeight = meters;
        ApplySettings();
    }
}
