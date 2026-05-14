using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 월드 X/Y 좌표를 지도 좌표로 변환합니다.
    /// 블럭이 설정되어 있으면 Transform 앵커, Vector 범위 순서로 블럭 내부 좌표를 사용합니다.
    /// 사용할 수 있는 블럭 범위가 없으면 기존 단일 지도 좌표로 변환합니다.
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
        /// 사용 가능한 블럭 범위가 없으면 fallbackRect를 사용해 기존 단일 지도 방식으로 동작합니다.
        /// </summary>
        public bool TryWorldToRectPosition(
            Vector3 worldPosition,
            RectTransform fallbackRect,
            out RectTransform targetRect,
            out Vector2 anchoredPosition)
        {
            if (HasUsableBlocks())
            {
                if (TryGetBlock(worldPosition, out MapBlock block) &&
                    block.BlockRect != null &&
                    block.TryWorldToNormalized(worldPosition, _clampToBounds, out Vector2 blockNormalized))
                {
                    targetRect = block.BlockRect;
                    anchoredPosition = NormalizedToRectPosition(blockNormalized, targetRect);
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
            if (_blocks == null)
            {
                block = null;
                return false;
            }

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

        private bool HasUsableBlocks()
        {
            if (_blocks == null)
            {
                return false;
            }

            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] != null && _blocks[i].TryGetWorldBounds(out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (HasUsableBlocks())
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
            if (block == null || !block.TryGetWorldBounds(out Vector2 worldMin, out Vector2 worldMax))
            {
                return;
            }

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
