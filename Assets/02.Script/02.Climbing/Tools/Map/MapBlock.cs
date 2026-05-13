using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 지도에서 하나의 구간을 나타내는 블럭 설정입니다.
    /// 월드 X/Y 범위와 이 범위가 표시될 UI RectTransform을 함께 관리합니다.
    /// </summary>
    [Serializable]
    public class MapBlock
    {
        [SerializeField] private string _label = "Map Block";
        [SerializeField] private Vector2 _worldMin = new Vector2(-10f, 0f);
        [SerializeField] private Vector2 _worldMax = new Vector2(10f, 10f);
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
        /// 이 블럭의 월드 X/Y 최소 범위입니다.
        /// </summary>
        public Vector2 WorldMin => _worldMin;

        /// <summary>
        /// 이 블럭의 월드 X/Y 최대 범위입니다.
        /// </summary>
        public Vector2 WorldMax => _worldMax;

        /// <summary>
        /// 월드 위치가 이 블럭의 X/Y 범위에 포함되는지 확인합니다.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            float minX = Mathf.Min(_worldMin.x, _worldMax.x);
            float maxX = Mathf.Max(_worldMin.x, _worldMax.x);
            float minY = Mathf.Min(_worldMin.y, _worldMax.y);
            float maxY = Mathf.Max(_worldMin.y, _worldMax.y);

            return worldPosition.x >= minX &&
                   worldPosition.x <= maxX &&
                   worldPosition.y >= minY &&
                   worldPosition.y <= maxY;
        }

        /// <summary>
        /// 월드 위치를 이 블럭 안의 0~1 정규화 좌표로 변환합니다.
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPosition, bool clampToBounds)
        {
            float x = Mathf.InverseLerp(_worldMin.x, _worldMax.x, worldPosition.x);
            float y = Mathf.InverseLerp(_worldMin.y, _worldMax.y, worldPosition.y);

            if (clampToBounds)
            {
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
            }

            return new Vector2(x, y);
        }
    }
}
