using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// MapDataProvider의 마커 데이터를 World Space Canvas 지도 위에 렌더링합니다.
    /// _mapBounds가 비어 있으면 씬의 MapBoundsSource에서 자동으로 가져옵니다.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [SerializeField] private RectTransform _mapRect;
        [SerializeField] private RectTransform[] _blockRects;
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

        private void Awake()
        {
            ResolveMapBounds();
        }

        private void OnEnable()
        {
            ResolveMapBounds();
        }

        /// <summary>
        /// 전달된 마커 목록을 현재 지도 블록 좌표계에 맞춰 표시합니다.
        /// </summary>
        public void Render(IReadOnlyList<MapMarkerData> markers)
        {
            ResolveMapBounds();

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

                if (!_mapBounds.TryWorldToMapPosition(
                        marker.WorldPosition,
                        out int blockIndex,
                        out Vector2 normalized))
                {
                    continue;
                }

                RectTransform targetRect = GetTargetRect(blockIndex);
                if (targetRect == null)
                {
                    continue;
                }

                RectTransform markerTransform = _markerPool[visibleIndex];
                if (markerTransform.parent != targetRect)
                {
                    markerTransform.SetParent(targetRect, false);
                }

                markerTransform.gameObject.SetActive(true);
                markerTransform.anchoredPosition = _mapBounds.NormalizedToRectPosition(normalized, targetRect);
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

        private RectTransform GetTargetRect(int blockIndex)
        {
            if (blockIndex < 0)
            {
                return _mapRect;
            }

            if (_blockRects == null || blockIndex >= _blockRects.Length)
            {
                return null;
            }

            return _blockRects[blockIndex];
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

        private void ResolveMapBounds()
        {
            if (_mapBounds != null)
            {
                return;
            }

            MapBoundsSource source = MapBoundsSource.Instance;
            if (source != null)
            {
                _mapBounds = source.MapBounds;
            }
        }
    }
}
