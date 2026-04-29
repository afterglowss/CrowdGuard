using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Common
{
    /// <summary>
    /// 도구의 메타 정보를 담는 식별 컴포넌트.
    /// 도구 종류, 사용 가능한 역할, 복귀할 파우치 위치를 관리합니다.
    /// ToolBeltManager가 GetComponentsInChildren으로 자동 탐색하여 역할 기반 활성화/비활성화에 사용합니다.
    /// </summary>
    public class ToolIdentity : MonoBehaviour
    {
        [Header("Tool Info")]
        [SerializeField] private ToolType _toolType;

        [Tooltip("이 도구를 사용할 수 있는 역할 목록")]
        [SerializeField] private PlayerRole[] _validRoles;

        [Tooltip("도구가 복귀할 파우치 위치. 비워두면 Awake 시 부모 Transform으로 자동 설정.")]
        public Transform homePouch;

        /// <summary>
        /// 도구의 종류.
        /// </summary>
        public ToolType ToolType => _toolType;

        /// <summary>
        /// 이 도구를 사용할 수 있는 역할 배열.
        /// </summary>
        public PlayerRole[] ValidRoles => _validRoles;

        private void Awake()
        {
            if (homePouch == null)
                homePouch = transform.parent;
        }

        /// <summary>
        /// 지정된 역할이 이 도구를 사용할 수 있는지 확인합니다.
        /// </summary>
        public bool IsValidForRole(PlayerRole role)
        {
            return Array.Exists(_validRoles, r => r == role);
        }
    }
}
