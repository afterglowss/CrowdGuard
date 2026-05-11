using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 월드 X/Y 좌표를 고정된 정면 지도 좌표로 변환합니다.
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 _worldMin = new Vector2(-10f, 0f);
        [SerializeField] private Vector2 _worldMax = new Vector2(10f, 30f);
        [SerializeField] private bool _clampToBounds = true;

        /// <summary>
        /// 월드 위치를 0~1 범위의 지도 정규화 좌표로 변환합니다.
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            float x = Mathf.InverseLerp(_worldMin.x, _worldMax.x, worldPosition.x);
            float y = Mathf.InverseLerp(_worldMin.y, _worldMax.y, worldPosition.y);

            if (_clampToBounds)
            {
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
            }

            return new Vector2(x, y);
        }

        /// <summary>
        /// 정규화 좌표를 지도 RectTransform의 anchoredPosition으로 변환합니다.
        /// </summary>
        public Vector2 NormalizedToRectPosition(Vector2 normalized, RectTransform mapRect)
        {
            if (mapRect == null)
            {
                return Vector2.zero;
            }

            Rect rect = mapRect.rect;
            return new Vector2(
                (normalized.x - 0.5f) * rect.width,
                (normalized.y - 0.5f) * rect.height);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = new Vector3(
                (_worldMin.x + _worldMax.x) * 0.5f,
                (_worldMin.y + _worldMax.y) * 0.5f,
                transform.position.z);
            Vector3 size = new Vector3(
                Mathf.Abs(_worldMax.x - _worldMin.x),
                Mathf.Abs(_worldMax.y - _worldMin.y),
                0.1f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
