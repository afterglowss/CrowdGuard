using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.IceAxe
{
    /// <summary>
    /// 아이스 바일의 순수 데이터 및 상태를 관리하는 Model (서버 동기화 대상 변수들)
    /// </summary>
    public class IceAxeModel : MonoBehaviour
    {
        public enum HandSide { Left, Right }

        [Header("Identity")]
        [Tooltip("이 바일이 왼손용인지 오른손용인지 인스펙터에서 지정합니다.")]
        public HandSide Side = HandSide.Left;

        // --- State Data ---
        [Header("State")]
        [SerializeField] private bool _isHeld = false;
        [SerializeField] private bool _isAttachedToWall = false;

        // --- Events (상태 변화를 Controller나 View에 알림) ---
        public event Action<bool> OnHeldStateChanged;
        public event Action<bool> OnAttachedStateChanged;

        // --- Properties ---
        public bool IsHeld
        {
            get => _isHeld;
            set
            {
                if (_isHeld != value)
                {
                    _isHeld = value;
                    OnHeldStateChanged?.Invoke(_isHeld); // 값이 변할 때만 이벤트 발생
                }
            }
        }

        public bool IsAttachedToWall
        {
            get => _isAttachedToWall;
            set
            {
                if (_isAttachedToWall != value)
                {
                    _isAttachedToWall = value;
                    OnAttachedStateChanged?.Invoke(_isAttachedToWall);
                }
            }
        }
    }
}
