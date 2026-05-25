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
        [SerializeField] private MapLandmarkDatabase _landmarkDatabase;
        [SerializeField] private bool _includeAnchors = true;
        [SerializeField] private bool _includeSavePoint = true;
        [SerializeField] private bool _includeBakedLandmarks = true;

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
            AddBakedLandmarkMarkers();

            return _markers;
        }

        /// <summary>
        /// 지도 active block 기준으로 사용할 로컬 Navigator 위치와 방향을 반환합니다.
        /// </summary>
        public bool TryGetLocalNavigatorPose(out Vector3 position, out Vector3 forward)
        {
            if (_playerTransform != null)
            {
                position = _playerTransform.position;
                forward = _playerTransform.forward;
                return true;
            }

            position = Vector3.zero;
            forward = Vector3.forward;

            if (!TryGetLocalNavigatorModel(out GamePlayerModel playerModel) ||
                playerModel.body == null)
            {
                return false;
            }

            Transform bodyTransform = playerModel.body.transform;
            Transform directionTransform = playerModel.head != null
                ? playerModel.head.transform
                : bodyTransform;

            position = bodyTransform.position;
            forward = directionTransform.forward;
            return true;
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

        private bool TryGetLocalNavigatorModel(out GamePlayerModel playerModel)
        {
            playerModel = GamePlayerModel.LocalPlayerModel;
            PlayerManager playerManager = PlayerManager.Instance;

            if (playerModel == null ||
                playerManager == null ||
                playerManager.players == null)
            {
                return false;
            }

            return playerManager.players.TryGetValue(PlayerRole.Navigator, out NetworkObject navigatorObject) &&
                   navigatorObject != null &&
                   playerModel.Object == navigatorObject;
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

            Transform directionTransform = playerModel.head != null
                ? playerModel.head.transform
                : playerModel.body.transform;

            AddPlayerMarker(playerModel.body.transform, directionTransform, label, role);
        }

        private void AddPlayerMarker(Transform playerTransform, string label, PlayerRole role = PlayerRole.None)
        {
            AddPlayerMarker(playerTransform, playerTransform, label, role);
        }

        private void AddPlayerMarker(
            Transform playerTransform,
            Transform directionTransform,
            string label,
            PlayerRole role = PlayerRole.None)
        {
            _markers.Add(new MapMarkerData(
                MapMarkerType.Player,
                playerTransform.position,
                directionTransform.forward,
                label,
                true,
                role));
            _markers.Add(new MapMarkerData(
                MapMarkerType.Direction,
                playerTransform.position,
                directionTransform.forward,
                $"{label} Direction",
                true,
                role));
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

        private void AddBakedLandmarkMarkers()
        {
            if (!_includeBakedLandmarks ||
                _landmarkDatabase == null ||
                _landmarkDatabase.Entries == null)
            {
                return;
            }

            foreach (MapLandmarkRecord record in _landmarkDatabase.Entries)
            {
                _markers.Add(record.ToMarkerData());
            }
        }
    }
}
