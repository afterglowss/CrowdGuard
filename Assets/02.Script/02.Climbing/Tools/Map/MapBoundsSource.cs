using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 런타임에 생성되는 지도 프리팹이 씬의 MapBounds를 찾기 위한 단일 진입점입니다.
    /// 개발 단계에서 맵이 하나일 때 사용하는 간단한 연결 방식입니다.
    /// </summary>
    public class MapBoundsSource : MonoBehaviour
    {
        public static MapBoundsSource Instance { get; private set; }

        [SerializeField] private MapBounds _mapBounds;

        /// <summary>
        /// 씬에서 공유할 지도 bounds입니다.
        /// </summary>
        public MapBounds MapBounds => _mapBounds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MapBoundsSource] 씬에 MapBoundsSource가 둘 이상 있습니다. 마지막 활성 인스턴스를 사용합니다.");
            }

            Instance = this;

            if (_mapBounds == null)
            {
                _mapBounds = GetComponent<MapBounds>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
