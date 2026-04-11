using UnityEditor;
using UnityEngine;
using System.IO;

public class WinterTerrainGenerator : EditorWindow
{
    [Header("Terrain Size")]
    int   _terrainWidth     = 200;
    int   _terrainLength    = 200;
    int   _terrainHeight    = 35;
    int   _heightmapRes     = 257;

    [Header("Noise")]
    float _noiseScale       = 0.015f;
    float _hillAmplitude    = 0.25f;
    int   _noiseSeed        = 42;
    int   _noiseOctaves     = 4;

    [Header("Camp Clearing")]
    float _clearingRadius   = 20f;
    float _clearingSmoothing = 10f;

    [Header("Snow Textures")]
    Texture2D _snowAlbedo;
    Texture2D _snowNormal;

    [Header("Trees")]
    bool  _generateTrees    = true;
    int   _treeCount        = 150;
    float _treeMinSpacing   = 4f;
    float _treeMinScale     = 0.8f;
    float _treeMaxScale     = 1.4f;

    Vector2 _scroll;

    [MenuItem("Tools/Winter Terrain Generator")]
    static void Open() => GetWindow<WinterTerrainGenerator>("Winter Terrain");

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Terrain Dimensions", EditorStyles.boldLabel);
        _terrainWidth   = EditorGUILayout.IntField("Width",  _terrainWidth);
        _terrainLength  = EditorGUILayout.IntField("Length", _terrainLength);
        _terrainHeight  = EditorGUILayout.IntField("Height", _terrainHeight);
        _heightmapRes   = EditorGUILayout.IntField("Heightmap Resolution", _heightmapRes);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Perlin Noise", EditorStyles.boldLabel);
        _noiseScale     = EditorGUILayout.FloatField("Scale",     _noiseScale);
        _hillAmplitude  = EditorGUILayout.Slider("Hill Amplitude", _hillAmplitude, 0f, 1f);
        _noiseSeed      = EditorGUILayout.IntField("Seed",        _noiseSeed);
        _noiseOctaves   = EditorGUILayout.IntSlider("Octaves",    _noiseOctaves, 1, 8);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camp Clearing (Center)", EditorStyles.boldLabel);
        _clearingRadius    = EditorGUILayout.Slider("Radius",    _clearingRadius,   5f,  60f);
        _clearingSmoothing = EditorGUILayout.Slider("Smoothing", _clearingSmoothing, 1f, 30f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Snow Textures", EditorStyles.boldLabel);
        _snowAlbedo = (Texture2D)EditorGUILayout.ObjectField("Albedo", _snowAlbedo, typeof(Texture2D), false);
        _snowNormal = (Texture2D)EditorGUILayout.ObjectField("Normal", _snowNormal, typeof(Texture2D), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placeholder Trees", EditorStyles.boldLabel);
        _generateTrees  = EditorGUILayout.Toggle("Generate Trees", _generateTrees);
        if (_generateTrees)
        {
            _treeCount      = EditorGUILayout.IntSlider("Count",       _treeCount, 0, 500);
            _treeMinSpacing = EditorGUILayout.Slider("Min Spacing",    _treeMinSpacing, 1f, 10f);
            _treeMinScale   = EditorGUILayout.Slider("Min Scale",      _treeMinScale, 0.3f, 2f);
            _treeMaxScale   = EditorGUILayout.Slider("Max Scale",      _treeMaxScale, 0.5f, 3f);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Terrain", GUILayout.Height(32)))
            Generate();

        EditorGUILayout.EndScrollView();
    }

    void Generate()
    {
        EnsureFolder("Assets/Terrain");

        var data = new TerrainData();
        data.heightmapResolution = _heightmapRes;
        data.size = new Vector3(_terrainWidth, _terrainHeight, _terrainLength);

        BuildHeightmap(data);
        ApplySnowLayer(data);

        string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Terrain/WinterTerrain.asset");
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();

        var terrainGo = Terrain.CreateTerrainGameObject(data);
        terrainGo.name = "WinterTerrain";
        terrainGo.transform.position = new Vector3(-_terrainWidth * 0.5f, 0, -_terrainLength * 0.5f);

        Undo.RegisterCreatedObjectUndo(terrainGo, "Create Winter Terrain");

        if (_generateTrees)
            PlaceTrees(terrainGo);

        Selection.activeGameObject = terrainGo;
        Debug.Log("[WinterTerrain] Terrain generated at " + assetPath);
    }

    void BuildHeightmap(TerrainData data)
    {
        int res = data.heightmapResolution;
        float[,] heights = new float[res, res];
        float cx = res * 0.5f;
        float cy = res * 0.5f;
        float clearWorldR = _clearingRadius / _terrainWidth * res;
        float smoothWorldR = _clearingSmoothing / _terrainWidth * res;
        float flat = _hillAmplitude * 0.15f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseVal  = 0f;
                float maxVal    = 0f;

                for (int o = 0; o < _noiseOctaves; o++)
                {
                    float sx = (x + _noiseSeed) * _noiseScale * frequency;
                    float sy = (y + _noiseSeed) * _noiseScale * frequency;
                    noiseVal += Mathf.PerlinNoise(sx, sy) * amplitude;
                    maxVal   += amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }

                float h = (noiseVal / maxVal) * _hillAmplitude;

                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float blend = Mathf.SmoothStep(0f, 1f, (dist - clearWorldR) / smoothWorldR);
                h = Mathf.Lerp(flat, h, blend);

                heights[y, x] = h;
            }
        }

        data.SetHeights(0, 0, heights);
    }

    void ApplySnowLayer(TerrainData data)
    {
        var layer = new TerrainLayer();
        layer.tileSize = new Vector2(10, 10);

        if (_snowAlbedo != null)
            layer.diffuseTexture = _snowAlbedo;
        if (_snowNormal != null)
            layer.normalMapTexture = _snowNormal;

        string layerPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Terrain/SnowLayer.asset");
        AssetDatabase.CreateAsset(layer, layerPath);

        data.terrainLayers = new TerrainLayer[] { layer };

        int mapRes = data.alphamapResolution;
        float[,,] alphas = new float[mapRes, mapRes, 1];
        for (int y = 0; y < mapRes; y++)
            for (int x = 0; x < mapRes; x++)
                alphas[y, x, 0] = 1f;
        data.SetAlphamaps(0, 0, alphas);
    }

    void PlaceTrees(GameObject terrainGo)
    {
        var terrain = terrainGo.GetComponent<Terrain>();
        var parent = new GameObject("Trees");
        parent.transform.SetParent(terrainGo.transform, false);
        Undo.RegisterCreatedObjectUndo(parent, "Create Trees");

        Material trunkMat  = CreateMat("Assets/Terrain/TrunkMat.mat",  new Color(0.25f, 0.15f, 0.07f));
        Material foliageMat = CreateMat("Assets/Terrain/FoliageMat.mat", new Color(0.08f, 0.18f, 0.06f));
        Material snowCapMat = CreateMat("Assets/Terrain/SnowCapMat.mat", Color.white);

        var placed = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;

        while (placed.Count < _treeCount && attempts < _treeCount * 10)
        {
            attempts++;
            float rx = Random.Range(0f, (float)_terrainWidth);
            float rz = Random.Range(0f, (float)_terrainLength);
            Vector3 world = terrainGo.transform.position + new Vector3(rx, 0, rz);

            float distCenter = Vector2.Distance(
                new Vector2(world.x, world.z), Vector2.zero);
            if (distCenter < _clearingRadius + 3f) continue;

            bool tooClose = false;
            foreach (var p in placed)
            {
                if (Vector2.Distance(new Vector2(world.x, world.z),
                    new Vector2(p.x, p.z)) < _treeMinSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            world.y = terrain.SampleHeight(world);
            placed.Add(world);

            float scale = Random.Range(_treeMinScale, _treeMaxScale);
            var tree = BuildPine(trunkMat, foliageMat, snowCapMat, scale);
            tree.transform.position = world;
            tree.transform.SetParent(parent.transform, true);
        }
    }

    GameObject BuildPine(Material trunk, Material foliage, Material snow, float scale)
    {
        var root = new GameObject("Pine");

        var trunkGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunkGo.name = "Trunk";
        trunkGo.transform.SetParent(root.transform);
        trunkGo.transform.localScale = new Vector3(0.3f, 2f, 0.3f) * scale;
        trunkGo.transform.localPosition = new Vector3(0, 2f * scale, 0);
        trunkGo.GetComponent<Renderer>().sharedMaterial = trunk;
        trunkGo.isStatic = true;

        float[] foliageYOffsets = { 2.5f, 3.4f, 4.2f };
        float[] foliageScales   = { 1.6f, 1.2f, 0.8f };

        for (int i = 0; i < 3; i++)
        {
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leaf.name = "Foliage_" + i;
            leaf.transform.SetParent(root.transform);
            float ls = foliageScales[i] * scale;
            leaf.transform.localScale = new Vector3(ls, ls * 0.6f, ls);
            leaf.transform.localPosition = new Vector3(0, foliageYOffsets[i] * scale, 0);
            leaf.GetComponent<Renderer>().sharedMaterial = foliage;
            Object.DestroyImmediate(leaf.GetComponent<Collider>());
            leaf.isStatic = true;
        }

        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "SnowCap";
        cap.transform.SetParent(root.transform);
        cap.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f) * scale;
        cap.transform.localPosition = new Vector3(0, (4.2f + 0.4f) * scale, 0);
        cap.GetComponent<Renderer>().sharedMaterial = snow;
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.isStatic = true;

        return root;
    }

    Material CreateMat(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
