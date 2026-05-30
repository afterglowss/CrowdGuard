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
        /// 등록된 지도 블록 수입니다.
        /// </summary>
        public int BlockCount => _blocks == null ? 0 : _blocks.Length;

        /// <summary>
        /// 하나 이상의 유효한 지도 블록 bounds가 있는지 확인합니다.
        /// </summary>
        public bool HasUsableBlockBounds => HasUsableBlocks();

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

        /// <summary>
        /// 지정한 지도 블록의 월드 X/Y bounds를 반환합니다.
        /// </summary>
        public bool TryGetBlockWorldBounds(int blockIndex, out Vector2 worldMin, out Vector2 worldMax)
        {
            if (_blocks == null ||
                blockIndex < 0 ||
                blockIndex >= _blocks.Length ||
                _blocks[blockIndex] == null)
            {
                worldMin = Vector2.zero;
                worldMax = Vector2.zero;
                return false;
            }

            return _blocks[blockIndex].TryGetWorldBounds(out worldMin, out worldMax);
        }

        /// <summary>
        /// 지정한 지도 블록의 표시 이름을 반환합니다.
        /// </summary>
        public string GetBlockLabel(int blockIndex)
        {
            if (_blocks == null ||
                blockIndex < 0 ||
                blockIndex >= _blocks.Length ||
                _blocks[blockIndex] == null)
            {
                return string.Empty;
            }

            return _blocks[blockIndex].Label;
        }

        /// <summary>
        /// 블록이 없을 때 사용하는 전체 지도 월드 X/Y bounds를 반환합니다.
        /// </summary>
        public bool TryGetFallbackWorldBounds(out Vector2 worldMin, out Vector2 worldMax)
        {
            worldMin = new Vector2(
                Mathf.Min(_worldMin.x, _worldMax.x),
                Mathf.Min(_worldMin.y, _worldMax.y));
            worldMax = new Vector2(
                Mathf.Max(_worldMin.x, _worldMax.x),
                Mathf.Max(_worldMin.y, _worldMax.y));

            return !Mathf.Approximately(worldMin.x, worldMax.x) &&
                   !Mathf.Approximately(worldMin.y, worldMax.y);
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

    }
}
