using System.Collections.Generic;
using Capstone.Photon.Game;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도 UI가 사용할 런타임 마커 데이터를 수집합니다.
    /// </summary>
    public class MapDataProvider : MonoBehaviour
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private MapMarker[] _staticMarkers;
        [SerializeField] private bool _includeAnchors = true;
        [SerializeField] private bool _includeSavePoint = true;

        private readonly List<MapMarkerData> _markers = new List<MapMarkerData>();

        /// <summary>
        /// 현재 플레이어, 진행 방향, 세이브 포인트, 앵커, 고정 마커를 반환합니다.
        /// </summary>
        public IReadOnlyList<MapMarkerData> GetMarkers()
        {
            _markers.Clear();

            Transform playerTransform = ResolvePlayerTransform();
            if (playerTransform != null)
            {
                _markers.Add(new MapMarkerData(
                    MapMarkerType.Player,
                    playerTransform.position,
                    playerTransform.forward,
                    "Player"));
                _markers.Add(new MapMarkerData(
                    MapMarkerType.Direction,
                    playerTransform.position,
                    playerTransform.forward,
                    "Direction"));
            }

            SavePointManager savePointManager = SavePointManager.Instance;
            if (savePointManager != null)
            {
                if (_includeSavePoint)
                {
                    _markers.Add(new MapMarkerData(
                        MapMarkerType.SavePoint,
                        savePointManager.LastSavePosition,
                        Vector3.forward,
                        "Save Point"));
                }

                if (_includeAnchors)
                {
                    foreach (Vector3 anchorPosition in savePointManager.SecuredAnchorPositions)
                    {
                        _markers.Add(new MapMarkerData(
                            MapMarkerType.Anchor,
                            anchorPosition,
                            Vector3.forward,
                            "Anchor"));
                    }
                }
            }

            if (_staticMarkers != null)
            {
                foreach (MapMarker marker in _staticMarkers)
                {
                    if (marker != null)
                    {
                        _markers.Add(marker.ToData());
                    }
                }
            }

            return _markers;
        }

        private Transform ResolvePlayerTransform()
        {
            if (_playerTransform != null)
            {
                return _playerTransform;
            }

            if (GamePlayerModel.LocalPlayerModel != null &&
                GamePlayerModel.LocalPlayerModel.body != null)
            {
                return GamePlayerModel.LocalPlayerModel.body.transform;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }
    }
}
