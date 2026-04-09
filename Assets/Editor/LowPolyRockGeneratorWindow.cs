using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace LowPolyRocks
{
    public class LowPolyRockGeneratorWindow : EditorWindow
    {
        // ── Shape ──────────────────────────────────────────────────────────────
        private int seed = 42;
        private float baseRadius = 0.5f;
        private int subdivisions = 2;        // icosphere subdivisions (0-4)
        private float noiseStrength = 0.35f;
        private float noiseFrequency = 2.2f;
        private float flattenY = 0.75f;    // squash on Y so rocks sit low
        private float sharpness = 0.6f;     // how "faceted" the displacement is

        // ── Cluster ────────────────────────────────────────────────────────────
        private bool generateCluster = true;
        private int clusterCount = 6;
        private float clusterRadius = 1.2f;
        private float satelliteScale = 0.3f;

        // ── Material ───────────────────────────────────────────────────────────
        private Material rockMaterial;
        private Color baseColor = new Color(0.55f, 0.57f, 0.60f);
        private Color shadowColor = new Color(0.30f, 0.32f, 0.35f);
        private Color highlightColor = new Color(0.80f, 0.82f, 0.85f);

        // ── Output ─────────────────────────────────────────────────────────────
        private string savePath = "Assets/LowPolyRocks/Meshes";
        private bool saveMesh = false;

        // ── UI state ───────────────────────────────────────────────────────────
        private Vector2 scroll;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private bool headersBuilt;

        // ──────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Low-Poly Rock Generator")]
        public static void ShowWindow()
        {
            var w = GetWindow<LowPolyRockGeneratorWindow>("🪨 Rock Generator");
            w.minSize = new Vector2(340, 560);
        }

        // ──────────────────────────────────────────────────────────────────────
        private void BuildStyles()
        {
            if (headersBuilt) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
            };
            headerStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

            sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4),
            };

            headersBuilt = true;
        }

        // ──────────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            BuildStyles();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // ── Title ──
            EditorGUILayout.Space(6);
            GUILayout.Label("LOW-POLY ROCK GENERATOR", headerStyle);
            DrawSeparator();

            // ── Shape ──
            SectionHeader("SHAPE");
            EditorGUILayout.BeginVertical(sectionStyle);
            seed = EditorGUILayout.IntField("Seed", seed);
            baseRadius = EditorGUILayout.Slider("Base Radius", baseRadius, 0.1f, 3f);
            subdivisions = EditorGUILayout.IntSlider("Subdivisions", subdivisions, 0, 4);
            noiseStrength = EditorGUILayout.Slider("Noise Strength", noiseStrength, 0f, 1f);
            noiseFrequency = EditorGUILayout.Slider("Noise Frequency", noiseFrequency, 0.5f, 6f);
            flattenY = EditorGUILayout.Slider("Y Flatten", flattenY, 0.2f, 1f);
            sharpness = EditorGUILayout.Slider("Sharpness", sharpness, 0f, 1f);
            EditorGUILayout.EndVertical();

            // ── Cluster ──
            SectionHeader("CLUSTER");
            EditorGUILayout.BeginVertical(sectionStyle);
            generateCluster = EditorGUILayout.Toggle("Generate Cluster", generateCluster);
            if (generateCluster)
            {
                clusterCount = EditorGUILayout.IntSlider("Satellite Count", clusterCount, 1, 12);
                clusterRadius = EditorGUILayout.Slider("Cluster Spread", clusterRadius, 0.3f, 5f);
                satelliteScale = EditorGUILayout.Slider("Satellite Scale", satelliteScale, 0.1f, 0.9f);
            }
            EditorGUILayout.EndVertical();

            // ── Material ──
            SectionHeader("MATERIAL");
            EditorGUILayout.BeginVertical(sectionStyle);
            rockMaterial = (Material)EditorGUILayout.ObjectField("Material Override", rockMaterial, typeof(Material), false);
            EditorGUILayout.LabelField("(leave blank to auto-create a flat-shaded material)", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            baseColor = EditorGUILayout.ColorField("Base Color", baseColor);
            shadowColor = EditorGUILayout.ColorField("Shadow Color", shadowColor);
            highlightColor = EditorGUILayout.ColorField("Highlight Color", highlightColor);
            EditorGUILayout.EndVertical();

            // ── Save ──
            SectionHeader("SAVE MESH");
            EditorGUILayout.BeginVertical(sectionStyle);
            saveMesh = EditorGUILayout.Toggle("Save .asset to disk", saveMesh);
            if (saveMesh)
                savePath = EditorGUILayout.TextField("Save Path", savePath);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ── Buttons ──
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⟳  RANDOMIZE SEED", GUILayout.Height(28)))
                seed = Random.Range(0, 99999);

            if (GUILayout.Button("🪨  GENERATE", GUILayout.Height(28)))
                Generate();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────────────────────────────
        private void Generate()
        {
            Random.InitState(seed);

            // Parent object
            var parent = new GameObject("RockCluster_" + seed);
            Undo.RegisterCreatedObjectUndo(parent, "Create Rock Cluster");

            // Main (large) rock
            var mainGO = CreateRock("Rock_Main", baseRadius, parent.transform, Vector3.zero);

            if (generateCluster)
            {
                for (int i = 0; i < clusterCount; i++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float dist = Random.Range(clusterRadius * 0.3f, clusterRadius);
                    var pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    float scale = baseRadius * satelliteScale * Random.Range(0.5f, 1.4f);
                    CreateRock("Rock_Sat_" + i, scale, parent.transform, pos);
                }
            }

            Selection.activeGameObject = parent;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // ──────────────────────────────────────────────────────────────────────
        private GameObject CreateRock(string name, float radius, Transform parent, Vector3 localPos)
        {
            int rockSeed = seed + name.GetHashCode();
            Mesh mesh = LowPolyRockMesh.Build(rockSeed, radius, subdivisions,
                                                    noiseStrength, noiseFrequency,
                                                    flattenY, sharpness);

            if (saveMesh)
                SaveMeshAsset(mesh, name);

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            // Slight random rotation so satellites look natural
            go.transform.localRotation = Quaternion.Euler(
                Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-10f, 10f));

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;

            mr.sharedMaterial = rockMaterial != null ? rockMaterial
                                                      : BuildMaterial(name, radius);
            return go;
        }

        // ──────────────────────────────────────────────────────────────────────
        private Material BuildMaterial(string suffix, float radius)
        {
            // Use URP/Lit if available, fallback to Standard
            string shaderName = Shader.Find("Universal Render Pipeline/Lit") != null
                ? "Universal Render Pipeline/Lit"
                : "Standard";

            var mat = new Material(Shader.Find(shaderName))
            {
                name = "M_Rock_" + suffix
            };

            // Lerp the base color slightly per rock for variety
            float t = Mathf.Clamp01(radius / (baseRadius + 0.01f));
            var col = Color.Lerp(shadowColor, baseColor, t * 0.6f + 0.2f);
            col *= Random.Range(0.88f, 1.08f);
            mat.color = col;
            mat.SetFloat("_Smoothness", 0.0f);
            mat.SetFloat("_Glossiness", 0.0f);

            return mat;
        }

        // ──────────────────────────────────────────────────────────────────────
        private void SaveMeshAsset(Mesh mesh, string name)
        {
            if (!System.IO.Directory.Exists(savePath))
                System.IO.Directory.CreateDirectory(savePath);

            string path = $"{savePath}/{name}_{seed}.asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void SectionHeader(string text)
        {
            EditorGUILayout.Space(4);
            GUILayout.Label(text, EditorStyles.boldLabel);
        }

        private void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(4);
        }
    }
}