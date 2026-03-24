using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
//  Shared Delaunay / helper utilities used by both generators
// ═══════════════════════════════════════════════════════════════════════════
internal static class MeshGenUtils
{
    // ── Bowyer-Watson Delaunay (2-D, XZ plane) ─────────────────────────────
    internal struct DelTri
    {
        public int a, b, c;
        public Vector2 cc;
        public float rr;

        public DelTri(int a, int b, int c, List<Vector2> pts)
        {
            this.a = a; this.b = b; this.c = c;
            Circumcircle(pts[a], pts[b], pts[c], out cc, out rr);
        }

        public bool InCircumcircle(Vector2 p)
        {
            float dx = p.x - cc.x, dy = p.y - cc.y;
            return dx * dx + dy * dy < rr - 1e-7f;
        }

        static void Circumcircle(Vector2 A, Vector2 B, Vector2 C,
                                  out Vector2 centre, out float rSq)
        {
            float ax = B.x - A.x, ay = B.y - A.y;
            float bx = C.x - A.x, by = C.y - A.y;
            float D = 2f * (ax * by - ay * bx);
            if (Mathf.Abs(D) < 1e-10f) { centre = (A + B + C) / 3f; rSq = float.MaxValue; return; }
            float a2 = ax * ax + ay * ay, b2 = bx * bx + by * by;
            float ux = (by * a2 - ay * b2) / D;
            float uy = (ax * b2 - bx * a2) / D;
            centre = new Vector2(A.x + ux, A.y + uy);
            rSq = ux * ux + uy * uy;
        }
    }

    /// Returns CW-wound triangles (Unity convention: right-hand rule, Y-up).
    /// Each element is int[3] = { indexA, indexB, indexC }.
    public static List<int[]> BowyerWatson(List<Vector2> pts)
    {
        float minX = float.MaxValue, minY = float.MaxValue,
              maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            if (p.x < minX) minX = p.x; if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x; if (p.y > maxY) maxY = p.y;
        }
        float diam = Mathf.Max(maxX - minX, maxY - minY) * 10f;
        float midX = (minX + maxX) * 0.5f;

        var all = new List<Vector2>(pts)
        {
            new Vector2(midX - diam, minY - diam),
            new Vector2(midX,        maxY + diam),
            new Vector2(midX + diam, minY - diam)
        };
        int sA = all.Count - 3, sB = sA + 1, sC = sA + 2;

        var tris = new List<DelTri> { new DelTri(sA, sB, sC, all) };
        var bad = new List<DelTri>();
        var boundary = new List<(int, int)>();

        for (int pi = 0; pi < pts.Count; pi++)
        {
            Vector2 pt = all[pi];

            bad.Clear();
            foreach (var t in tris)
                if (t.InCircumcircle(pt)) bad.Add(t);

            boundary.Clear();
            foreach (var t in bad)
            {
                AddEdgeIfUnique(boundary, bad, t, t.a, t.b);
                AddEdgeIfUnique(boundary, bad, t, t.b, t.c);
                AddEdgeIfUnique(boundary, bad, t, t.c, t.a);
            }

            foreach (var t in bad) tris.Remove(t);
            foreach (var e in boundary)
                tris.Add(new DelTri(e.Item1, e.Item2, pi, all));
        }

        var result = new List<int[]>(tris.Count);
        foreach (var t in tris)
        {
            if (t.a >= sA || t.b >= sA || t.c >= sA) continue;

            // Unity is left-handed (Y-up). For an upward-facing surface
            // the normal must point +Y. Cross(B-A, C-A) points +Y when
            // the triangle is wound CLOCKWISE in XZ viewed from above.
            Vector2 pa = all[t.a], pb = all[t.b], pc = all[t.c];
            // 2-D cross: positive = CCW in standard math = CW in Unity XZ
            float cross = (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
            if (cross > 0)
                result.Add(new[] { t.a, t.c, t.b });   // already CW in Unity XZ
            else
                result.Add(new[] { t.a, t.b, t.c });   // flip to CW
        }
        return result;
    }

    static void AddEdgeIfUnique(List<(int, int)> boundary, List<DelTri> bad,
                                 DelTri owner, int e0, int e1)
    {
        foreach (var t in bad)
        {
            if (t.Equals(owner)) continue;
            if ((t.a == e0 || t.b == e0 || t.c == e0) && (t.a == e1 || t.b == e1 || t.c == e1)) return;
        }
        boundary.Add((e0, e1));
    }

    // ── Flat-shaded mesh assembly ──────────────────────────────────────────
    /// <param name="pts3d">All 3-D positions indexed by the triangle lists.</param>
    /// <param name="triangles">Each int[3] is CW-wound in XZ.</param>
    /// <param name="colorFn">Given the face centroid Y, returns the vertex colour.</param>
    public static Mesh AssembleFlatMesh(string name,
                                         Vector3[] pts3d,
                                         List<int[]> triangles,
                                         bool useVertexColor,
                                         System.Func<float, Color> colorFn)
    {
        var verts = new List<Vector3>(triangles.Count * 3);
        var nrms = new List<Vector3>(triangles.Count * 3);
        var uvs = new List<Vector2>(triangles.Count * 3);
        var cols = new List<Color>(triangles.Count * 3);
        var triIdx = new List<int>(triangles.Count * 3);

        // Compute mesh extents for UV normalisation
        float minX = float.MaxValue, maxX = float.MinValue,
              minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in pts3d)
        {
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
        }
        float rX = Mathf.Max(maxX - minX, 1e-4f), rZ = Mathf.Max(maxZ - minZ, 1e-4f);

        foreach (var tri in triangles)
        {
            Vector3 a = pts3d[tri[0]], b = pts3d[tri[1]], c = pts3d[tri[2]];

            // Normal via cross product – CW winding gives +Y for upward faces
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;

            float avgY = (a.y + b.y + c.y) / 3f;
            Color col = colorFn(avgY);

            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            nrms.Add(n); nrms.Add(n); nrms.Add(n);
            uvs.Add(new Vector2((a.x - minX) / rX, (a.z - minZ) / rZ));
            uvs.Add(new Vector2((b.x - minX) / rX, (b.z - minZ) / rZ));
            uvs.Add(new Vector2((c.x - minX) / rX, (c.z - minZ) / rZ));
            cols.Add(col); cols.Add(col); cols.Add(col);
            triIdx.Add(idx); triIdx.Add(idx + 1); triIdx.Add(idx + 2);
        }

        var mesh = new Mesh { name = name };
        if (verts.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetNormals(nrms);
        mesh.SetUVs(0, uvs);
        if (useVertexColor) mesh.SetColors(cols);
        mesh.SetTriangles(triIdx, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── Asset saving ───────────────────────────────────────────────────────
    public static void SaveMeshAsset(Mesh mesh, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            var parts = folder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
        string path = folder + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); }
        else AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MeshGen] Saved → {path}");
    }

    // ── Fractal Perlin noise ───────────────────────────────────────────────
    public static float FractalNoise(float nx, float nz, int octaves,
                                      float scale, float persist, float lacunarity,
                                      Vector2[] offsets)
    {
        float amp = 1f, freq = 1f, val = 0f, maxA = 0f;
        for (int o = 0; o < octaves; o++)
        {
            val += Mathf.PerlinNoise(nx * scale * freq + offsets[o].x,
                                       nz * scale * freq + offsets[o].y) * amp;
            maxA += amp;
            amp *= persist;
            freq *= lacunarity;
        }
        return val / maxA;
    }

    public static Vector2[] MakeOctaveOffsets(int octaves, int seed)
    {
        var saved = Random.state;
        Random.InitState(seed);
        var offs = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
            offs[i] = new Vector2(Random.Range(-9999f, 9999f), Random.Range(-9999f, 9999f));
        Random.state = saved;
        return offs;
    }
}


// ═══════════════════════════════════════════════════════════════════════════
//  MOUNTAIN GENERATOR
// ═══════════════════════════════════════════════════════════════════════════
public class LowPolyMountainGenerator : EditorWindow
{
    // ── Shape ──────────────────────────────────────────────────────────────
    private int resolution = 20;
    private float width = 10f, depth = 10f;
    private float peakHeight = 6f, baseHeight = 0f;

    // ── Triangulation ──────────────────────────────────────────────────────
    private enum TriMode { RegularGrid, JitteredDelaunay, FullyRandomDelaunay }
    private TriMode triMode = TriMode.JitteredDelaunay;
    private float jitter = 0.65f;
    private int randomPts = 300;

    // ── Noise ──────────────────────────────────────────────────────────────
    private int seed = 42;
    private float nScale = 0.35f, nStrength = 1.8f;
    private int nOctaves = 3;
    private float nPersist = 0.5f, nLacunarity = 2f;

    // ── Peak ───────────────────────────────────────────────────────────────
    private float sharpness = 2.5f;
    private float peakX = 0f, peakZ = 0f;
    private bool multiPeak = false;
    private int peakCount = 2;
    private float peakSpread = 3f;

    // ── Shading ────────────────────────────────────────────────────────────
    private bool useVC = false;
    private Color snowCol = new Color(0.95f, 0.97f, 1.0f);
    private Color rockCol = new Color(0.45f, 0.40f, 0.35f);
    private Color grassCol = new Color(0.30f, 0.55f, 0.25f);
    private float snowLine = 0.75f, grassLine = 0.30f;
    private bool useMat = false;
    private Material mat = null;

    // ── Output ─────────────────────────────────────────────────────────────
    private string meshName = "LowPolyMountain";
    private bool saveAsset = false;
    private string saveFolder = "Assets/Meshes";

    // ── UI ─────────────────────────────────────────────────────────────────
    private Vector2 scroll;
    private bool sShape = true, sTri = true, sNoise = true, sPeak = true, sShade = true, sOut = true;
    private GameObject previewObj;
    private GUIStyle hdrStyle;
    private bool stylesOk;

    [MenuItem("Tools/Low Poly Mountain Generator")]
    public static void ShowWindow() =>
        GetWindow<LowPolyMountainGenerator>("⛰  Mountain Gen").minSize = new Vector2(340, 560);

    void InitStyles()
    {
        if (stylesOk) return; stylesOk = true;
        hdrStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
        hdrStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
    }

    void OnGUI()
    {
        InitStyles();
        var r = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.12f, 0.18f, 0.28f));
        GUI.Label(new Rect(r.x + 10, r.y + 6, r.width, 28), "⛰   Low Poly Mountain Generator", hdrStyle);
        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        sShape = Sec(sShape, "Terrain Shape", ShapeUI);
        sTri = Sec(sTri, "Triangulation", TriUI);
        sNoise = Sec(sNoise, "Noise / Detail", NoiseUI);
        sPeak = Sec(sPeak, "Peak Settings", PeakUI);
        sShade = Sec(sShade, "Shading & Colors", ShadeUI);
        sOut = Sec(sOut, "Output", OutUI);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
        if (GUILayout.Button("▶  Generate in Scene", GUILayout.Height(34))) Generate(false);
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f);
        if (GUILayout.Button("↺  Randomise Seed", GUILayout.Height(34))) { seed = Random.Range(0, 99999); Generate(false); }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        if (saveAsset)
        {
            GUI.backgroundColor = new Color(0.85f, 0.65f, 0.2f);
            if (GUILayout.Button("💾  Save Mesh Asset", GUILayout.Height(28))) Generate(true);
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.EndScrollView();
    }

    bool Sec(bool open, string label, System.Action body)
    {
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, label);
        if (open) { EditorGUI.indentLevel++; body(); EditorGUI.indentLevel--; EditorGUILayout.Space(2); }
        EditorGUILayout.EndFoldoutHeaderGroup();
        return open;
    }

    void ShapeUI()
    {
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 4, 80);
        width = EditorGUILayout.Slider("Width", width, 1f, 50f);
        depth = EditorGUILayout.Slider("Depth", depth, 1f, 50f);
        peakHeight = EditorGUILayout.Slider("Peak Height", peakHeight, 0.5f, 30f);
        baseHeight = EditorGUILayout.Slider("Base Height", baseHeight, -5f, 5f);
    }

    void TriUI()
    {
        triMode = (TriMode)EditorGUILayout.EnumPopup("Mode", triMode);
        EditorGUILayout.HelpBox(
            triMode == TriMode.RegularGrid ? "Classic diagonal-split quads." :
            triMode == TriMode.JitteredDelaunay ? "Grid points nudged before Delaunay. Best organic balance." :
            "Random scatter + Delaunay. Most organic.", MessageType.None);
        if (triMode == TriMode.JitteredDelaunay) jitter = EditorGUILayout.Slider("Jitter", jitter, 0f, 1f);
        if (triMode == TriMode.FullyRandomDelaunay) randomPts = EditorGUILayout.IntSlider("Points", randomPts, 20, 2000);
    }

    void NoiseUI()
    {
        seed = EditorGUILayout.IntField("Seed", seed);
        nScale = EditorGUILayout.Slider("Scale", nScale, 0.01f, 2f);
        nStrength = EditorGUILayout.Slider("Strength", nStrength, 0f, 5f);
        nOctaves = EditorGUILayout.IntSlider("Octaves", nOctaves, 1, 6);
        nPersist = EditorGUILayout.Slider("Persistence", nPersist, 0.1f, 1f);
        nLacunarity = EditorGUILayout.Slider("Lacunarity", nLacunarity, 1f, 4f);
    }

    void PeakUI()
    {
        sharpness = EditorGUILayout.Slider("Sharpness", sharpness, 0.5f, 8f);
        peakX = EditorGUILayout.Slider("Peak Offset X", peakX, -1f, 1f);
        peakZ = EditorGUILayout.Slider("Peak Offset Z", peakZ, -1f, 1f);
        multiPeak = EditorGUILayout.Toggle("Multi Peak", multiPeak);
        if (multiPeak)
        {
            EditorGUI.indentLevel++;
            peakCount = EditorGUILayout.IntSlider("Count", peakCount, 2, 6);
            peakSpread = EditorGUILayout.Slider("Spread", peakSpread, 0.5f, 8f);
            EditorGUI.indentLevel--;
        }
    }

    void ShadeUI()
    {
        useVC = EditorGUILayout.Toggle("Vertex Colors", useVC);
        if (useVC)
        {
            EditorGUI.indentLevel++;
            snowCol = EditorGUILayout.ColorField("Snow", snowCol);
            rockCol = EditorGUILayout.ColorField("Rock", rockCol);
            grassCol = EditorGUILayout.ColorField("Grass", grassCol);
            snowLine = EditorGUILayout.Slider("Snow Line", snowLine, 0f, 1f);
            grassLine = EditorGUILayout.Slider("Grass Line", grassLine, 0f, 1f);
            EditorGUI.indentLevel--;
        }
        useMat = EditorGUILayout.Toggle("Assign Material", useMat);
        if (useMat)
        {
            EditorGUI.indentLevel++;
            mat = (Material)EditorGUILayout.ObjectField("Material", mat, typeof(Material), false);
            EditorGUI.indentLevel--;
        }
    }

    void OutUI()
    {
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);
        saveAsset = EditorGUILayout.Toggle("Save Mesh Asset", saveAsset);
        if (saveAsset)
        {
            EditorGUI.indentLevel++;
            saveFolder = EditorGUILayout.TextField("Folder", saveFolder);
            EditorGUI.indentLevel--;
        }
    }

    // ── Generation ────────────────────────────────────────────────────────
    void Generate(bool save)
    {
        Mesh mesh = BuildMesh();
        if (save) MeshGenUtils.SaveMeshAsset(mesh, saveFolder);

        if (previewObj == null) { previewObj = new GameObject(meshName); previewObj.AddComponent<MeshFilter>(); previewObj.AddComponent<MeshRenderer>(); }
        previewObj.name = meshName;
        previewObj.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = previewObj.GetComponent<MeshRenderer>();
        if (useMat && mat != null) mr.sharedMaterial = mat;
        else if (mr.sharedMaterial == null) mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        Selection.activeGameObject = previewObj;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.RegisterCreatedObjectUndo(previewObj, "Generate Mountain");
    }

    Mesh BuildMesh()
    {
        var offs = MeshGenUtils.MakeOctaveOffsets(nOctaves, seed);

        // Peak list (normalised -1..1)
        Vector2[] peaks = multiPeak ? new Vector2[peakCount] : new[] { new Vector2(peakX, peakZ) };
        if (multiPeak) for (int p = 0; p < peakCount; p++)
            {
                float a = p * Mathf.PI * 2f / peakCount, r = peakSpread / Mathf.Max(width, depth);
                peaks[p] = new Vector2(peakX + Mathf.Cos(a) * r, peakZ + Mathf.Sin(a) * r);
            }

        // 2-D point set
        List<Vector2> pts2d;
        var savedRng = Random.state;
        Random.InitState(seed + 1);
        if (triMode == TriMode.RegularGrid || triMode == TriMode.JitteredDelaunay)
        {
            int vx = resolution + 1, vz = resolution + 1;
            float cW = width / resolution, cD = depth / resolution;
            float hJW = cW * jitter * 0.5f, hJD = cD * jitter * 0.5f;
            pts2d = new List<Vector2>(vx * vz);
            for (int z = 0; z < vz; z++) for (int x = 0; x < vx; x++)
                {
                    float px = (x / (float)resolution - 0.5f) * width;
                    float pz = (z / (float)resolution - 0.5f) * depth;
                    bool border = (x == 0 || x == resolution || z == 0 || z == resolution);
                    if (triMode == TriMode.JitteredDelaunay && !border) { px += Random.Range(-hJW, hJW); pz += Random.Range(-hJD, hJD); }
                    pts2d.Add(new Vector2(px, pz));
                }
        }
        else
        {
            pts2d = new List<Vector2>(randomPts + 4)
            {
                new Vector2(-0.5f*width,-0.5f*depth), new Vector2(0.5f*width,-0.5f*depth),
                new Vector2( 0.5f*width, 0.5f*depth), new Vector2(-0.5f*width, 0.5f*depth)
            };
            for (int i = 0; i < randomPts; i++)
                pts2d.Add(new Vector2(Random.Range(-0.5f * width, 0.5f * width), Random.Range(-0.5f * depth, 0.5f * depth)));
        }
        Random.state = savedRng;

        // Height function
        float H(Vector2 p)
        {
            float nx = p.x / (0.5f * width), nz = p.y / (0.5f * depth);
            float falloff = 0f;
            foreach (var pk in peaks) { float dx = nx - pk.x, dz = nz - pk.y; float f = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz)), sharpness); if (f > falloff) falloff = f; }
            float nv = MeshGenUtils.FractalNoise(nx, nz, nOctaves, nScale, nPersist, nLacunarity, offs);
            return falloff * peakHeight + nv * nStrength * falloff + baseHeight;
        }

        var pts3d = new Vector3[pts2d.Count];
        for (int i = 0; i < pts2d.Count; i++) pts3d[i] = new Vector3(pts2d[i].x, H(pts2d[i]), pts2d[i].y);

        var tris = triMode == TriMode.RegularGrid ? GridTris() : MeshGenUtils.BowyerWatson(pts2d);

        Color ColorFn(float y) { float t = Mathf.InverseLerp(baseHeight, peakHeight, y); return t > snowLine ? snowCol : t > grassLine ? rockCol : grassCol; }

        return MeshGenUtils.AssembleFlatMesh(meshName, pts3d, tris, useVC, ColorFn);
    }

    List<int[]> GridTris()
    {
        int vx = resolution + 1;
        var t = new List<int[]>(resolution * resolution * 2);
        for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                int i00 = z * vx + x, i10 = i00 + 1, i01 = i00 + vx, i11 = i01 + 1;
                // CW winding in XZ → normal points up (+Y)
                t.Add(new[] { i00, i01, i10 });
                t.Add(new[] { i10, i01, i11 });
            }
        return t;
    }
}


// ═══════════════════════════════════════════════════════════════════════════
//  CLIFF GENERATOR
// ═══════════════════════════════════════════════════════════════════════════
public class LowPolyCliffGenerator : EditorWindow
{
    // ── Shape ──────────────────────────────────────────────────────────────
    private int segments = 24;    // horizontal segments along cliff face
    private int layers = 8;     // vertical layers of ledges
    private float cliffWidth = 12f;
    private float cliffHeight = 8f;
    private float cliffDepth = 3f;    // thickness / depth of the rock mass

    // ── Jaggedness ─────────────────────────────────────────────────────────
    private float xJitter = 0.6f;  // horizontal irregularity on face
    private float yJitter = 0.5f;  // vertical ledge height variance
    private float depthJitter = 0.5f;  // how much ledge edges protrude/recede
    private float overhangAmt = 0.3f;  // max negative-depth overhang
    private bool addTopSurface = true;

    // ── Triangulation ──────────────────────────────────────────────────────
    private enum FaceMode { RegularGrid, JitteredDelaunay }
    private FaceMode faceMode = FaceMode.JitteredDelaunay;
    private float faceJitter = 0.5f;

    // ── Noise ──────────────────────────────────────────────────────────────
    private int seed = 77;
    private float nScale = 0.4f, nStrength = 0.6f;
    private int nOctaves = 3;
    private float nPersist = 0.5f, nLacunarity = 2f;

    // ── Shading ────────────────────────────────────────────────────────────
    private bool useVC = false;
    private Color topCol = new Color(0.40f, 0.55f, 0.30f);
    private Color midCol = new Color(0.42f, 0.38f, 0.32f);
    private Color baseCol = new Color(0.30f, 0.27f, 0.22f);
    private float topLine = 0.80f;
    private bool useMat = false;
    private Material mat = null;

    // ── Output ─────────────────────────────────────────────────────────────
    private string meshName = "LowPolyCliff";
    private bool saveAsset = false;
    private string saveFolder = "Assets/Meshes";

    // ── UI ─────────────────────────────────────────────────────────────────
    private Vector2 scroll;
    private bool sShape = true, sTri = true, sNoise = true, sShade = true, sOut = true;
    private GameObject previewObj;
    private GUIStyle hdrStyle;
    private bool stylesOk;

    [MenuItem("Tools/Low Poly Cliff Generator")]
    public static void ShowWindow() =>
        GetWindow<LowPolyCliffGenerator>("🪨  Cliff Gen").minSize = new Vector2(340, 540);

    void InitStyles()
    {
        if (stylesOk) return; stylesOk = true;
        hdrStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
        hdrStyle.normal.textColor = new Color(1f, 0.88f, 0.7f);
    }

    void OnGUI()
    {
        InitStyles();
        var r = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.22f, 0.16f, 0.10f));
        GUI.Label(new Rect(r.x + 10, r.y + 6, r.width, 28), "🪨   Low Poly Cliff Generator", hdrStyle);
        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        sShape = Sec(sShape, "Cliff Shape", ShapeUI);
        sTri = Sec(sTri, "Face Triangulation", TriUI);
        sNoise = Sec(sNoise, "Noise / Roughness", NoiseUI);
        sShade = Sec(sShade, "Shading & Colors", ShadeUI);
        sOut = Sec(sOut, "Output", OutUI);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.7f, 0.45f, 0.25f);
        if (GUILayout.Button("▶  Generate in Scene", GUILayout.Height(34))) Generate(false);
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f);
        if (GUILayout.Button("↺  Randomise Seed", GUILayout.Height(34))) { seed = Random.Range(0, 99999); Generate(false); }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        if (saveAsset)
        {
            GUI.backgroundColor = new Color(0.85f, 0.65f, 0.2f);
            if (GUILayout.Button("💾  Save Mesh Asset", GUILayout.Height(28))) Generate(true);
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.EndScrollView();
    }

    bool Sec(bool open, string label, System.Action body)
    {
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, label);
        if (open) { EditorGUI.indentLevel++; body(); EditorGUI.indentLevel--; EditorGUILayout.Space(2); }
        EditorGUILayout.EndFoldoutHeaderGroup();
        return open;
    }

    void ShapeUI()
    {
        segments = EditorGUILayout.IntSlider("Segments (Width)", segments, 4, 80);
        layers = EditorGUILayout.IntSlider("Layers (Height)", layers, 2, 30);
        cliffWidth = EditorGUILayout.Slider("Cliff Width", cliffWidth, 1f, 50f);
        cliffHeight = EditorGUILayout.Slider("Cliff Height", cliffHeight, 1f, 30f);
        cliffDepth = EditorGUILayout.Slider("Cliff Depth", cliffDepth, 0.5f, 15f);
        xJitter = EditorGUILayout.Slider("Horizontal Jaggedness", xJitter, 0f, 1f);
        yJitter = EditorGUILayout.Slider("Ledge Height Variance", yJitter, 0f, 1f);
        depthJitter = EditorGUILayout.Slider("Surface Protrusion", depthJitter, 0f, 1f);
        overhangAmt = EditorGUILayout.Slider("Overhang", overhangAmt, 0f, 0.8f);
        addTopSurface = EditorGUILayout.Toggle("Add Top Surface", addTopSurface);
    }

    void TriUI()
    {
        faceMode = (FaceMode)EditorGUILayout.EnumPopup("Face Mode", faceMode);
        EditorGUILayout.HelpBox(faceMode == FaceMode.RegularGrid
            ? "Grid quads on the cliff face." : "Jittered Delaunay for irregular face polygons.", MessageType.None);
        if (faceMode == FaceMode.JitteredDelaunay)
            faceJitter = EditorGUILayout.Slider("Face Jitter", faceJitter, 0f, 1f);
    }

    void NoiseUI()
    {
        seed = EditorGUILayout.IntField("Seed", seed);
        nScale = EditorGUILayout.Slider("Scale", nScale, 0.01f, 3f);
        nStrength = EditorGUILayout.Slider("Strength", nStrength, 0f, 2f);
        nOctaves = EditorGUILayout.IntSlider("Octaves", nOctaves, 1, 6);
        nPersist = EditorGUILayout.Slider("Persistence", nPersist, 0.1f, 1f);
        nLacunarity = EditorGUILayout.Slider("Lacunarity", nLacunarity, 1f, 4f);
    }

    void ShadeUI()
    {
        useVC = EditorGUILayout.Toggle("Vertex Colors", useVC);
        if (useVC)
        {
            EditorGUI.indentLevel++;
            topCol = EditorGUILayout.ColorField("Top / Moss", topCol);
            midCol = EditorGUILayout.ColorField("Mid Rock", midCol);
            baseCol = EditorGUILayout.ColorField("Base Rock", baseCol);
            topLine = EditorGUILayout.Slider("Top Line", topLine, 0f, 1f);
            EditorGUI.indentLevel--;
        }
        useMat = EditorGUILayout.Toggle("Assign Material", useMat);
        if (useMat)
        {
            EditorGUI.indentLevel++;
            mat = (Material)EditorGUILayout.ObjectField("Material", mat, typeof(Material), false);
            EditorGUI.indentLevel--;
        }
    }

    void OutUI()
    {
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);
        saveAsset = EditorGUILayout.Toggle("Save Mesh Asset", saveAsset);
        if (saveAsset)
        {
            EditorGUI.indentLevel++;
            saveFolder = EditorGUILayout.TextField("Folder", saveFolder);
            EditorGUI.indentLevel--;
        }
    }

    // ── Generation ────────────────────────────────────────────────────────
    void Generate(bool save)
    {
        Mesh mesh = BuildCliffMesh();
        if (save) MeshGenUtils.SaveMeshAsset(mesh, saveFolder);

        if (previewObj == null) { previewObj = new GameObject(meshName); previewObj.AddComponent<MeshFilter>(); previewObj.AddComponent<MeshRenderer>(); }
        previewObj.name = meshName;
        previewObj.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = previewObj.GetComponent<MeshRenderer>();
        if (useMat && mat != null) mr.sharedMaterial = mat;
        else if (mr.sharedMaterial == null) mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        Selection.activeGameObject = previewObj;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.RegisterCreatedObjectUndo(previewObj, "Generate Cliff");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cliff mesh construction
    //
    // Strategy:
    //   • Build a (segments+1) × (layers+1) grid of 3-D control points.
    //   • Each point gets X and Y jitter (making ledges/overhangs) plus a
    //     Z (depth) displacement driven by noise, so the face undulates.
    //   • Triangulate using Delaunay on the UV-flattened face or a regular grid.
    //   • Add vertical side walls and optionally a flat/noisy top cap.
    // ─────────────────────────────────────────────────────────────────────
    Mesh BuildCliffMesh()
    {
        var offs = MeshGenUtils.MakeOctaveOffsets(nOctaves, seed);

        var savedRng = Random.state;
        Random.InitState(seed);

        int cols = segments + 1, rows = layers + 1;

        // ── Build control-point grid ──────────────────────────────────────
        // pts[col, row] in world space
        var pts = new Vector3[cols, rows];

        // Ledge Y positions: equally spaced + per-row jitter
        float layerH = cliffHeight / layers;
        float[] rowY = new float[rows];
        rowY[0] = 0f; rowY[rows - 1] = cliffHeight;
        for (int row = 1; row < rows - 1; row++)
            rowY[row] = row * layerH + Random.Range(-yJitter, yJitter) * layerH * 0.5f;

        // Per-column X positions: equally spaced + per-column jitter
        float segW = cliffWidth / segments;
        float[] colX = new float[cols];
        colX[0] = -cliffWidth * 0.5f; colX[cols - 1] = cliffWidth * 0.5f;
        for (int col = 1; col < cols - 1; col++)
            colX[col] = col * segW - cliffWidth * 0.5f + Random.Range(-xJitter, xJitter) * segW * 0.5f;

        Random.state = savedRng;

        // Fill grid: Z displacement from noise + overhang per ledge
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                float nx = colX[col] / (cliffWidth * 0.5f);
                float ny = rowY[row] / cliffHeight;

                float noiseVal = MeshGenUtils.FractalNoise(nx, ny, nOctaves, nScale, nPersist, nLacunarity, offs);
                // Z: 0 = front face, positive = inset into cliff
                float zBase = noiseVal * nStrength * depthJitter * cliffDepth;
                float zOver = (row > 0 && row < rows - 1) ? Mathf.Sin(ny * Mathf.PI) * overhangAmt * cliffDepth : 0f;
                pts[col, row] = new Vector3(colX[col], rowY[row], -zBase - zOver);
            }

        // ── Triangulate the front face ────────────────────────────────────
        var allVerts = new List<Vector3>();
        var allTris = new List<int[]>();

        if (faceMode == FaceMode.JitteredDelaunay)
        {
            // Project face points to a 2-D UV space for Delaunay, then lift back
            var pts2d = new List<Vector2>(cols * rows);
            var pts3d = new List<Vector3>(cols * rows);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    Vector3 p = pts[col, row];
                    // UV = (colX normalised, rowY normalised) – undistorted face layout
                    pts2d.Add(new Vector2(colX[col], rowY[row]));
                    pts3d.Add(p);
                }

            // Jitter interior UV points (same as mountain jitter)
            var savedRng2 = Random.state;
            Random.InitState(seed + 99);
            float jW = segW * faceJitter * 0.5f, jH = layerH * faceJitter * 0.5f;
            for (int i = 0; i < pts2d.Count; i++)
            {
                int col = i % cols, row = i / cols;
                bool border = (col == 0 || col == segments || row == 0 || row == layers);
                if (!border) pts2d[i] += new Vector2(Random.Range(-jW, jW), Random.Range(-jH, jH));
            }
            Random.state = savedRng2;

            var triIdx = MeshGenUtils.BowyerWatson(pts2d);
            // Re-orient: Delaunay in XY (X=along cliff, Y=up) needs the normal to point -Z (toward viewer)
            // AssembleFlatMesh uses CW-XZ convention – here our "XZ" is actually XY of the face.
            // We handle this manually below.
            foreach (var tri in triIdx)
            {
                Vector3 a = pts3d[tri[0]], b = pts3d[tri[1]], c = pts3d[tri[2]];
                allVerts.Add(a); allVerts.Add(b); allVerts.Add(c);
                allTris.Add(new[] { allVerts.Count - 3, allVerts.Count - 2, allVerts.Count - 1 });
            }
        }
        else
        {
            // Regular grid quads – CW winding so normals face -Z (front of cliff)
            for (int row = 0; row < layers; row++)
                for (int col = 0; col < segments; col++)
                {
                    Vector3 v00 = pts[col, row], v10 = pts[col + 1, row], v01 = pts[col, row + 1], v11 = pts[col + 1, row + 1];
                    int b = allVerts.Count;
                    allVerts.Add(v00); allVerts.Add(v10); allVerts.Add(v01); allVerts.Add(v11);
                    allTris.Add(new[] { b, b + 1, b + 2 });
                    allTris.Add(new[] { b + 1, b + 3, b + 2 });
                }
        }

        // ── Side walls (left / right) ────────────────────────────────────
        AddQuadStrip(allVerts, allTris, pts, 0, 0, rows, true, cliffDepth); // left
        AddQuadStrip(allVerts, allTris, pts, segments, 0, rows, false, cliffDepth); // right

        // ── Back wall ────────────────────────────────────────────────────
        AddBackWall(allVerts, allTris, pts, cols, rows, cliffDepth);

        // ── Top cap ──────────────────────────────────────────────────────
        if (addTopSurface) AddTopCap(allVerts, allTris, pts, cols, cliffDepth, offs);

        // ── Assemble mesh ─────────────────────────────────────────────────
        Color ColFn(float y) { float t = y / cliffHeight; return t > topLine ? topCol : t > 0.35f ? midCol : baseCol; }

        var verts = new List<Vector3>(allTris.Count * 3);
        var nrms = new List<Vector3>(allTris.Count * 3);
        var uvs = new List<Vector2>(allTris.Count * 3);
        var cols2 = new List<Color>(allTris.Count * 3);
        var tIdxs = new List<int>(allTris.Count * 3);

        foreach (var tri in allTris)
        {
            Vector3 a = allVerts[tri[0]], b = allVerts[tri[1]], c = allVerts[tri[2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            float avgY = (a.y + b.y + c.y) / 3f;
            Color col = ColFn(avgY);
            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            nrms.Add(n); nrms.Add(n); nrms.Add(n);
            uvs.Add(new Vector2(a.x / cliffWidth + 0.5f, a.y / cliffHeight));
            uvs.Add(new Vector2(b.x / cliffWidth + 0.5f, b.y / cliffHeight));
            uvs.Add(new Vector2(c.x / cliffWidth + 0.5f, c.y / cliffHeight));
            cols2.Add(col); cols2.Add(col); cols2.Add(col);
            tIdxs.Add(idx); tIdxs.Add(idx + 1); tIdxs.Add(idx + 2);
        }

        var mesh = new Mesh { name = meshName };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetNormals(nrms); mesh.SetUVs(0, uvs);
        if (useVC) mesh.SetColors(cols2);
        mesh.SetTriangles(tIdxs, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Vertical side wall along a fixed column
    void AddQuadStrip(List<Vector3> verts, List<int[]> tris,
                      Vector3[,] pts, int col, int rowStart, int rowCount,
                      bool leftSide, float depth)
    {
        for (int row = rowStart; row < rowStart + rowCount - 1; row++)
        {
            Vector3 front0 = pts[col, row];
            Vector3 front1 = pts[col, row + 1];
            Vector3 back0 = new Vector3(front0.x, front0.y, depth);
            Vector3 back1 = new Vector3(front1.x, front1.y, depth);

            int b = verts.Count;
            verts.Add(front0); verts.Add(front1); verts.Add(back0); verts.Add(back1);

            if (leftSide) { tris.Add(new[] { b, b + 2, b + 1 }); tris.Add(new[] { b + 1, b + 2, b + 3 }); }
            else { tris.Add(new[] { b, b + 1, b + 2 }); tris.Add(new[] { b + 1, b + 3, b + 2 }); }
        }
    }

    // Back wall (flat, at Z = cliffDepth)
    void AddBackWall(List<Vector3> verts, List<int[]> tris,
                     Vector3[,] pts, int cols, int rows, float depth)
    {
        int topRow = rows - 1;
        for (int col = 0; col < cols - 1; col++)
        {
            Vector3 bl = new Vector3(pts[col, 0].x, 0f, depth);
            Vector3 br = new Vector3(pts[col + 1, 0].x, 0f, depth);
            Vector3 tl = new Vector3(pts[col, topRow].x, cliffHeight, depth);
            Vector3 tr = new Vector3(pts[col + 1, topRow].x, cliffHeight, depth);
            int b = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tl); verts.Add(tr);
            // Back wall normal faces +Z → CW when viewed from +Z side
            tris.Add(new[] { b, b + 1, b + 2 }); tris.Add(new[] { b + 1, b + 3, b + 2 });
        }
    }

    // Top cap with slight noise variation for a rocky ledge top
    void AddTopCap(List<Vector3> verts, List<int[]> tris,
                   Vector3[,] pts, int cols, float depth, Vector2[] offs)
    {
        int topRow = layers; // last row index
        for (int col = 0; col < cols - 1; col++)
        {
            Vector3 fl = pts[col, topRow];
            Vector3 fr = pts[col + 1, topRow];
            Vector3 bl = new Vector3(fl.x, fl.y, depth);
            Vector3 br = new Vector3(fr.x, fr.y, depth);
            int b = verts.Count;
            verts.Add(fl); verts.Add(fr); verts.Add(bl); verts.Add(br);
            // Top cap normal points up → CW in XZ
            tris.Add(new[] { b, b + 2, b + 1 }); tris.Add(new[] { b + 1, b + 2, b + 3 });
        }
    }
}