using System.Collections.Generic;
using Capstone.Photon.Game;
using CrowdGuard.Climbing.Tools.Common;
using Fusion;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도 UI가 사용할 마커 데이터를 런타임 상태에서 수집합니다.
    /// </summary>
    public class MapDataProvider : MonoBehaviour
    {
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private MapMarker[] _staticMarkers;
        [SerializeField] private bool _includeAnchors = true;
        [SerializeField] private bool _includeSavePoint = true;

        private readonly List<MapMarkerData> _markers = new List<MapMarkerData>();

        /// <summary>
        /// 플레이어, 진행 방향, 세이브 포인트, 앵커, 고정 마커를 반환합니다.
        /// </summary>
        public IReadOnlyList<MapMarkerData> GetMarkers()
        {
            _markers.Clear();

            AddPlayerMarkers();
            AddSavePointMarkers();
            AddStaticMarkers();

            return _markers;
        }

        private void AddPlayerMarkers()
        {
            if (_playerTransform != null)
            {
                AddPlayerMarker(_playerTransform, "Player");
                return;
            }

            PlayerManager playerManager = PlayerManager.Instance;
            if (playerManager == null || playerManager.players == null)
            {
                return;
            }

            AddRolePlayerMarker(playerManager, PlayerRole.Leader, "Leader");
            AddRolePlayerMarker(playerManager, PlayerRole.Navigator, "Navigator");
        }

        private void AddRolePlayerMarker(PlayerManager playerManager, PlayerRole role, string label)
        {
            if (!playerManager.players.TryGetValue(role, out NetworkObject playerObject) ||
                playerObject == null ||
                !playerObject.TryGetComponent(out GamePlayerModel playerModel) ||
                playerModel.body == null)
            {
                return;
            }

            AddPlayerMarker(playerModel.body.transform, label);
        }

        private void AddPlayerMarker(Transform playerTransform, string label)
        {
            _markers.Add(new MapMarkerData(
                MapMarkerType.Player,
                playerTransform.position,
                playerTransform.forward,
                label));
            _markers.Add(new MapMarkerData(
                MapMarkerType.Direction,
                playerTransform.position,
                playerTransform.forward,
                $"{label} Direction"));
        }

        private void AddSavePointMarkers()
        {
            SavePointManager savePointManager = SavePointManager.Instance;
            if (savePointManager == null)
            {
                return;
            }

            if (_includeSavePoint)
            {
                _markers.Add(new MapMarkerData(
                    MapMarkerType.SavePoint,
                    savePointManager.LastSavePosition,
                    Vector3.forward,
                    "Save Point"));
            }

            if (!_includeAnchors)
            {
                return;
            }

            foreach (Vector3 anchorPosition in savePointManager.SecuredAnchorPositions)
            {
                _markers.Add(new MapMarkerData(
                    MapMarkerType.Anchor,
                    anchorPosition,
                    Vector3.forward,
                    "Anchor"));
            }
        }

        private void AddStaticMarkers()
        {
            if (_staticMarkers == null)
            {
                return;
            }

            foreach (MapMarker marker in _staticMarkers)
            {
                if (marker != null)
                {
                    _markers.Add(marker.ToData());
                }
            }
        }
    }
}
