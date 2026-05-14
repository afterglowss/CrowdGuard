using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도에서 하나의 구간을 나타내는 블럭 설정입니다.
    /// Transform 앵커가 있으면 앵커 위치를 우선 사용하고, 없으면 Vector 범위를 사용합니다.
    /// </summary>
    [Serializable]
    public class MapBlock
    {
        [SerializeField] private string _label = "Map Block";
        [SerializeField] private Transform _minPoint;
        [SerializeField] private Transform _maxPoint;
        [SerializeField] private Vector2 _worldMin;
        [SerializeField] private Vector2 _worldMax;
        [SerializeField] private RectTransform _blockRect;

        /// <summary>
        /// 블럭 표시 이름입니다.
        /// </summary>
        public string Label => _label;

        /// <summary>
        /// 마커가 배치될 UI 영역입니다.
        /// </summary>
        public RectTransform BlockRect => _blockRect;

        /// <summary>
        /// 현재 설정에서 사용할 수 있는 월드 X/Y 범위를 계산합니다.
        /// </summary>
        public bool TryGetWorldBounds(out Vector2 worldMin, out Vector2 worldMax)
        {
            if (_minPoint != null && _maxPoint != null)
            {
                worldMin = new Vector2(
                    Mathf.Min(_minPoint.position.x, _maxPoint.position.x),
                    Mathf.Min(_minPoint.position.y, _maxPoint.position.y));
                worldMax = new Vector2(
                    Mathf.Max(_minPoint.position.x, _maxPoint.position.x),
                    Mathf.Max(_minPoint.position.y, _maxPoint.position.y));
                return HasArea(worldMin, worldMax);
            }

            if (HasArea(_worldMin, _worldMax))
            {
                worldMin = new Vector2(
                    Mathf.Min(_worldMin.x, _worldMax.x),
                    Mathf.Min(_worldMin.y, _worldMax.y));
                worldMax = new Vector2(
                    Mathf.Max(_worldMin.x, _worldMax.x),
                    Mathf.Max(_worldMin.y, _worldMax.y));
                return true;
            }

            worldMin = Vector2.zero;
            worldMax = Vector2.zero;
            return false;
        }

        /// <summary>
        /// 월드 위치가 이 블럭의 X/Y 범위에 포함되는지 확인합니다.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            if (!TryGetWorldBounds(out Vector2 worldMin, out Vector2 worldMax))
            {
                return false;
            }

            return worldPosition.x >= worldMin.x &&
                   worldPosition.x <= worldMax.x &&
                   worldPosition.y >= worldMin.y &&
                   worldPosition.y <= worldMax.y;
        }

        /// <summary>
        /// 월드 위치를 이 블럭 안의 0~1 정규화 좌표로 변환합니다.
        /// </summary>
        public bool TryWorldToNormalized(Vector3 worldPosition, bool clampToBounds, out Vector2 normalized)
        {
            if (!TryGetWorldBounds(out Vector2 worldMin, out Vector2 worldMax))
            {
                normalized = Vector2.zero;
                return false;
            }

            float x = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPosition.x);
            float y = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPosition.y);

            if (clampToBounds)
            {
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
            }

            normalized = new Vector2(x, y);
            return true;
        }

        private bool HasArea(Vector2 worldMin, Vector2 worldMax)
        {
            return !Mathf.Approximately(worldMin.x, worldMax.x) &&
                   !Mathf.Approximately(worldMin.y, worldMax.y);
        }
    }
}
