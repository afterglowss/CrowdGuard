using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.IceAnchor
{
    /// <summary>
    /// 아이스 앵커의 순수 데이터 및 상태를 관리하는 Model (서버 동기화 대상 변수들).
    /// 앵커는 ㄱ자 형태이며 손잡이를 잡고 벽에 삽입 후 게이트밸브처럼 돌려 체결합니다.
    /// </summary>
    public class IceAnchorModel : MonoBehaviour
    {
        // --- State Data ---
        [Header("State (Read-Only in Inspector)")]
        [SerializeField] private bool _isHeld = false;
        [SerializeField] private bool _isContactingWall = false;
        [SerializeField] private bool _isInserted = false;
        [SerializeField] private bool _isFullySecured = false;
        [SerializeField][Range(0f, 1f)] private float _screwProgress = 0f;

        // --- Events ---
        public event Action<bool> OnHeldStateChanged;
        public event Action<bool> OnContactingWallChanged;
        public event Action<bool> OnInsertedStateChanged;
        public event Action<float> OnScrewProgressChanged;
        /// <summary>
        /// 체결 상태가 변할 때 발화. true = 완전 체결, false = 해체됨.
        /// </summary>
        public event Action<bool> OnFullySecuredChanged;

        // --- Properties ---
        public bool IsHeld
        {
            get => _isHeld;
            set
            {
                if (_isHeld != value)
                {
                    _isHeld = value;
                    OnHeldStateChanged?.Invoke(_isHeld);
                }
            }
        }

        public bool IsContactingWall
        {
            get => _isContactingWall;
            set
            {
                if (_isContactingWall != value)
                {
                    _isContactingWall = value;
                    OnContactingWallChanged?.Invoke(_isContactingWall);
                }
            }
        }

        public bool IsInserted
        {
            get => _isInserted;
            set
            {
                if (_isInserted != value)
                {
                    _isInserted = value;
                    OnInsertedStateChanged?.Invoke(_isInserted);
                }
            }
        }

        public float ScrewProgress
        {
            get => _screwProgress;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (!Mathf.Approximately(_screwProgress, clamped))
                {
                    _screwProgress = clamped;
                    OnScrewProgressChanged?.Invoke(_screwProgress);
                }
            }
        }

        /// <summary>
        /// 완전 체결 상태. 양방향 전환 가능 (역회전으로 해체 시 false).
        /// </summary>
        public bool IsFullySecured
        {
            get => _isFullySecured;
            set
            {
                if (_isFullySecured != value)
                {
                    _isFullySecured = value;
                    OnFullySecuredChanged?.Invoke(_isFullySecured);
                }
            }
        }

        public void ResetState()
        {
            _isHeld = false;
            _isContactingWall = false;
            _isInserted = false;
            _isFullySecured = false;
            _screwProgress = 0f;
        }
    }
}
