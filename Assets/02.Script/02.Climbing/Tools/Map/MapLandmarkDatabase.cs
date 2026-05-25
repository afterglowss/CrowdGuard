using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 에디터에서 bake한 고정 지도 landmark 목록을 런타임에 제공하는 asset입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "MapLandmarkDatabase", menuName = "CrowdGuard/Map/Landmark Database")]
    public class MapLandmarkDatabase : ScriptableObject
    {
        [SerializeField] private List<MapLandmarkRecord> _entries = new List<MapLandmarkRecord>();

        /// <summary>
        /// Bake된 landmark record 목록입니다.
        /// </summary>
        public IReadOnlyList<MapLandmarkRecord> Entries => _entries;

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 bake 결과로 landmark 목록을 교체합니다.
        /// </summary>
        public void SetEntries(IEnumerable<MapLandmarkRecord> entries)
        {
            _entries.Clear();

            if (entries == null)
            {
                return;
            }

            _entries.AddRange(entries);
        }
#endif
    }

    /// <summary>
    /// Bake된 지도 landmark 한 개의 위치, 표시 타입, 분류 정보를 보관합니다.
    /// </summary>
    [Serializable]
    public struct MapLandmarkRecord
    {
        [SerializeField] private MapMarkerType _type;
        [SerializeField] private Vector3 _worldPosition;
        [SerializeField] private Vector3 _forward;
        [SerializeField] private string _label;
        [SerializeField] private string _category;
        [SerializeField] private bool _isVisible;

        public MapMarkerType Type => _type;
        public Vector3 WorldPosition => _worldPosition;
        public Vector3 Forward => _forward;
        public string Label => _label;
        public string Category => _category;
        public bool IsVisible => _isVisible;

        public MapLandmarkRecord(
            MapMarkerType type,
            Vector3 worldPosition,
            Vector3 forward,
            string label,
            string category,
            bool isVisible = true)
        {
            _type = type;
            _worldPosition = worldPosition;
            _forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            _label = label ?? string.Empty;
            _category = category ?? string.Empty;
            _isVisible = isVisible;
        }

        /// <summary>
        /// 런타임 지도 렌더링에 사용할 marker data로 변환합니다.
        /// </summary>
        public MapMarkerData ToMarkerData()
        {
            return new MapMarkerData(
                _type,
                _worldPosition,
                _forward,
                _label,
                _isVisible);
        }

        /// <summary>
        /// 명시적으로 배치된 MapMarker 데이터를 bake record로 변환합니다.
        /// </summary>
        public static MapLandmarkRecord FromMarkerData(MapMarkerData markerData, string category)
        {
            return new MapLandmarkRecord(
                markerData.Type,
                markerData.WorldPosition,
                markerData.Forward,
                markerData.Label,
                category,
                markerData.IsVisible);
        }
    }
}
