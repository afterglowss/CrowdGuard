using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도 landmark bake 범위와 대상 database asset을 지정하는 scene component입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapLandmarkBaker : MonoBehaviour
    {
        [SerializeField] private Transform _searchRoot;
        [SerializeField] private MapLandmarkDatabase _targetDatabase;
        [SerializeField] private bool _includeInactive;
        [SerializeField] private bool _includeMapMarkers = true;
        [SerializeField] private bool _includeTentSavePoints = true;
        [SerializeField] private bool _includeHazardTriggerZones = true;

        public Transform SearchRoot => _searchRoot;
        public MapLandmarkDatabase TargetDatabase => _targetDatabase;
        public bool IncludeInactive => _includeInactive;
        public bool IncludeMapMarkers => _includeMapMarkers;
        public bool IncludeTentSavePoints => _includeTentSavePoints;
        public bool IncludeHazardTriggerZones => _includeHazardTriggerZones;
    }
}
