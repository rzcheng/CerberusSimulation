using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using CesiumForUnity;

// Builds (and optionally bakes) the MDRS terrain scene.
// Open via Tools > MDRS Scene Builder  or  Tools > MDRS Quick Rebuild.
// Credentials are saved to EditorPrefs — never written to any project file.
public class MDRSSceneBuilder : EditorWindow
{
    public enum TerrainMode { CesiumWorldTerrain, GooglePhotorealistic }

    const string PrefGoogleKey   = "MDRS_GoogleApiKey";
    const string PrefCesiumToken = "MDRS_CesiumToken";
    const string PrefTerrainMode = "MDRS_TerrainMode";
    const string BakeFolder      = "Assets/BakedTerrain";

    // Bake radius kept small on purpose — closer camera = higher-res tiles loaded.
    // 1500m captures the MDRS station + immediate canyon area at excellent quality.
    const float BakeRadius = 1500f;
    const float BakeMSE    = 2f;

    [System.NonSerialized] public TerrainMode mode = TerrainMode.GooglePhotorealistic;
    [System.NonSerialized] public string googleKey   = "";
    [System.NonSerialized] public string cesiumToken = "";

    bool showGoogle;
    bool showCesium;
    bool bakeAfterBuild;

    // State during bake polling
    bool   baking;
    float  bakeProgress;
    Cesium3DTileset activeTileset;
    float  savedMSE;
    float  savedRadius;

    [MenuItem("Tools/MDRS Scene Builder")]
    static void Open()
    {
        var w = GetWindow<MDRSSceneBuilder>("MDRS Scene Builder");
        w.minSize    = new Vector2(400, 260);
        w.googleKey   = EditorPrefs.GetString(PrefGoogleKey,  "");
        w.cesiumToken = EditorPrefs.GetString(PrefCesiumToken, "");
        w.mode        = (TerrainMode)EditorPrefs.GetInt(PrefTerrainMode, (int)TerrainMode.GooglePhotorealistic);
    }

    [MenuItem("Tools/MDRS Quick Rebuild")]
    static void QuickRebuild()
    {
        string gKey = EditorPrefs.GetString(PrefGoogleKey,  "");
        string cTok = EditorPrefs.GetString(PrefCesiumToken, "");

        if (string.IsNullOrEmpty(gKey) || string.IsNullOrEmpty(cTok))
        {
            EditorUtility.DisplayDialog("MDRS Quick Rebuild",
                "No saved credentials. Open Tools > MDRS Scene Builder first.", "OK");
            return;
        }

        var b = CreateInstance<MDRSSceneBuilder>();
        b.googleKey   = gKey;
        b.cesiumToken = cTok;
        b.mode        = (TerrainMode)EditorPrefs.GetInt(PrefTerrainMode, (int)TerrainMode.GooglePhotorealistic);
        b.BuildScene();
        DestroyImmediate(b);
    }

    void OnGUI()
    {
        GUILayout.Label("Mars Desert Research Station", EditorStyles.boldLabel);
        GUILayout.Label("Wayne County, Utah  —  38.4065°N 110.7919°W  1372m", EditorStyles.miniLabel);
        EditorGUILayout.Space(10);

        mode = (TerrainMode)EditorGUILayout.EnumPopup("Terrain Mode", mode);
        EditorGUILayout.Space(6);

        if (mode == TerrainMode.GooglePhotorealistic)
            DrawCredentialRow("Google Maps API Key", ref googleKey, ref showGoogle);

        DrawCredentialRow("Cesium Ion Token", ref cesiumToken, ref showCesium);
        EditorGUILayout.Space(10);

        bakeAfterBuild = EditorGUILayout.Toggle("Bake terrain after load", bakeAfterBuild);
        if (bakeAfterBuild)
            EditorGUILayout.HelpBox(
                "After tiles finish streaming, meshes and textures are saved to " +
                BakeFolder + " as an offline prefab with physics colliders.\n" +
                "Keep the Scene View open and zoomed in to ground level for best tile quality.",
                MessageType.Info);

        EditorGUILayout.Space(10);

        bool canBuild = !string.IsNullOrEmpty(cesiumToken) &&
            (mode == TerrainMode.CesiumWorldTerrain || !string.IsNullOrEmpty(googleKey));

        GUI.enabled = canBuild && !baking;
        if (GUILayout.Button("Build Scene", GUILayout.Height(36)))
        {
            SavePrefs();
            BuildScene();
        }
        GUI.enabled = true;

        if (baking)
        {
            EditorGUILayout.Space(6);
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(false, 20),
                bakeProgress,
                $"Loading tiles... {bakeProgress * 100f:F0}%");
        }
    }

    void DrawCredentialRow(string label, ref string value, ref bool visible)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        value = visible ? EditorGUILayout.TextField(value) : EditorGUILayout.PasswordField(value);
        if (GUILayout.Button(visible ? "Hide" : "Show", GUILayout.Width(45)))
            visible = !visible;
        EditorGUILayout.EndHorizontal();
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefGoogleKey,   googleKey);
        EditorPrefs.SetString(PrefCesiumToken, cesiumToken);
        EditorPrefs.SetInt(PrefTerrainMode,    (int)mode);
    }

    public void BuildScene()
    {
        ClearExisting();
        SetCesiumToken(cesiumToken);

        var root = CreateGeoreference();

        Cesium3DTileset tileset;
        MDRSAreaExcluder excluder;

        if (mode == TerrainMode.CesiumWorldTerrain)
            (tileset, excluder) = AddCesiumWorldTerrain(root);
        else
            (tileset, excluder) = AddGoogleTiles(root);

        var cam = AddCamera(root);
        var sun = AddSun();

        var manager = root.AddComponent<MDRSSceneManager>();
        manager.tileset      = tileset;
        manager.areaExcluder = excluder;
        manager.sceneCamera  = cam;
        manager.sun          = sun;

        FocusSceneView();
        Selection.activeGameObject = root;
        Debug.Log("[MDRS] Scene built.");

        if (bakeAfterBuild)
            StartBake(tileset, excluder);
    }

    // ── Scene construction ────────────────────────────────────────────────────

    static void ClearExisting()
    {
        foreach (string n in new[] { "CesiumGeoreference", "Sun" })
        {
            var go = GameObject.Find(n);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
    }

    static void SetCesiumToken(string token)
    {
        var server = CesiumIonServer.defaultServer;
        if (server == null) return;
        server.defaultIonAccessToken = token;
        EditorUtility.SetDirty(server);
    }

    static GameObject CreateGeoreference()
    {
        var go     = new GameObject("CesiumGeoreference");
        var georef = go.AddComponent<CesiumGeoreference>();
        georef.latitude  = MDRSConfig.Latitude;
        georef.longitude = MDRSConfig.Longitude;
        georef.height    = MDRSConfig.Elevation;
        go.AddComponent<CesiumOriginShift>();
        Undo.RegisterCreatedObjectUndo(go, "Create MDRS Scene");
        return go;
    }

    (Cesium3DTileset, MDRSAreaExcluder) AddCesiumWorldTerrain(GameObject parent)
    {
        var go = new GameObject("CesiumWorldTerrain");
        go.transform.SetParent(parent.transform);

        var tileset = go.AddComponent<Cesium3DTileset>();
        tileset.tilesetSource           = CesiumDataSource.FromCesiumIon;
        tileset.ionAssetID              = MDRSConfig.CesiumWorldTerrainId;
        tileset.ionAccessToken          = cesiumToken;
        tileset.maximumScreenSpaceError = 2;

        var overlay = go.AddComponent<CesiumIonRasterOverlay>();
        overlay.ionAssetID              = MDRSConfig.BingAerialId;
        overlay.ionAccessToken          = cesiumToken;
        overlay.maximumScreenSpaceError = 2;

        var excluder = go.AddComponent<MDRSAreaExcluder>();
        excluder.radiusMeters = 4000f;

        return (tileset, excluder);
    }

    (Cesium3DTileset, MDRSAreaExcluder) AddGoogleTiles(GameObject parent)
    {
        var go = new GameObject("GooglePhotorealistic3DTiles");
        go.transform.SetParent(parent.transform);

        var tileset = go.AddComponent<Cesium3DTileset>();
        tileset.tilesetSource           = CesiumDataSource.FromUrl;
        tileset.url                     = MDRSConfig.GoogleTilesUrl + "?key=" + googleKey;
        tileset.maximumScreenSpaceError = 2;

        var excluder = go.AddComponent<MDRSAreaExcluder>();
        excluder.radiusMeters = 4000f;

        return (tileset, excluder);
    }

    static Camera AddCamera(GameObject parent)
    {
        var existing = Camera.main;
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = new GameObject("CesiumCamera");
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = new Vector3(0, 300, 0);
        go.transform.localRotation = Quaternion.Euler(45, 0, 0);

        var cam = go.AddComponent<Camera>();
        cam.tag           = "MainCamera";
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane  = 500000f;

        go.AddComponent<CesiumCameraController>();
        return cam;
    }

    static Light AddSun()
    {
        var go = new GameObject("Sun");
        go.transform.rotation = Quaternion.Euler(50, -30, 0);

        var light = go.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.2f;
        light.color     = new Color(1f, 0.93f, 0.78f);

        Undo.RegisterCreatedObjectUndo(go, "Create Sun");
        return light;
    }

    static void FocusSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.up * 150f, Quaternion.Euler(45, 0, 0), 1500f);
    }

    // ── Terrain baking ────────────────────────────────────────────────────────

    void StartBake(Cesium3DTileset tileset, MDRSAreaExcluder excluder)
    {
        activeTileset = tileset;
        savedMSE      = tileset.maximumScreenSpaceError;
        savedRadius   = excluder.radiusMeters;

        // Shrink area and boost quality just for the bake
        excluder.radiusMeters           = BakeRadius;
        tileset.maximumScreenSpaceError = BakeMSE;

        baking       = true;
        bakeProgress = 0f;

        EditorApplication.update += PollAndBake;
        Repaint();
    }

    void PollAndBake()
    {
        if (activeTileset == null) { FinishBake(false); return; }

        bakeProgress = activeTileset.ComputeLoadProgress();
        Repaint();

        if (bakeProgress >= 1f)
        {
            EditorApplication.update -= PollAndBake;
            EditorApplication.delayCall += ExecuteBake;
        }
    }

    void ExecuteBake()
    {
        var filters = activeTileset.GetComponentsInChildren<MeshFilter>(false);
        if (filters.Length == 0)
        {
            Debug.LogWarning("[MDRS] Bake found no tile meshes. Is the Scene View open?");
            FinishBake(false);
            return;
        }

        EnsureFolder(BakeFolder);
        EnsureFolder(BakeFolder + "/Textures");
        EnsureFolder(BakeFolder + "/Materials");

        var textureCache  = new Dictionary<Texture, Material>();
        var bakedRoot     = new GameObject("BakedMDRSTerrain");
        int count         = 0;

        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;

            var renderer = filter.GetComponent<MeshRenderer>();
            var mat      = renderer != null ? renderer.sharedMaterial : null;

            // Save the mesh
            string meshPath = $"{BakeFolder}/mesh_{count:D4}.asset";
            var meshCopy = Object.Instantiate(filter.sharedMesh);
            AssetDatabase.CreateAsset(meshCopy, meshPath);

            // Get or create a URP-compatible material with the tile's texture
            var bakedMat = GetOrCreateMaterial(mat, textureCache, count);

            // Rebuild tile as a static GameObject
            var tile = new GameObject($"Tile_{count:D4}");
            tile.transform.SetParent(bakedRoot.transform);
            tile.transform.SetPositionAndRotation(
                filter.transform.position, filter.transform.rotation);
            tile.transform.localScale = filter.transform.lossyScale;
            tile.isStatic = true;

            tile.AddComponent<MeshFilter>().sharedMesh =
                AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            tile.AddComponent<MeshRenderer>().sharedMaterial = bakedMat;
            tile.AddComponent<MeshCollider>().sharedMesh =
                AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            count++;
        }

        string prefabPath = $"{BakeFolder}/MDRSTerrain_Baked.prefab";
        PrefabUtility.SaveAsPrefabAsset(bakedRoot, prefabPath);
        Object.DestroyImmediate(bakedRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Restore original settings
        if (activeTileset != null)
        {
            var excluder = activeTileset.GetComponent<MDRSAreaExcluder>();
            activeTileset.maximumScreenSpaceError = savedMSE;
            if (excluder != null) excluder.radiusMeters = savedRadius;
        }

        Debug.Log($"[MDRS] Baked {count} tiles → {prefabPath}");
        EditorUtility.DisplayDialog("Terrain Baked",
            $"{count} tiles saved to {prefabPath}\n\n" +
            "Drag the prefab into your scene for fully offline terrain with physics.",
            "OK");

        FinishBake(true);
    }

    // Reads the tile's texture, saves it as a PNG asset, returns a new URP Lit material.
    static Material GetOrCreateMaterial(Material sourceMat,
        Dictionary<Texture, Material> cache, int index)
    {
        Texture srcTex = sourceMat != null
            ? (sourceMat.GetTexture("_BaseMap") ?? sourceMat.GetTexture("_MainTex"))
            : null;

        if (srcTex != null && cache.TryGetValue(srcTex, out var cached))
            return cached;

        // Blit to a readable RenderTexture so we can save it regardless of compression
        var rt = RenderTexture.GetTemporary(srcTex != null ? srcTex.width : 512,
                                            srcTex != null ? srcTex.height : 512);

        if (srcTex != null)
            Graphics.Blit(srcTex, rt);
        else
            Graphics.Blit(Texture2D.whiteTexture, rt); // fallback: white

        RenderTexture.active = rt;
        var readable = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // Save texture as PNG
        string texPath = $"{BakeFolder}/Textures/tex_{index:D4}.png";
        File.WriteAllBytes(texPath, readable.EncodeToPNG());
        Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(texPath);
        var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // Create a new URP Lit material pointing to the saved texture
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat    = new Material(shader);
        mat.SetTexture("_BaseMap", savedTex);
        string matPath = $"{BakeFolder}/Materials/mat_{index:D4}.mat";
        AssetDatabase.CreateAsset(mat, matPath);

        var savedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (srcTex != null) cache[srcTex] = savedMat;
        return savedMat;
    }

    void FinishBake(bool success)
    {
        EditorApplication.update    -= PollAndBake;
        EditorApplication.delayCall -= ExecuteBake;
        baking = false;
        Repaint();
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts  = path.Split('/');
            var parent = string.Join("/", parts, 0, parts.Length - 1);
            AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
        }
    }
}
