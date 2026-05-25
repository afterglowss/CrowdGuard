using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Map
{
    /// <summary>
    /// 월드 X/Y 좌표를 지도 좌표로 변환합니다.
    /// 월드 블록과 UI 블록은 배열 인덱스로 매칭합니다.
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
        /// 월드 위치가 들어갈 지도 블록 인덱스와 해당 블록 내부 정규화 좌표를 찾습니다.
        /// 사용할 수 있는 월드 블록이 없으면 blockIndex -1과 단일 지도 정규화 좌표를 반환합니다.
        /// </summary>
        public bool TryWorldToMapPosition(
            Vector3 worldPosition,
            out int blockIndex,
            out Vector2 normalized)
        {
            if (HasUsableBlocks())
            {
                if (TryGetBlockIndex(worldPosition, out blockIndex) &&
                    _blocks[blockIndex].TryWorldToNormalized(worldPosition, _clampToBounds, out normalized))
                {
                    return true;
                }

                blockIndex = -1;
                normalized = Vector2.zero;
                return false;
            }

            blockIndex = -1;
            normalized = WorldToNormalized(worldPosition);
            return true;
        }

        /// <summary>
        /// 지정된 지도 블록 안에서 월드 위치를 0~1 정규화 좌표로 변환합니다.
        /// </summary>
        public bool TryWorldToMapPositionInBlock(
            int blockIndex,
            Vector3 worldPosition,
            out Vector2 normalized)
        {
            if (_blocks == null ||
                blockIndex < 0 ||
                blockIndex >= _blocks.Length ||
                _blocks[blockIndex] == null ||
                !_blocks[blockIndex].Contains(worldPosition))
            {
                normalized = Vector2.zero;
                return false;
            }

            return _blocks[blockIndex].TryWorldToNormalized(
                worldPosition,
                _clampToBounds,
                out normalized);
        }

        /// <summary>
        /// 지도 블록 또는 fallback 전체 지도의 월드 가로/세로 비율을 구합니다.
        /// </summary>
        public bool TryGetWorldAspect(int blockIndex, out float aspect)
        {
            if (blockIndex >= 0)
            {
                if (_blocks == null ||
                    blockIndex >= _blocks.Length ||
                    _blocks[blockIndex] == null ||
                    !_blocks[blockIndex].TryGetWorldBounds(out Vector2 blockMin, out Vector2 blockMax))
                {
                    aspect = 0f;
                    return false;
                }

                return TryCalculateAspect(blockMin, blockMax, out aspect);
            }

            return TryCalculateAspect(_worldMin, _worldMax, out aspect);
        }

        /// <summary>
        /// 월드 위치를 포함하는 지도 블록 인덱스를 반환합니다.
        /// </summary>
        public bool TryGetBlockIndex(Vector3 worldPosition, out int blockIndex)
        {
            if (_blocks == null)
            {
                blockIndex = -1;
                return false;
            }

            for (int i = _blocks.Length - 1; i >= 0; i--)
            {
                MapBlock candidate = _blocks[i];
                if (candidate != null && candidate.Contains(worldPosition))
                {
                    blockIndex = i;
                    return true;
                }
            }

            blockIndex = -1;
            return false;
        }

        private bool TryCalculateAspect(Vector2 worldMin, Vector2 worldMax, out float aspect)
        {
            float width = Mathf.Abs(worldMax.x - worldMin.x);
            float height = Mathf.Abs(worldMax.y - worldMin.y);

            if (Mathf.Approximately(width, 0f) || Mathf.Approximately(height, 0f))
            {
                aspect = 0f;
                return false;
            }

            aspect = width / height;
            return true;
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
