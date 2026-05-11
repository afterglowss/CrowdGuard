using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// MapDataProvider의 마커 데이터를 World Space Canvas 지도 위에 렌더링합니다.
    /// 정적 지형 구조는 인스펙터에서 _terrainLayer에 연결된 배경/라인 오브젝트로 표시하며,
    /// 이 View는 런타임 지형 생성 없이 마커 렌더링만 담당합니다.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [SerializeField] private RectTransform _mapRect;
        [SerializeField] private MapBounds _mapBounds;
        [SerializeField] private RectTransform _markerPrefab;
        [SerializeField] private GameObject _terrainLayer;
        [SerializeField] private Sprite _playerSprite;
        [SerializeField] private Sprite _directionSprite;
        [SerializeField] private Sprite _savePointSprite;
        [SerializeField] private Sprite _anchorSprite;
        [SerializeField] private Sprite _tentSprite;
        [SerializeField] private Sprite _landmarkSprite;

        private readonly List<RectTransform> _markerPool = new List<RectTransform>();

        /// <summary>
        /// 전달된 마커 목록을 현재 지도 좌표계에 맞춰 표시합니다.
        /// </summary>
        public void Render(IReadOnlyList<MapMarkerData> markers)
        {
            if (_mapRect == null || _mapBounds == null || _markerPrefab == null || markers == null)
            {
                return;
            }

            EnsurePoolSize(markers.Count);

            int visibleIndex = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarkerData marker = markers[i];
                if (!marker.IsVisible)
                {
                    continue;
                }

                RectTransform markerTransform = _markerPool[visibleIndex];
                markerTransform.gameObject.SetActive(true);
                markerTransform.anchoredPosition = _mapBounds.NormalizedToRectPosition(
                    _mapBounds.WorldToNormalized(marker.WorldPosition),
                    _mapRect);
                markerTransform.localRotation = marker.Type == MapMarkerType.Direction
                    ? Quaternion.Euler(0f, 0f, GetDirectionAngle(marker.Forward))
                    : Quaternion.identity;

                Image image = markerTransform.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = GetSprite(marker.Type);
                }

                visibleIndex++;
            }

            for (int i = visibleIndex; i < _markerPool.Count; i++)
            {
                _markerPool[i].gameObject.SetActive(false);
            }
        }

        internal Sprite GetSprite(MapMarkerType type)
        {
            switch (type)
            {
                case MapMarkerType.Player:
                    return _playerSprite;
                case MapMarkerType.Direction:
                    return _directionSprite;
                case MapMarkerType.SavePoint:
                    return _savePointSprite;
                case MapMarkerType.Anchor:
                    return _anchorSprite;
                case MapMarkerType.Tent:
                    return _tentSprite;
                case MapMarkerType.Landmark:
                default:
                    return _landmarkSprite;
            }
        }

        private void EnsurePoolSize(int markerCount)
        {
            while (_markerPool.Count < markerCount)
            {
                RectTransform marker = Instantiate(_markerPrefab, _mapRect);
                marker.gameObject.SetActive(false);
                _markerPool.Add(marker);
            }
        }

        private float GetDirectionAngle(Vector3 forward)
        {
            Vector2 direction = new Vector2(forward.x, forward.y);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        }
    }
}
