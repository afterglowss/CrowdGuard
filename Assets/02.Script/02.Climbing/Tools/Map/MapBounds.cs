using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 월드 X/Y 좌표를 지도 좌표로 변환합니다.
    /// 블럭이 설정되어 있으면 해당 블럭 내부 좌표로, 없으면 기존 단일 지도 좌표로 변환합니다.
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 _worldMin = new Vector2(-10f, 0f);
        [SerializeField] private Vector2 _worldMax = new Vector2(10f, 30f);
        [SerializeField] private bool _clampToBounds = true;
        [SerializeField] private MapBlock[] _blocks;

        /// <summary>
        /// 월드 위치를 단일 지도 기준 0~1 정규화 좌표로 변환합니다.
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
        /// 정규화 좌표를 지정된 RectTransform의 anchoredPosition으로 변환합니다.
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

        /// <summary>
        /// 월드 위치가 들어갈 지도 블럭과 해당 블럭 내부 anchoredPosition을 찾습니다.
        /// 블럭이 설정되지 않은 경우 fallbackRect를 사용해 기존 단일 지도 방식으로 동작합니다.
        /// </summary>
        public bool TryWorldToRectPosition(
            Vector3 worldPosition,
            RectTransform fallbackRect,
            out RectTransform targetRect,
            out Vector2 anchoredPosition)
        {
            if (HasBlocks())
            {
                if (TryGetBlock(worldPosition, out MapBlock block) && block.BlockRect != null)
                {
                    targetRect = block.BlockRect;
                    anchoredPosition = NormalizedToRectPosition(
                        block.WorldToNormalized(worldPosition, _clampToBounds),
                        targetRect);
                    return true;
                }

                targetRect = null;
                anchoredPosition = Vector2.zero;
                return false;
            }

            targetRect = fallbackRect;
            anchoredPosition = NormalizedToRectPosition(WorldToNormalized(worldPosition), fallbackRect);
            return targetRect != null;
        }

        private bool TryGetBlock(Vector3 worldPosition, out MapBlock block)
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                MapBlock candidate = _blocks[i];
                if (candidate != null && candidate.Contains(worldPosition))
                {
                    block = candidate;
                    return true;
                }
            }

            block = null;
            return false;
        }

        private bool HasBlocks()
        {
            return _blocks != null && _blocks.Length > 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (HasBlocks())
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _blocks.Length; i++)
                {
                    DrawBlockGizmo(_blocks[i]);
                }

                return;
            }

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

        private void DrawBlockGizmo(MapBlock block)
        {
            if (block == null)
            {
                return;
            }

            Vector2 worldMin = block.WorldMin;
            Vector2 worldMax = block.WorldMax;
            Vector3 center = new Vector3(
                (worldMin.x + worldMax.x) * 0.5f,
                (worldMin.y + worldMax.y) * 0.5f,
                transform.position.z);
            Vector3 size = new Vector3(
                Mathf.Abs(worldMax.x - worldMin.x),
                Mathf.Abs(worldMax.y - worldMin.y),
                0.1f);

            Gizmos.DrawWireCube(center, size);
        }
    }
}
