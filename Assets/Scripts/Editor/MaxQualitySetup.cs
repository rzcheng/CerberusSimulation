using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using CesiumForUnity;

// Cranks up tile detail, shadow quality, and MSAA for the MDRS scene.
// Run via Tools > Max Render Quality.
public static class MaxQualitySetup
{
    [MenuItem("Tools/Max Render Quality")]
    static void Apply()
    {
        foreach (var tileset in Object.FindObjectsByType<Cesium3DTileset>(FindObjectsSortMode.None))
        {
            tileset.maximumScreenSpaceError = 1;
            EditorUtility.SetDirty(tileset);
        }

        foreach (var overlay in Object.FindObjectsByType<CesiumRasterOverlay>(FindObjectsSortMode.None))
        {
            overlay.maximumScreenSpaceError = 1;
            EditorUtility.SetDirty(overlay);
        }

        var urp = GetURPAsset();
        if (urp == null)
        {
            Debug.LogWarning("[MaxQuality] URP asset not found.");
            return;
        }

        urp.shadowDistance             = 2000f;
        urp.shadowCascadeCount         = 4;
        urp.mainLightShadowmapResolution = 4096;
        urp.msaaSampleCount            = 4;
        urp.renderScale                = 1f;
        urp.supportsHDR                = true;

        EditorUtility.SetDirty(urp);
        AssetDatabase.SaveAssets();

        Debug.Log("[MaxQuality] Done.");
    }

    static UniversalRenderPipelineAsset GetURPAsset()
    {
        var active = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (active != null) return active;

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path  = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null && path.Contains("PC")) return asset;
        }

        return null;
    }
}
