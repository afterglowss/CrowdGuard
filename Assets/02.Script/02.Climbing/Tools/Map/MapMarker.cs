using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 텐트나 랜드마크처럼 월드에 고정된 지도 마커입니다.
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        [SerializeField] private MapMarkerType _markerType = MapMarkerType.Landmark;
        [SerializeField] private string _label;
        [SerializeField] private bool _alwaysVisible = true;

        /// <summary>
        /// 현재 Transform 상태를 지도 렌더링용 데이터로 변환합니다.
        /// </summary>
        public MapMarkerData ToData()
        {
            return new MapMarkerData(
                _markerType,
                transform.position,
                transform.forward,
                _label,
                _alwaysVisible);
        }

        private void Reset()
        {
            if (name.ToLowerInvariant().Contains("tent"))
            {
                _markerType = MapMarkerType.Tent;
            }
        }
    }
}
