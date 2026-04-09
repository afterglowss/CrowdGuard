// LowPolyMountainSculptor.cs
// Place inside any  Assets/.../Editor/  folder in your Unity project.
// Open via:  Tools > Low-Poly Mountain Sculptor

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class LowPolyMountainSculptor : EditorWindow
{
    // ── Sculpt modes ──────────────────────────────────────────────────────────
    enum SculptMode { Raise, Lower, Flatten, Smooth, CliffSlab, Ledge }

    // ── Settings ──────────────────────────────────────────────────────────────
    GameObject targetObject;
    SculptMode sculptMode = SculptMode.Raise;
    float brushRadius = 2f;
    float brushStrength = 0.8f;
    float brushFalloff = 0.6f;
    float flattenHeight = 0f;

    float cliffHeight = 4f;
    float cliffSteepness = 0.85f;
    int ledgeCount = 3;
    float ledgeDepth = 0.5f;
    float ledgeHeightSpread = 0.4f;

    float noiseScale = 1.2f;
    float microNoiseScale = 3.5f;
    float noiseStrength = 0.35f;
    float microNoiseStr = 0.12f;
    bool randomizeOnRaise = true;
    bool flatShading = true;

    bool enableSnow = true;
    float snowSlopeThresh = 0.72f;
    float snowHeightMin = 0f;
    Color snowColor = new Color(0.93f, 0.95f, 1.00f);
    Color rockColor = new Color(0.46f, 0.44f, 0.42f);
    Color ledgeColor = new Color(0.55f, 0.52f, 0.50f);

    Mesh workingMesh;
    Vector3[] originalVerts;
    bool isPainting = false;
    Vector2 scrollPos;
    int noiseOffset = 0;

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/Low-Poly Mountain Sculptor")]
    static void OpenWindow()
    {
        LowPolyMountainSculptor w = GetWindow<LowPolyMountainSculptor>("Mountain Sculptor");
        w.minSize = new Vector2(300, 620);
        w.noiseOffset = Random.Range(0, 10000);
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (targetObject != null) InitMesh();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        isPainting = false;
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawHeader();
        DrawTargetSection();

        if (targetObject == null)
        {
            EditorGUILayout.HelpBox("Assign a GameObject with a MeshFilter to begin.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawBrushSection();
        DrawCliffSection();
        DrawOrganicSection();
        DrawSnowSection();
        DrawActionsSection();
        EditorGUILayout.EndScrollView();
    }

    void DrawHeader()
    {
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
        s.fontSize = 15;
        s.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Low-Poly Mountain Sculptor", s);
        EditorGUILayout.Space(2);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        EditorGUILayout.Space(6);
    }

    void DrawTargetSection()
    {
        SectionLabel("TARGET MESH");
        GameObject prev = targetObject;
        targetObject = (GameObject)EditorGUILayout.ObjectField("Mesh Object", targetObject, typeof(GameObject), true);
        if (targetObject != prev) InitMesh();
        if (targetObject != null && workingMesh == null) InitMesh();
        if (workingMesh != null)
            EditorGUILayout.LabelField(
                string.Format("  Verts: {0}  Tris: {1}", workingMesh.vertexCount, workingMesh.triangles.Length / 3),
                EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    void DrawBrushSection()
    {
        SectionLabel("SCULPT MODE & BRUSH");
        sculptMode = (SculptMode)GUILayout.SelectionGrid((int)sculptMode,
            new string[] { "Raise", "Lower", "Flatten", "Smooth", "Cliff Slab", "Ledge" }, 3);
        EditorGUILayout.Space(4);
        brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.1f, 20f);
        brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.01f, 3f);
        brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0f, 1f);
        if (sculptMode == SculptMode.Flatten)
            flattenHeight = EditorGUILayout.FloatField("Flatten To Y", flattenHeight);

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = isPainting ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
        if (GUILayout.Button(isPainting ? "SCULPTING ACTIVE (click to stop)" : "Start Sculpting", GUILayout.Height(28)))
        {
            isPainting = !isPainting;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = old;
        EditorGUILayout.Space(4);
    }

    void DrawCliffSection()
    {
        SectionLabel("CLIFF & LEDGE SETTINGS");
        cliffHeight = EditorGUILayout.Slider("Cliff Height", cliffHeight, 1f, 30f);
        cliffSteepness = EditorGUILayout.Slider("Cliff Steepness", cliffSteepness, 0.3f, 1f);
        ledgeCount = EditorGUILayout.IntSlider("Ledge Count", ledgeCount, 0, 8);
        ledgeDepth = EditorGUILayout.Slider("Ledge Depth", ledgeDepth, 0.1f, 2f);
        ledgeHeightSpread = EditorGUILayout.Slider("Ledge Spread", ledgeHeightSpread, 0f, 1f);
        EditorGUILayout.HelpBox("Cliff Slab: steep cliff face.\nLedge: horizontal resting shelves.", MessageType.None);
        EditorGUILayout.Space(4);
    }

    void DrawOrganicSection()
    {
        SectionLabel("ORGANIC VARIATION");
        randomizeOnRaise = EditorGUILayout.Toggle("Randomize On Sculpt", randomizeOnRaise);
        flatShading = EditorGUILayout.Toggle("Flat Shading (Low-Poly)", flatShading);
        noiseScale = EditorGUILayout.Slider("Large Noise Scale", noiseScale, 0.1f, 5f);
        noiseStrength = EditorGUILayout.Slider("Large Noise Strength", noiseStrength, 0f, 1f);
        microNoiseScale = EditorGUILayout.Slider("Micro Noise Scale", microNoiseScale, 0.5f, 10f);
        microNoiseStr = EditorGUILayout.Slider("Micro Noise Strength", microNoiseStr, 0f, 0.5f);
        if (GUILayout.Button("Apply Organic Variation to Entire Mesh"))
            ApplyOrganicNoiseAll();
        EditorGUILayout.Space(4);
    }

    void DrawSnowSection()
    {
        SectionLabel("SNOW");
        enableSnow = EditorGUILayout.Toggle("Enable Snow", enableSnow);
        snowSlopeThresh = EditorGUILayout.Slider("Slope Threshold", snowSlopeThresh, 0.3f, 1f);
        snowHeightMin = EditorGUILayout.FloatField("Min Snow Height", snowHeightMin);
        snowColor = EditorGUILayout.ColorField("Snow Color", snowColor);
        rockColor = EditorGUILayout.ColorField("Rock Color", rockColor);
        ledgeColor = EditorGUILayout.ColorField("Ledge Color", ledgeColor);
        if (GUILayout.Button("Bake Vertex Colors Now"))
            BakeVertexColors();
        EditorGUILayout.Space(4);
    }

    void DrawActionsSection()
    {
        SectionLabel("UTILITIES");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Subdivide Mesh")) SubdivideMesh();
        if (GUILayout.Button("Reset Mesh")) ResetMesh();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Mesh Asset")) SaveMeshAsset();
        if (GUILayout.Button("New Noise Seed")) noiseOffset = Random.Range(0, 10000);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }

    // ── Scene GUI ─────────────────────────────────────────────────────────────
    void OnSceneGUI(SceneView sv)
    {
        if (!isPainting || targetObject == null || workingMesh == null) return;

        Event e = Event.current;

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity)) return;

        DrawBrushGizmo(hit.point, hit.normal);
        sv.Repaint();

        bool doSculpt = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                        && e.button == 0 && !e.alt;
        if (doSculpt)
        {
            Undo.RecordObject(workingMesh, "Sculpt Terrain");
            SculptAt(hit.point, e.delta.magnitude * 0.1f + 1f);
            if (enableSnow) BakeVertexColors();
            e.Use();
        }
    }

    void DrawBrushGizmo(Vector3 center, Vector3 normal)
    {
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.5f);
        Handles.DrawWireDisc(center, normal, brushRadius);
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.15f);
        Handles.DrawSolidDisc(center, normal, brushRadius * 0.15f);
        GUIStyle ls = new GUIStyle(EditorStyles.miniLabel);
        ls.normal.textColor = Color.cyan;
        Handles.Label(center + Vector3.up * 0.3f, sculptMode.ToString(), ls);
    }

    // ── Mesh init ─────────────────────────────────────────────────────────────
    void InitMesh()
    {
        if (targetObject == null) return;
        MeshFilter mf = targetObject.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("[Mountain Sculptor] No MeshFilter on target.");
            return;
        }
        workingMesh = Instantiate(mf.sharedMesh);
        workingMesh.name = mf.sharedMesh.name + "_Sculpted";
        mf.sharedMesh = workingMesh;
        originalVerts = (Vector3[])workingMesh.vertices.Clone();
        EnsureCollider();
        if (flatShading) ApplyFlatShading();
    }

    void EnsureCollider()
    {
        MeshCollider mc = targetObject.GetComponent<MeshCollider>();
        if (mc == null) mc = targetObject.AddComponent<MeshCollider>();
        mc.sharedMesh = workingMesh;
    }

    // ── Core sculpt ───────────────────────────────────────────────────────────
    void SculptAt(Vector3 worldPos, float deltaScale)
    {
        Vector3[] verts = workingMesh.vertices;
        int[] tris = workingMesh.triangles;
        Transform tr = targetObject.transform;
        Vector3 localCenter = tr.InverseTransformPoint(worldPos);
        float dt = brushStrength * 0.015f * deltaScale;
        float sc = (tr.localScale.x > 0f) ? tr.localScale.x : 1f;
        float r2 = (brushRadius / sc) * (brushRadius / sc);

        switch (sculptMode)
        {
            case SculptMode.Raise:
            case SculptMode.Lower:
                {
                    float dir = (sculptMode == SculptMode.Raise) ? 1f : -1f;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float d2 = SqDist2D(verts[i], localCenter);
                        if (d2 > r2) continue;
                        float w = BrushWeight(Mathf.Sqrt(d2 / r2));
                        verts[i].y += dir * dt * w * (1f + SampleOrganic(verts[i]));
                    }
                    break;
                }
            case SculptMode.Flatten:
                {
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float d2 = SqDist2D(verts[i], localCenter);
                        if (d2 > r2) continue;
                        float w = BrushWeight(Mathf.Sqrt(d2 / r2));
                        verts[i].y = Mathf.Lerp(verts[i].y, flattenHeight, w * dt * 10f);
                    }
                    break;
                }
            case SculptMode.Smooth:
                {
                    Vector3[] smoothed = (Vector3[])verts.Clone();
                    Dictionary<int, List<int>> nb = BuildNeighborMap(verts, tris);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float d2 = SqDist2D(verts[i], localCenter);
                        if (d2 > r2) continue;
                        float w = BrushWeight(Mathf.Sqrt(d2 / r2));
                        List<int> neighbors;
                        if (!nb.TryGetValue(i, out neighbors) || neighbors.Count == 0) continue;
                        float sum = 0f;
                        foreach (int n in neighbors) sum += verts[n].y;
                        float avg = sum / neighbors.Count;
                        smoothed[i].y = Mathf.Lerp(verts[i].y, avg, w * dt * 8f);
                    }
                    verts = smoothed;
                    break;
                }
            case SculptMode.CliffSlab:
                {
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float dx = verts[i].x - localCenter.x;
                        float dz = verts[i].z - localCenter.z;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        float dr = d / Mathf.Sqrt(r2);
                        if (dr > 1f) continue;
                        float w = BrushWeight(dr);
                        float exp = 1f / Mathf.Max(cliffSteepness, 0.01f);
                        float targetY = cliffHeight * (1f - Mathf.Pow(dr, exp));
                        targetY += SampleOrganic(verts[i]) * cliffHeight * 0.18f;
                        verts[i].y = Mathf.Lerp(verts[i].y, targetY, w * dt * 6f);
                    }
                    break;
                }
            case SculptMode.Ledge:
                {
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float dx = verts[i].x - localCenter.x;
                        float dz = verts[i].z - localCenter.z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 > r2) continue;
                        float w = BrushWeight(Mathf.Sqrt(d2 / r2));
                        float ledgeY = NearestLedgeY(verts[i].y);
                        float diff = Mathf.Abs(verts[i].y - ledgeY);
                        if (diff < ledgeDepth * 1.5f)
                        {
                            verts[i].y += (ledgeY - verts[i].y) * w * dt * 5f;
                            float mn = SampleMicro(verts[i]);
                            verts[i].x += dx * -0.005f * w * (1f + mn);
                            verts[i].z += dz * -0.005f * w * (1f + mn);
                        }
                    }
                    break;
                }
        }

        workingMesh.vertices = verts;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
        if (randomizeOnRaise) ApplyOrganicNoiseRegion(verts, localCenter, r2);
        if (flatShading) ApplyFlatShading();
        UpdateCollider();
    }

    // ── Ledge ─────────────────────────────────────────────────────────────────
    float NearestLedgeY(float y)
    {
        float best = 0f, bestDist = float.MaxValue;
        for (int l = 0; l < ledgeCount; l++)
        {
            float frac = (float)(l + 1) / (ledgeCount + 1);
            float rnd = (Mathf.PerlinNoise(l * 137.3f + noiseOffset, 0.5f) - 0.5f) * ledgeHeightSpread;
            float ledgeY = (frac + rnd) * cliffHeight;
            float dist = Mathf.Abs(y - ledgeY);
            if (dist < bestDist) { bestDist = dist; best = ledgeY; }
        }
        return best;
    }

    // ── Noise ─────────────────────────────────────────────────────────────────
    float SampleOrganic(Vector3 v)
    {
        float x = v.x + noiseOffset;
        float z = v.z + noiseOffset;
        float n = Mathf.PerlinNoise(x * noiseScale * 0.10f, z * noiseScale * 0.10f)
                + Mathf.PerlinNoise(x * noiseScale * 0.23f, z * noiseScale * 0.23f) * 0.50f
                + Mathf.PerlinNoise(x * noiseScale * 0.61f, z * noiseScale * 0.61f) * 0.25f;
        return n * noiseStrength;
    }

    float SampleMicro(Vector3 v)
    {
        float x = v.x + noiseOffset * 0.7f;
        float z = v.z + noiseOffset * 0.7f;
        return Mathf.PerlinNoise(x * microNoiseScale * 0.1f, z * microNoiseScale * 0.1f) * microNoiseStr;
    }

    void DisplaceVerts(Vector3[] verts, Vector3 localCenter, float radius2, bool applyAll)
    {
        for (int i = 0; i < verts.Length; i++)
        {
            if (!applyAll)
            {
                float dx = verts[i].x - localCenter.x;
                float dz = verts[i].z - localCenter.z;
                if (dx * dx + dz * dz > radius2) continue;
            }
            float micro = SampleMicro(verts[i]);
            verts[i].x += (Mathf.PerlinNoise(verts[i].z * noiseScale * 0.07f + noiseOffset + 300f, verts[i].y * 0.3f) - 0.5f)
                          * noiseStrength * 0.4f * (1f + micro);
            verts[i].z += (Mathf.PerlinNoise(verts[i].x * noiseScale * 0.07f + noiseOffset + 600f, verts[i].y * 0.3f) - 0.5f)
                          * noiseStrength * 0.4f * (1f + micro);
            verts[i].y += micro * 0.3f;
        }
    }

    void ApplyOrganicNoiseAll()
    {
        Vector3[] verts = workingMesh.vertices;
        DisplaceVerts(verts, Vector3.zero, 0f, true);
        workingMesh.vertices = verts;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    void ApplyOrganicNoiseRegion(Vector3[] verts, Vector3 localCenter, float radius2)
    {
        DisplaceVerts(verts, localCenter, radius2, false);
        workingMesh.vertices = verts;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    // ── Flat shading ──────────────────────────────────────────────────────────
    void ApplyFlatShading()
    {
        Vector3[] sv = workingMesh.vertices;
        int[] st = workingMesh.triangles;
        Vector2[] su = workingMesh.uv;
        bool hu = (su != null && su.Length == sv.Length);

        int c = st.Length;
        Vector3[] nv = new Vector3[c];
        int[] nt = new int[c];
        Vector2[] nu = hu ? new Vector2[c] : null;

        for (int i = 0; i < c; i++)
        {
            nv[i] = sv[st[i]];
            nt[i] = i;
            if (hu) nu[i] = su[st[i]];
        }

        workingMesh.Clear();
        workingMesh.vertices = nv;
        workingMesh.triangles = nt;
        if (hu) workingMesh.uv = nu;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    // ── Vertex colour snow ────────────────────────────────────────────────────
    void BakeVertexColors()
    {
        Vector3[] verts = workingMesh.vertices;
        Vector3[] normals = workingMesh.normals;
        Color[] colors = new Color[verts.Length];
        Transform tr = targetObject.transform;

        for (int i = 0; i + 2 < verts.Length; i += 3)
        {
            Vector3 worldV = tr.TransformPoint(verts[i]);
            Vector3 worldN = tr.TransformDirection(normals[i]).normalized;
            float upDot = Vector3.Dot(worldN, Vector3.up);
            bool isSnow = worldV.y >= snowHeightMin && upDot >= snowSlopeThresh;
            float ld = Mathf.Abs(NearestLedgeY(verts[i].y) - verts[i].y);
            bool isLedge = ld < ledgeDepth * 0.8f && upDot > 0.45f;

            Color c = isLedge ? ledgeColor : (isSnow ? snowColor : rockColor);
            float v = Mathf.PerlinNoise(worldV.x * 2.3f + noiseOffset, worldV.z * 2.3f + noiseOffset) * 0.06f - 0.03f;
            c.r = Mathf.Clamp01(c.r + v);
            c.g = Mathf.Clamp01(c.g + v * 0.8f);
            c.b = Mathf.Clamp01(c.b + v * 0.6f);
            colors[i] = colors[i + 1] = colors[i + 2] = c;
        }
        workingMesh.colors = colors;
    }

    // ── Subdivide ─────────────────────────────────────────────────────────────
    void SubdivideMesh()
    {
        Undo.RecordObject(workingMesh, "Subdivide Mesh");
        List<Vector3> verts = new List<Vector3>(workingMesh.vertices);
        List<int> srcTris = new List<int>(workingMesh.triangles);
        Dictionary<long, int> cache = new Dictionary<long, int>();
        List<int> newTris = new List<int>();

        int count = srcTris.Count / 3;
        for (int i = 0; i < count; i++)
        {
            int i0 = srcTris[i * 3];
            int i1 = srcTris[i * 3 + 1];
            int i2 = srcTris[i * 3 + 2];
            int m01 = MidpointIndex(i0, i1, verts, cache);
            int m12 = MidpointIndex(i1, i2, verts, cache);
            int m20 = MidpointIndex(i2, i0, verts, cache);
            newTris.Add(i0); newTris.Add(m01); newTris.Add(m20);
            newTris.Add(m01); newTris.Add(i1); newTris.Add(m12);
            newTris.Add(m20); newTris.Add(m12); newTris.Add(i2);
            newTris.Add(m01); newTris.Add(m12); newTris.Add(m20);
        }

        workingMesh.Clear();
        workingMesh.vertices = verts.ToArray();
        workingMesh.triangles = newTris.ToArray();
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
        originalVerts = (Vector3[])workingMesh.vertices.Clone();
        if (flatShading) ApplyFlatShading();
        UpdateCollider();
        Debug.Log(string.Format("[Mountain Sculptor] Subdivided -> {0} verts, {1} tris",
            workingMesh.vertexCount, workingMesh.triangles.Length / 3));
    }

    int MidpointIndex(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        int lo = (a < b) ? a : b;
        int hi = (a < b) ? b : a;
        long key = ((long)lo << 32) | (uint)hi;
        int idx;
        if (cache.TryGetValue(key, out idx)) return idx;
        idx = verts.Count;
        verts.Add((verts[a] + verts[b]) * 0.5f);
        cache[key] = idx;
        return idx;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────
    float SqDist2D(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    float BrushWeight(float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Pow(1f - t, 1f + brushFalloff * 3f);
    }

    Dictionary<int, List<int>> BuildNeighborMap(Vector3[] verts, int[] tris)
    {
        Dictionary<int, List<int>> map = new Dictionary<int, List<int>>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int vi = tris[i + k];
                if (!map.ContainsKey(vi)) map[vi] = new List<int>();
                map[vi].Add(tris[i + (k + 1) % 3]);
                map[vi].Add(tris[i + (k + 2) % 3]);
            }
        }
        return map;
    }

    void UpdateCollider()
    {
        MeshCollider mc = targetObject.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = workingMesh;
    }

    void ResetMesh()
    {
        if (workingMesh == null || originalVerts == null) return;
        Undo.RecordObject(workingMesh, "Reset Mesh");
        workingMesh.vertices = (Vector3[])originalVerts.Clone();
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
        workingMesh.colors = new Color[workingMesh.vertexCount];
        UpdateCollider();
    }

    void SaveMeshAsset()
    {
        if (workingMesh == null) return;
        string path = EditorUtility.SaveFilePanelInProject("Save Mesh", workingMesh.name, "asset", "Save sculpted mesh");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(workingMesh, path);
        AssetDatabase.SaveAssets();
        Debug.Log("[Mountain Sculptor] Saved to " + path);
    }

    void SectionLabel(string text)
    {
        EditorGUILayout.Space(2);
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
        s.fontSize = 10;
        s.normal.textColor = new Color(0.55f, 0.75f, 1f);
        EditorGUILayout.LabelField(text, s);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.55f, 0.75f, 1f, 0.2f));
        EditorGUILayout.Space(3);
    }
}