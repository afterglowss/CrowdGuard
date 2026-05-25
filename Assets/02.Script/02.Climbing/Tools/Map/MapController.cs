using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도 데이터를 주기적으로 수집해 MapView에 전달합니다.
    /// </summary>
    [RequireComponent(typeof(MapDataProvider))]
    public class MapController : MonoBehaviour
    {
        [SerializeField] private MapDataProvider _dataProvider;
        [SerializeField] private MapView _view;
        [SerializeField] private float _refreshInterval = 0.1f;
        [SerializeField] private bool _refreshWhenDisabled = false;

        private float _nextRefreshTime;

        private void Awake()
        {
            if (_dataProvider == null)
            {
                _dataProvider = GetComponent<MapDataProvider>();
            }
        }

        private void OnEnable()
        {
            RefreshMap();
        }

        private void Update()
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            RefreshMap();
            _nextRefreshTime = Time.time + Mathf.Max(0.01f, _refreshInterval);
        }

        /// <summary>
        /// 현재 마커 데이터를 즉시 다시 렌더링합니다.
        /// </summary>
        public void RefreshMap()
        {
            if (!isActiveAndEnabled && !_refreshWhenDisabled)
            {
                return;
            }

            if (_view == null || _dataProvider == null)
            {
                return;
            }

            var markers = _dataProvider.GetMarkers();
            bool hasLocalNavigatorPose = _dataProvider.TryGetLocalNavigatorPose(
                out Vector3 localNavigatorPosition,
                out _);

            _view.Render(markers, localNavigatorPosition, hasLocalNavigatorPose);
        }
    }
}
