using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도에서 하나의 월드 구간을 나타내는 블록 설정입니다.
    /// UI RectTransform은 런타임에 생성되는 MapView가 인덱스로 따로 연결합니다.
    /// </summary>
    [Serializable]
    public class MapBlock
    {
        [SerializeField] private string _label = "Map Block";
        [SerializeField] private Transform _blockRoot;
        [SerializeField] private Transform _minPoint;
        [SerializeField] private Transform _maxPoint;
        [SerializeField] private Vector2 _worldMin;
        [SerializeField] private Vector2 _worldMax;

        /// <summary>
        /// 블록 표시 이름입니다. UI block 매칭에는 배열 인덱스를 사용합니다.
        /// </summary>
        public string Label => _label;

        /// <summary>
        /// 현재 설정에서 사용할 수 있는 월드 X/Y 범위를 계산합니다.
        /// </summary>
        public bool TryGetWorldBounds(out Vector2 worldMin, out Vector2 worldMax)
        {
            if (TryGetPointPair(out Transform firstPoint, out Transform secondPoint))
            {
                worldMin = new Vector2(
                    Mathf.Min(firstPoint.position.x, secondPoint.position.x),
                    Mathf.Min(firstPoint.position.y, secondPoint.position.y));
                worldMax = new Vector2(
                    Mathf.Max(firstPoint.position.x, secondPoint.position.x),
                    Mathf.Max(firstPoint.position.y, secondPoint.position.y));
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
        /// 월드 위치가 이 블록의 X/Y 범위에 포함되는지 확인합니다.
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
        /// 월드 위치를 이 블록 안의 0~1 정규화 좌표로 변환합니다.
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

        private bool TryGetPointPair(out Transform firstPoint, out Transform secondPoint)
        {
            if (_blockRoot != null && _blockRoot.childCount >= 2)
            {
                firstPoint = _blockRoot.GetChild(0);
                secondPoint = _blockRoot.GetChild(1);
                return firstPoint != null && secondPoint != null;
            }

            if (_minPoint != null && _maxPoint != null)
            {
                firstPoint = _minPoint;
                secondPoint = _maxPoint;
                return true;
            }

            firstPoint = null;
            secondPoint = null;
            return false;
        }

        private bool HasArea(Vector2 worldMin, Vector2 worldMax)
        {
            return !Mathf.Approximately(worldMin.x, worldMax.x) &&
                   !Mathf.Approximately(worldMin.y, worldMax.y);
        }
    }
}
