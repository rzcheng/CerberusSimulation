using UnityEngine;
using UnityEditor;
using CesiumForUnity;

// Snaps the scene view and all cameras to the MDRS location.
// Useful if the camera drifts or the scene opens in the wrong place.
public static class GoToMDRS
{
    [MenuItem("Tools/Go To MDRS")]
    static void Snap()
    {
        var georef = Object.FindFirstObjectByType<CesiumGeoreference>();
        if (georef == null)
        {
            Debug.LogWarning("[MDRS] No CesiumGeoreference found. Run MDRS Scene Builder first.");
            return;
        }

        georef.latitude  = MDRSConfig.Latitude;
        georef.longitude = MDRSConfig.Longitude;
        georef.height    = MDRSConfig.Elevation;
        EditorUtility.SetDirty(georef);

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.transform.localPosition = new Vector3(0, 300, 0);
            cam.transform.localRotation = Quaternion.Euler(45, 0, 0);
            EditorUtility.SetDirty(cam);
        }

        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.up * 150f, Quaternion.Euler(45, 0, 0), 1500f);

        Selection.activeObject = georef;
        Debug.Log("[MDRS] Snapped to MDRS.");
    }
}
