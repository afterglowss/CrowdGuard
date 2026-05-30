using CrowdGuard.Climbing.Tools.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdGuard.Editor.Map
{
    /// <summary>
    /// Scene View에서 지도 블록 bounds와 비율 라벨을 항상 표시합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class MapBoundsSceneViewDrawer
    {
        private static readonly Color BlockFillColor = new Color(1f, 0.82f, 0.05f, 0.08f);
        private static readonly Color BlockWireColor = new Color(1f, 0.82f, 0.05f, 0.9f);
        private static readonly Color FallbackFillColor = new Color(0.1f, 0.85f, 1f, 0.06f);
        private static readonly Color FallbackWireColor = new Color(0.1f, 0.85f, 1f, 0.9f);
        private const float InactiveAlphaMultiplier = 0.55f;
        private const float TargetAspectWidth = 2f;
        private const float TargetAspectHeight = 3f;
        private const float TargetAspectTolerance = 0.02f;

        private static GUIStyle _labelStyle;

        static MapBoundsSceneViewDrawer()
        {
            SceneView.duringSceneGui += DrawSceneView;
        }

        private static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        padding = new RectOffset(5, 5, 3, 3)
                    };
                    _labelStyle.normal.textColor = Color.white;
                }

                return _labelStyle;
            }
        }

        private static void DrawSceneView(SceneView sceneView)
        {
            MapBounds[] mapBoundsList = Object.FindObjectsByType<MapBounds>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MapBounds mapBounds in mapBoundsList)
            {
                if (mapBounds == null ||
                    !mapBounds.gameObject.scene.IsValid() ||
                    !mapBounds.gameObject.scene.isLoaded)
                {
                    continue;
                }

                DrawMapBounds(mapBounds);
            }
        }

        private static void DrawMapBounds(MapBounds mapBounds)
        {
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;

            try
            {
                Handles.zTest = CompareFunction.Always;

                if (mapBounds.HasUsableBlockBounds)
                {
                    for (int i = 0; i < mapBounds.BlockCount; i++)
                    {
                        if (!mapBounds.TryGetBlockWorldBounds(i, out Vector2 worldMin, out Vector2 worldMax))
                        {
                            continue;
                        }

                        DrawBounds(
                            mapBounds,
                            worldMin,
                            worldMax,
                            BuildBlockLabel(mapBounds, i, worldMin, worldMax),
                            BlockFillColor,
                            BlockWireColor);
                    }
                }
                else if (mapBounds.TryGetFallbackWorldBounds(out Vector2 worldMin, out Vector2 worldMax))
                {
                    DrawBounds(
                        mapBounds,
                        worldMin,
                        worldMax,
                        "Map Bounds\n" + BuildSizeLine(worldMin, worldMax),
                        FallbackFillColor,
                        FallbackWireColor);
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
                Handles.color = previousColor;
            }
        }

        private static void DrawBounds(
            MapBounds mapBounds,
            Vector2 worldMin,
            Vector2 worldMax,
            string label,
            Color fillColor,
            Color wireColor)
        {
            bool inactive = !mapBounds.gameObject.activeInHierarchy || !mapBounds.enabled;
            if (inactive)
            {
                fillColor = WithAlphaMultiplier(fillColor, InactiveAlphaMultiplier);
                wireColor = WithAlphaMultiplier(wireColor, InactiveAlphaMultiplier);
            }

            float z = mapBounds.transform.position.z;
            Vector3[] corners =
            {
                new Vector3(worldMin.x, worldMin.y, z),
                new Vector3(worldMin.x, worldMax.y, z),
                new Vector3(worldMax.x, worldMax.y, z),
                new Vector3(worldMax.x, worldMin.y, z)
            };

            Handles.DrawSolidRectangleWithOutline(corners, fillColor, wireColor);

            Vector3 labelPosition = new Vector3(
                (worldMin.x + worldMax.x) * 0.5f,
                worldMax.y,
                z);
            Handles.Label(labelPosition, label, LabelStyle);
        }

        private static string BuildBlockLabel(
            MapBounds mapBounds,
            int blockIndex,
            Vector2 worldMin,
            Vector2 worldMax)
        {
            string blockLabel = mapBounds.GetBlockLabel(blockIndex);
            string title = string.IsNullOrWhiteSpace(blockLabel)
                ? $"Block {blockIndex}"
                : $"Block {blockIndex}: {blockLabel}";

            return title + "\n" + BuildSizeLine(worldMin, worldMax);
        }

        private static string BuildSizeLine(Vector2 worldMin, Vector2 worldMax)
        {
            float width = Mathf.Abs(worldMax.x - worldMin.x);
            float height = Mathf.Abs(worldMax.y - worldMin.y);
            float aspect = Mathf.Approximately(height, 0f) ? 0f : width / height;

            return $"{width:0.##} x {height:0.##} | aspect {aspect:0.###}\n" +
                   BuildTargetAspectLine(aspect);
        }

        private static string BuildTargetAspectLine(float aspect)
        {
            float targetAspect = TargetAspectWidth / TargetAspectHeight;
            if (Mathf.Approximately(aspect, 0f))
            {
                return $"target {TargetAspectWidth:0}:{TargetAspectHeight:0} invalid";
            }

            float difference = Mathf.Abs(aspect - targetAspect) / targetAspect;
            if (difference <= TargetAspectTolerance)
            {
                return $"target {TargetAspectWidth:0}:{TargetAspectHeight:0} OK";
            }

            return $"target {TargetAspectWidth:0}:{TargetAspectHeight:0} mismatch {difference:P1}";
        }

        private static Color WithAlphaMultiplier(Color color, float multiplier)
        {
            color.a *= multiplier;
            return color;
        }
    }
}
