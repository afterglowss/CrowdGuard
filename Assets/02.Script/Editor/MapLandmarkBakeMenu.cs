using CrowdGuard.Climbing.Tools.Map;
using UnityEditor;
using UnityEngine;

namespace CrowdGuard.Editor.Map
{
    /// <summary>
    /// Unity 상단 메뉴에서 scene의 MapLandmarkBaker bake를 실행합니다.
    /// </summary>
    public static class MapLandmarkBakeMenu
    {
        [MenuItem("Tools/CrowdGuard/Map/Bake Landmarks")]
        private static void BakeLandmarks()
        {
            MapLandmarkBaker baker = FindSelectedBaker();
            if (baker == null)
            {
                baker = MapLandmarkBakeUtility.FindFirstBakerInActiveScene();
            }

            if (baker == null)
            {
                const string message = "Active scene에서 MapLandmarkBaker를 찾을 수 없습니다.";
                Debug.LogWarning($"[MapLandmarkBaker] {message}");
                EditorUtility.DisplayDialog("Bake Landmarks", message, "OK");
                return;
            }

            MapLandmarkBakeUtility.Bake(baker, out _);
        }

        private static MapLandmarkBaker FindSelectedBaker()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return null;
            }

            MapLandmarkBaker baker = selectedObject.GetComponentInParent<MapLandmarkBaker>();
            if (baker != null)
            {
                return baker;
            }

            return selectedObject.GetComponentInChildren<MapLandmarkBaker>(true);
        }
    }
}
