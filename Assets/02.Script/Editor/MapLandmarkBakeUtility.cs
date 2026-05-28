using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdGuard.Editor.Map
{
    /// <summary>
    /// Scene에 배치된 지도 landmark 후보를 수집해 MapLandmarkDatabase asset으로 bake합니다.
    /// </summary>
    public static class MapLandmarkBakeUtility
    {
        public static bool Bake(MapLandmarkBaker baker, out int bakedCount)
        {
            bakedCount = 0;

            if (baker == null)
            {
                Debug.LogWarning("[MapLandmarkBaker] Baker component is missing.");
                return false;
            }

            MapLandmarkDatabase targetDatabase = baker.TargetDatabase;
            if (targetDatabase == null)
            {
                Debug.LogWarning("[MapLandmarkBaker] Target database is not assigned.", baker);
                return false;
            }

            List<MapLandmarkRecord> records = new List<MapLandmarkRecord>();
            HashSet<int> visitedGameObjects = new HashSet<int>();

            if (baker.IncludeMapMarkers)
            {
                AddMapMarkers(baker, records, visitedGameObjects);
            }

            if (baker.IncludeTentSavePoints)
            {
                AddTentSavePoints(baker, records, visitedGameObjects);
            }

            if (baker.IncludeHazardTriggerZones)
            {
                AddHazardTriggerZones(baker, records, visitedGameObjects);
            }

            Undo.RecordObject(targetDatabase, "Bake Map Landmarks");
            targetDatabase.SetEntries(records);
            EditorUtility.SetDirty(targetDatabase);
            AssetDatabase.SaveAssets();

            bakedCount = records.Count;
            string assetPath = AssetDatabase.GetAssetPath(targetDatabase);
            Debug.Log($"[MapLandmarkBaker] Baked {bakedCount} landmark marker(s) to '{assetPath}'.", baker);
            return true;
        }

        public static MapLandmarkBaker FindFirstBakerInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                MapLandmarkBaker baker = roots[i].GetComponentInChildren<MapLandmarkBaker>(true);
                if (baker != null)
                {
                    return baker;
                }
            }

            return null;
        }

        private static void AddMapMarkers(
            MapLandmarkBaker baker,
            List<MapLandmarkRecord> records,
            HashSet<int> visitedGameObjects)
        {
            foreach (MapMarker marker in FindComponents<MapMarker>(baker))
            {
                if (!CanBake(marker, baker.IncludeInactive))
                {
                    continue;
                }

                int instanceId = marker.gameObject.GetInstanceID();
                visitedGameObjects.Add(instanceId);
                records.Add(MapLandmarkRecord.FromMarkerData(marker.ToData(), "Explicit"));
            }
        }

        private static void AddTentSavePoints(
            MapLandmarkBaker baker,
            List<MapLandmarkRecord> records,
            HashSet<int> visitedGameObjects)
        {
            foreach (TentSavePoint savePoint in FindComponents<TentSavePoint>(baker))
            {
                if (!CanBake(savePoint, baker.IncludeInactive))
                {
                    continue;
                }

                int instanceId = savePoint.gameObject.GetInstanceID();
                if (visitedGameObjects.Contains(instanceId))
                {
                    continue;
                }

                Transform source = savePoint.exteriorPos != null
                    ? savePoint.exteriorPos
                    : savePoint.transform;
                string label = string.IsNullOrWhiteSpace(savePoint.gameObject.name)
                    ? "Tent"
                    : savePoint.gameObject.name;

                records.Add(new MapLandmarkRecord(
                    MapMarkerType.Tent,
                    source.position,
                    source.forward,
                    label,
                    "Tent"));
                visitedGameObjects.Add(instanceId);
            }
        }

        private static void AddHazardTriggerZones(
            MapLandmarkBaker baker,
            List<MapLandmarkRecord> records,
            HashSet<int> visitedGameObjects)
        {
            foreach (HazardTriggerZone hazard in FindComponents<HazardTriggerZone>(baker))
            {
                if (!CanBake(hazard, baker.IncludeInactive))
                {
                    continue;
                }

                int instanceId = hazard.gameObject.GetInstanceID();
                if (visitedGameObjects.Contains(instanceId))
                {
                    continue;
                }

                Collider hazardCollider = hazard.GetComponent<Collider>();
                Vector3 position = hazardCollider != null
                    ? hazardCollider.bounds.center
                    : hazard.transform.position;
                string category = $"Hazard/{hazard.hazardType}";
                string label = string.IsNullOrWhiteSpace(hazard.gameObject.name)
                    ? hazard.hazardType.ToString()
                    : $"{hazard.hazardType}: {hazard.gameObject.name}";

                records.Add(new MapLandmarkRecord(
                    MapMarkerType.Landmark,
                    position,
                    hazard.transform.forward,
                    label,
                    category));
                visitedGameObjects.Add(instanceId);
            }
        }

        private static List<T> FindComponents<T>(MapLandmarkBaker baker) where T : Component
        {
            List<T> components = new List<T>();

            if (baker.SearchRoot != null)
            {
                components.AddRange(baker.SearchRoot.GetComponentsInChildren<T>(baker.IncludeInactive));
                return components;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return components;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (!baker.IncludeInactive && !roots[i].activeInHierarchy)
                {
                    continue;
                }

                components.AddRange(roots[i].GetComponentsInChildren<T>(baker.IncludeInactive));
            }

            return components;
        }

        private static bool CanBake(Component component, bool includeInactive)
        {
            return component != null &&
                   (includeInactive || component.gameObject.activeInHierarchy);
        }
    }
}
