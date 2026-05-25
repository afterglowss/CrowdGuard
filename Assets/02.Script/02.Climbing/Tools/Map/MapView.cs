using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.Common;
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
        [SerializeField] private GameObject _fallbackRoot;
        [SerializeField] private Sprite _playerSprite;
        [SerializeField] private Sprite _directionSprite;
        [SerializeField] private Sprite _savePointSprite;
        [SerializeField] private Sprite _anchorSprite;
        [SerializeField] private Sprite _tentSprite;
        [SerializeField] private Sprite _landmarkSprite;
        [SerializeField] private Color _defaultMarkerColor = Color.white;
        [SerializeField] private Color _leaderMarkerColor = new Color(0.2f, 0.75f, 1f, 1f);
        [SerializeField] private Color _navigatorMarkerColor = new Color(1f, 0.78f, 0.2f, 1f);
        [SerializeField] private float _aspectWarningTolerance = 0.15f;

        private readonly List<RectTransform> _markerPool = new List<RectTransform>();
        private readonly HashSet<int> _aspectWarningBlocks = new HashSet<int>();
        private bool _activeBlockWarningLogged;

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
            Render(markers, Vector3.zero, false);
        }

        /// <summary>
        /// 로컬 Navigator 위치가 속한 지도 블록만 활성화하고 해당 블록의 마커만 표시합니다.
        /// </summary>
        public void Render(
            IReadOnlyList<MapMarkerData> markers,
            Vector3 activeBlockWorldPosition,
            bool hasActiveBlockWorldPosition)
        {
            ResolveMapBounds();

            if (_mapRect == null || _mapBounds == null || _markerPrefab == null || markers == null)
            {
                return;
            }

            int activeBlockIndex = ResolveActiveBlockIndex(
                activeBlockWorldPosition,
                hasActiveBlockWorldPosition);
            SetActiveBlock(activeBlockIndex);

            if (activeBlockIndex < 0)
            {
                RenderFallback(activeBlockWorldPosition, hasActiveBlockWorldPosition);
                return;
            }

            _activeBlockWarningLogged = false;
            EnsurePoolSize(markers.Count);

            RectTransform targetRect = GetTargetRect(activeBlockIndex);
            if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
            {
                DeactivateMarkers();
                return;
            }

            WarnIfAspectMismatch(activeBlockIndex, targetRect);

            int visibleIndex = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarkerData marker = markers[i];
                if (!marker.IsVisible)
                {
                    continue;
                }

                if (!_mapBounds.TryWorldToMapPositionInBlock(
                        activeBlockIndex,
                        marker.WorldPosition,
                        out Vector2 normalized))
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
                    image.color = GetMarkerColor(marker);
                }

                visibleIndex++;
            }

            DeactivateMarkers(visibleIndex);
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

        private int ResolveActiveBlockIndex(Vector3 worldPosition, bool hasWorldPosition)
        {
            if (!hasWorldPosition ||
                _mapBounds == null ||
                !_mapBounds.TryGetBlockIndex(worldPosition, out int blockIndex))
            {
                return -1;
            }

            return blockIndex;
        }

        private void SetActiveBlock(int activeBlockIndex)
        {
            bool hasActiveBlock = activeBlockIndex >= 0;

            if (_terrainLayer != null)
            {
                _terrainLayer.SetActive(hasActiveBlock);
            }

            if (_fallbackRoot != null)
            {
                _fallbackRoot.SetActive(!hasActiveBlock);
            }

            if (_blockRects == null)
            {
                return;
            }

            for (int i = 0; i < _blockRects.Length; i++)
            {
                if (_blockRects[i] != null)
                {
                    _blockRects[i].gameObject.SetActive(i == activeBlockIndex);
                }
            }
        }

        private void DeactivateMarkers(int startIndex = 0)
        {
            for (int i = startIndex; i < _markerPool.Count; i++)
            {
                _markerPool[i].gameObject.SetActive(false);
            }
        }

        private void RenderFallback(Vector3 worldPosition, bool hasWorldPosition)
        {
            WarnIfActiveBlockUnavailable(worldPosition, hasWorldPosition);
            DeactivateMarkers();
        }

        private void WarnIfActiveBlockUnavailable(Vector3 worldPosition, bool hasWorldPosition)
        {
            if (_activeBlockWarningLogged)
            {
                return;
            }

            _activeBlockWarningLogged = true;
            string message = hasWorldPosition
                ? $"Local Navigator position has no matching map block. worldPosition={worldPosition}"
                : "Local Navigator pose is unavailable. Map fallback is active.";

            Debug.LogWarning(message, this);
        }

        private Color GetMarkerColor(MapMarkerData marker)
        {
            if (marker.Type != MapMarkerType.Player && marker.Type != MapMarkerType.Direction)
            {
                return _defaultMarkerColor;
            }

            switch (marker.OwnerRole)
            {
                case PlayerRole.Leader:
                    return _leaderMarkerColor;
                case PlayerRole.Navigator:
                    return _navigatorMarkerColor;
                case PlayerRole.None:
                default:
                    return _defaultMarkerColor;
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

        private void WarnIfAspectMismatch(int blockIndex, RectTransform targetRect)
        {
            if (_aspectWarningTolerance <= 0f ||
                _aspectWarningBlocks.Contains(blockIndex) ||
                _mapBounds == null ||
                !_mapBounds.TryGetWorldAspect(blockIndex, out float worldAspect))
            {
                return;
            }

            Rect rect = targetRect.rect;
            if (Mathf.Approximately(rect.height, 0f))
            {
                return;
            }

            float rectAspect = Mathf.Abs(rect.width / rect.height);
            if (Mathf.Approximately(rectAspect, 0f))
            {
                return;
            }

            float aspectDifference = Mathf.Abs(worldAspect - rectAspect) / Mathf.Max(worldAspect, rectAspect);
            if (aspectDifference <= _aspectWarningTolerance)
            {
                return;
            }

            _aspectWarningBlocks.Add(blockIndex);
            Debug.LogWarning(
                $"Map block aspect mismatch. blockIndex={blockIndex}, worldAspect={worldAspect:F3}, rectAspect={rectAspect:F3}, tolerance={_aspectWarningTolerance:F3}",
                this);
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
