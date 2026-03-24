using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Low Poly Mountain Generator – Unity 2022.3 LTS
/// Triangulation modes:
///   • Regular Grid        – classic diagonal-split quads
///   • Jittered Delaunay   – grid points randomly nudged, then Delaunay-triangulated
///   • Fully Random Delaunay – scattered random points + Delaunay
/// Place this file inside any Editor/ folder.  Open via: Tools ▶ Low Poly Mountain Generator
/// </summary>
public class LowPolyMountainGenerator : EditorWindow
{
    // ── Terrain Shape ──────────────────────────────────────────────
    private int resolution = 20;
    private float width = 10f;
    private float depth = 10f;
    private float peakHeight = 6f;
    private float baseHeight = 0f;

    // ── Triangulation ──────────────────────────────────────────────
    private enum TriMode { RegularGrid, JitteredDelaunay, FullyRandomDelaunay }
    private TriMode triMode = TriMode.JitteredDelaunay;
    private float jitterAmount = 0.65f;   // 0 = grid, 1 = full cell width
    private int randomPoints = 300;     // used by FullyRandomDelaunay

    // ── Noise ──────────────────────────────────────────────────────
    private float noiseScale = 0.35f;
    private float noiseStrength = 1.8f;
    private int noiseSeed = 42;
    private int noiseOctaves = 3;
    private float noisePersist = 0.5f;
    private float noiseLacunarity = 2.0f;

    // ── Peak ───────────────────────────────────────────────────────
    private float peakSharpness = 2.5f;
    private float peakOffsetX = 0f;
    private float peakOffsetZ = 0f;
    private bool multiPeak = false;
    private int peakCount = 2;
    private float peakSpread = 3f;

    // ── Shading ────────────────────────────────────────────────────
    private bool flatShading = true;
    private bool assignMaterial = false;
    private Material mountainMat = null;
    private bool useVertexColor = false;
    private Color snowColor = new Color(0.95f, 0.97f, 1.0f);
    private Color rockColor = new Color(0.45f, 0.40f, 0.35f);
    private Color grassColor = new Color(0.30f, 0.55f, 0.25f);
    private float snowLine = 0.75f;
    private float grassLine = 0.30f;

    // ── Output ─────────────────────────────────────────────────────
    private string meshName = "LowPolyMountain";
    private bool saveMeshAsset = false;
    private string saveFolder = "Assets/Meshes";

    // ── UI ─────────────────────────────────────────────────────────
    private Vector2 scroll;
    private bool secShape = true, secTri = true, secNoise = true,
                       secPeak = true, secShading = true, secOutput = true;
    private GameObject previewObj;
    private GUIStyle headerStyle;
    private bool stylesInit = false;

    // ──────────────────────────────────────────────────────────────
    [MenuItem("Tools/Low Poly Mountain Generator")]
    public static void ShowWindow()
    {
        var w = GetWindow<LowPolyMountainGenerator>("⛰  Mountain Gen");
        w.minSize = new Vector2(340, 560);
    }

    private void InitStyles()
    {
        if (stylesInit) return;
        stylesInit = true;
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(0.85f, 0.92f, 1.0f);
    }

    // ──────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        InitStyles();

        var bannerRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.18f, 0.28f));
        GUI.Label(new Rect(bannerRect.x + 10, bannerRect.y + 6, bannerRect.width, 28),
                  "⛰   Low Poly Mountain Generator", headerStyle);

        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        secShape = DrawSection(secShape, "Terrain Shape", DrawShapeSection);
        secTri = DrawSection(secTri, "Triangulation", DrawTriSection);
        secNoise = DrawSection(secNoise, "Noise / Detail", DrawNoiseSection);
        secPeak = DrawSection(secPeak, "Peak Settings", DrawPeakSection);
        secShading = DrawSection(secShading, "Shading & Colors", DrawShadingSection);
        secOutput = DrawSection(secOutput, "Output", DrawOutputSection);

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
        if (GUILayout.Button("▶  Generate in Scene", GUILayout.Height(34)))
            GenerateMountain(false);

        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f);
        if (GUILayout.Button("↺  Randomise Seed", GUILayout.Height(34)))
        {
            noiseSeed = Random.Range(0, 99999);
            GenerateMountain(false);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (saveMeshAsset)
        {
            GUI.backgroundColor = new Color(0.85f, 0.65f, 0.2f);
            if (GUILayout.Button("💾  Save Mesh Asset", GUILayout.Height(28)))
                GenerateMountain(true);
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────────────────────
    private bool DrawSection(bool open, string label, System.Action body)
    {
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, label);
        if (open)
        {
            EditorGUI.indentLevel++;
            body();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        return open;
    }

    private void DrawShapeSection()
    {
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 4, 80);
        width = EditorGUILayout.Slider("Width", width, 1f, 50f);
        depth = EditorGUILayout.Slider("Depth", depth, 1f, 50f);
        peakHeight = EditorGUILayout.Slider("Peak Height", peakHeight, 0.5f, 30f);
        baseHeight = EditorGUILayout.Slider("Base Height", baseHeight, -5f, 5f);
    }

    private void DrawTriSection()
    {
        triMode = (TriMode)EditorGUILayout.EnumPopup("Mode", triMode);

        string hint =
            triMode == TriMode.RegularGrid
                ? "Classic diagonal-split quads. Clean but uniform."
            : triMode == TriMode.JitteredDelaunay
                ? "Grid points randomly nudged before Delaunay. Best balance of organic look & solid coverage."
                : "Fully random point scatter + Delaunay. Most organic; raise Point Count for detail.";
        EditorGUILayout.HelpBox(hint, MessageType.None);

        if (triMode == TriMode.JitteredDelaunay)
            jitterAmount = EditorGUILayout.Slider("Jitter Amount", jitterAmount, 0f, 1f);

        if (triMode == TriMode.FullyRandomDelaunay)
            randomPoints = EditorGUILayout.IntSlider("Point Count", randomPoints, 20, 2000);
    }

    private void DrawNoiseSection()
    {
        noiseSeed = EditorGUILayout.IntField("Seed", noiseSeed);
        noiseScale = EditorGUILayout.Slider("Scale", noiseScale, 0.01f, 2f);
        noiseStrength = EditorGUILayout.Slider("Strength", noiseStrength, 0f, 5f);
        noiseOctaves = EditorGUILayout.IntSlider("Octaves", noiseOctaves, 1, 6);
        noisePersist = EditorGUILayout.Slider("Persistence", noisePersist, 0.1f, 1f);
        noiseLacunarity = EditorGUILayout.Slider("Lacunarity", noiseLacunarity, 1f, 4f);
    }

    private void DrawPeakSection()
    {
        peakSharpness = EditorGUILayout.Slider("Sharpness", peakSharpness, 0.5f, 8f);
        peakOffsetX = EditorGUILayout.Slider("Peak Offset X", peakOffsetX, -1f, 1f);
        peakOffsetZ = EditorGUILayout.Slider("Peak Offset Z", peakOffsetZ, -1f, 1f);
        multiPeak = EditorGUILayout.Toggle("Multi Peak", multiPeak);
        if (multiPeak)
        {
            EditorGUI.indentLevel++;
            peakCount = EditorGUILayout.IntSlider("Peak Count", peakCount, 2, 6);
            peakSpread = EditorGUILayout.Slider("Spread", peakSpread, 0.5f, 8f);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawShadingSection()
    {
        flatShading = EditorGUILayout.Toggle("Flat Shading", flatShading);
        useVertexColor = EditorGUILayout.Toggle("Vertex Colors", useVertexColor);
        if (useVertexColor)
        {
            EditorGUI.indentLevel++;
            snowColor = EditorGUILayout.ColorField("Snow", snowColor);
            rockColor = EditorGUILayout.ColorField("Rock", rockColor);
            grassColor = EditorGUILayout.ColorField("Grass", grassColor);
            snowLine = EditorGUILayout.Slider("Snow Line", snowLine, 0f, 1f);
            grassLine = EditorGUILayout.Slider("Grass Line", grassLine, 0f, 1f);
            EditorGUI.indentLevel--;
        }
        assignMaterial = EditorGUILayout.Toggle("Assign Material", assignMaterial);
        if (assignMaterial)
        {
            EditorGUI.indentLevel++;
            mountainMat = (Material)EditorGUILayout.ObjectField(
                "Material", mountainMat, typeof(Material), false);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawOutputSection()
    {
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);
        saveMeshAsset = EditorGUILayout.Toggle("Save Mesh Asset", saveMeshAsset);
        if (saveMeshAsset)
        {
            EditorGUI.indentLevel++;
            saveFolder = EditorGUILayout.TextField("Save Folder", saveFolder);
            EditorGUI.indentLevel--;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Generation entry point
    // ══════════════════════════════════════════════════════════════
    private void GenerateMountain(bool saveAsset)
    {
        Mesh mesh = BuildMesh();
        if (saveAsset) SaveMesh(mesh);

        if (previewObj == null)
        {
            previewObj = new GameObject(meshName);
            previewObj.AddComponent<MeshFilter>();
            previewObj.AddComponent<MeshRenderer>();
        }
        previewObj.name = meshName;
        previewObj.GetComponent<MeshFilter>().sharedMesh = mesh;

        var mr = previewObj.GetComponent<MeshRenderer>();
        if (assignMaterial && mountainMat != null)
            mr.sharedMaterial = mountainMat;
        else if (mr.sharedMaterial == null)
            mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

        Selection.activeGameObject = previewObj;
        SceneView.lastActiveSceneView?.FrameSelected();
        Undo.RegisterCreatedObjectUndo(previewObj, "Generate Mountain");
    }

    // ══════════════════════════════════════════════════════════════
    // Mesh builder
    // ══════════════════════════════════════════════════════════════
    private Mesh BuildMesh()
    {
        // Save & seed RNG
        Random.State savedRng = Random.state;
        Random.InitState(noiseSeed);

        // Octave offsets for fractal noise
        Vector2[] octaveOffsets = new Vector2[noiseOctaves];
        for (int o = 0; o < noiseOctaves; o++)
            octaveOffsets[o] = new Vector2(Random.Range(-9999f, 9999f),
                                           Random.Range(-9999f, 9999f));

        // Peak positions in normalised −1..1 space
        Vector2[] peaks;
        if (multiPeak)
        {
            peaks = new Vector2[peakCount];
            for (int p = 0; p < peakCount; p++)
            {
                float angle = p * Mathf.PI * 2f / peakCount;
                float r = peakSpread / Mathf.Max(width, depth);
                peaks[p] = new Vector2(peakOffsetX + Mathf.Cos(angle) * r,
                                       peakOffsetZ + Mathf.Sin(angle) * r);
            }
        }
        else peaks = new[] { new Vector2(peakOffsetX, peakOffsetZ) };

        // ── Build 2-D point set ───────────────────────────────────
        List<Vector2> pts2d;
        if (triMode == TriMode.RegularGrid || triMode == TriMode.JitteredDelaunay)
        {
            pts2d = BuildGridPoints(triMode == TriMode.JitteredDelaunay);
        }
        else
        {
            // Fully random + border pins for clean edges
            pts2d = new List<Vector2>(randomPoints + 4)
            {
                new Vector2(-0.5f * width, -0.5f * depth),
                new Vector2( 0.5f * width, -0.5f * depth),
                new Vector2( 0.5f * width,  0.5f * depth),
                new Vector2(-0.5f * width,  0.5f * depth)
            };
            for (int i = 0; i < randomPoints; i++)
                pts2d.Add(new Vector2(Random.Range(-0.5f * width, 0.5f * width),
                                      Random.Range(-0.5f * depth, 0.5f * depth)));
        }

        Random.state = savedRng; // restore Unity's global RNG

        // ── Height function ───────────────────────────────────────
        float GetHeight(Vector2 p)
        {
            float nx = p.x / (0.5f * width);   // −1..1
            float nz = p.y / (0.5f * depth);

            float falloff = 0f;
            foreach (var pk in peaks)
            {
                float dx = nx - pk.x, dz = nz - pk.y;
                float f = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz)), peakSharpness);
                if (f > falloff) falloff = f;
            }

            float amp = 1f, freq = 1f, nv = 0f, maxA = 0f;
            for (int o = 0; o < noiseOctaves; o++)
            {
                float sx = nx * noiseScale * freq + octaveOffsets[o].x;
                float sz = nz * noiseScale * freq + octaveOffsets[o].y;
                nv += Mathf.PerlinNoise(sx, sz) * amp;
                maxA += amp;
                amp *= noisePersist;
                freq *= noiseLacunarity;
            }
            nv /= maxA;
            return falloff * peakHeight + nv * noiseStrength * falloff + baseHeight;
        }

        // Lift to 3D
        var pts3d = new Vector3[pts2d.Count];
        for (int i = 0; i < pts2d.Count; i++)
            pts3d[i] = new Vector3(pts2d[i].x, GetHeight(pts2d[i]), pts2d[i].y);

        // ── Triangulate ───────────────────────────────────────────
        List<int[]> triangles = (triMode == TriMode.RegularGrid)
            ? TriangulateGrid()
            : BowyerWatson(pts2d);

        // ── Assemble mesh ─────────────────────────────────────────
        var verts = new List<Vector3>(triangles.Count * 3);
        var nrms = new List<Vector3>(triangles.Count * 3);
        var uvs = new List<Vector2>(triangles.Count * 3);
        var cols = new List<Color>(triangles.Count * 3);
        var triIdx = new List<int>(triangles.Count * 3);

        foreach (var tri in triangles)
        {
            Vector3 a = pts3d[tri[0]], b = pts3d[tri[1]], c = pts3d[tri[2]];
            Vector3 n = Vector3.Cross(c - a, b - a).normalized;

            float avgY = (a.y + b.y + c.y) / 3f;
            float t = Mathf.InverseLerp(baseHeight, peakHeight, avgY);
            Color col = t > snowLine ? snowColor
                       : t > grassLine ? rockColor
                                       : grassColor;

            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            nrms.Add(n); nrms.Add(n); nrms.Add(n);
            uvs.Add(new Vector2(a.x / width + 0.5f, a.z / depth + 0.5f));
            uvs.Add(new Vector2(b.x / width + 0.5f, b.z / depth + 0.5f));
            uvs.Add(new Vector2(c.x / width + 0.5f, c.z / depth + 0.5f));
            cols.Add(col); cols.Add(col); cols.Add(col);
            triIdx.Add(idx); triIdx.Add(idx + 1); triIdx.Add(idx + 2);
        }

        var mesh = new Mesh { name = meshName };
        if (verts.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetNormals(nrms);
        mesh.SetUVs(0, uvs);
        if (useVertexColor) mesh.SetColors(cols);
        mesh.SetTriangles(triIdx, 0);
        mesh.RecalculateBounds();
        if (!flatShading) mesh.RecalculateNormals();

        return mesh;
    }

    // ══════════════════════════════════════════════════════════════
    // Point generation
    // ══════════════════════════════════════════════════════════════
    private List<Vector2> BuildGridPoints(bool jitter)
    {
        int vx = resolution + 1, vz = resolution + 1;
        float cellW = width / resolution;
        float cellD = depth / resolution;
        float hJW = cellW * jitterAmount * 0.5f;
        float hJD = cellD * jitterAmount * 0.5f;

        var pts = new List<Vector2>(vx * vz);
        for (int z = 0; z < vz; z++)
            for (int x = 0; x < vx; x++)
            {
                float px = (x / (float)resolution - 0.5f) * width;
                float pz = (z / (float)resolution - 0.5f) * depth;
                bool border = (x == 0 || x == resolution || z == 0 || z == resolution);
                if (jitter && !border)
                {
                    px += Random.Range(-hJW, hJW);
                    pz += Random.Range(-hJD, hJD);
                }
                pts.Add(new Vector2(px, pz));
            }
        return pts;
    }

    // ══════════════════════════════════════════════════════════════
    // Regular-grid triangulation (index-based, no Delaunay needed)
    // ══════════════════════════════════════════════════════════════
    private List<int[]> TriangulateGrid()
    {
        int vx = resolution + 1;
        var tris = new List<int[]>(resolution * resolution * 2);
        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int i00 = z * vx + x, i10 = i00 + 1,
                    i01 = i00 + vx, i11 = i01 + 1;
                tris.Add(new[] { i00, i01, i10 });
                tris.Add(new[] { i10, i01, i11 });
            }
        return tris;
    }

    // ══════════════════════════════════════════════════════════════
    // Bowyer-Watson Delaunay triangulation (2-D, O(n²) – fine up to ~2000 pts)
    // ══════════════════════════════════════════════════════════════
    private struct DelTri
    {
        public int a, b, c;
        public Vector2 cc;      // circumcircle centre
        public float rr;      // circumradius²

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
            if (Mathf.Abs(D) < 1e-10f)
            { centre = (A + B + C) / 3f; rSq = float.MaxValue; return; }
            float a2 = ax * ax + ay * ay, b2 = bx * bx + by * by;
            float ux = (by * a2 - ay * b2) / D;
            float uy = (ax * b2 - bx * a2) / D;
            centre = new Vector2(A.x + ux, A.y + uy);
            rSq = ux * ux + uy * uy;
        }
    }

    private List<int[]> BowyerWatson(List<Vector2> pts)
    {
        // Bounding super-triangle
        float minX = float.MaxValue, minY = float.MaxValue,
              maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            if (p.x < minX) minX = p.x; if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x; if (p.y > maxY) maxY = p.y;
        }
        float dx = maxX - minX, dy = maxY - minY;
        float delta = Mathf.Max(dx, dy) * 10f;

        var all = new List<Vector2>(pts) // copy; super-triangle verts appended
        {
            new Vector2(minX - delta,      minY - delta),
            new Vector2(minX + dx * 0.5f,  maxY + delta),
            new Vector2(maxX + delta,      minY - delta)
        };
        int sA = all.Count - 3, sB = sA + 1, sC = sA + 2;

        var tris = new List<DelTri> { new DelTri(sA, sB, sC, all) };

        var bad = new List<DelTri>();
        var boundary = new List<(int, int)>();

        for (int pi = 0; pi < pts.Count; pi++)
        {
            Vector2 point = all[pi];

            // Collect bad triangles
            bad.Clear();
            foreach (var t in tris)
                if (t.InCircumcircle(point)) bad.Add(t);

            // Boundary edges of the polygonal hole
            boundary.Clear();
            foreach (var t in bad)
            {
                TryAddEdge(boundary, bad, t.a, t.b, t);
                TryAddEdge(boundary, bad, t.b, t.c, t);
                TryAddEdge(boundary, bad, t.c, t.a, t);
            }

            foreach (var t in bad) tris.Remove(t);
            foreach (var e in boundary)
                tris.Add(new DelTri(e.Item1, e.Item2, pi, all));
        }

        // Collect result, skipping super-triangle vertices
        var result = new List<int[]>(tris.Count);
        foreach (var t in tris)
        {
            if (t.a >= sA || t.b >= sA || t.c >= sA) continue;

            // Ensure CCW winding in XZ (Unity left-hand Y-up)
            Vector2 pa = all[t.a], pb = all[t.b], pc = all[t.c];
            float cross = (pb.x - pa.x) * (pc.y - pa.y)
                        - (pb.y - pa.y) * (pc.x - pa.x);
            result.Add(cross > 0
                ? new[] { t.a, t.b, t.c }
                : new[] { t.a, t.c, t.b });
        }
        return result;
    }

    /// Adds edge (e0,e1) to boundary only when it is not shared by another bad triangle.
    private static void TryAddEdge(List<(int, int)> boundary, List<DelTri> bad,
                                   int e0, int e1, DelTri owner)
    {
        foreach (var t in bad)
        {
            if (t.Equals(owner)) continue;
            if ((t.a == e0 || t.b == e0 || t.c == e0) &&
                (t.a == e1 || t.b == e1 || t.c == e1)) return; // shared – skip
        }
        boundary.Add((e0, e1));
    }

    // ══════════════════════════════════════════════════════════════
    // Asset saving
    // ══════════════════════════════════════════════════════════════
    private void SaveMesh(Mesh mesh)
    {
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            var parts = saveFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        string path = saveFolder + "/" + meshName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mountain Gen] Updated mesh at {path}");
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mountain Gen] Saved mesh to {path}");
        }
    }
}