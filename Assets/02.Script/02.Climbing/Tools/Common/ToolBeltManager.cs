using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Common
{
    /// <summary>
    /// 플레이어 자식 오브젝트로 부착.
    /// 자식 계층의 ToolIdentity를 자동 탐색하여 역할에 따라 도구를 활성화/비활성화합니다.
    /// </summary>
    public class ToolBeltManager : MonoBehaviour
    {
        [Header("Role Settings")]
        [SerializeField] private PlayerRole _currentRole = PlayerRole.Leader;

        private ToolIdentity[] _tools;

        private void Awake()
        {
            _tools = GetComponentsInChildren<ToolIdentity>(true);
            InitializeTools();
        }

        private void Start()
        {
            ApplyRole(_currentRole);
        }

        /// <summary>
        /// 모든 도구를 파우치 위치에 정렬하고 초기 물리 상태를 설정합니다.
        /// </summary>
        private void InitializeTools()
        {
            foreach (var tool in _tools)
            {
                if (tool.homePouch != null)
                {
                    tool.transform.SetPositionAndRotation(
                        tool.homePouch.position,
                        tool.homePouch.rotation);
                    tool.transform.SetParent(tool.homePouch);
                }

                var rb = tool.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }
        }

        /// <summary>
        /// 역할을 변경하고 도구 활성 상태를 갱신합니다.
        /// 네트워크 역할 할당 시스템에서 호출하는 진입점입니다.
        /// </summary>
        public void SetRole(PlayerRole newRole)
        {
            _currentRole = newRole;
            ApplyRole(_currentRole);
        }

        /// <summary>
        /// ToolIdentity.validRoles에 따라 도구를 활성화/비활성화합니다.
        /// </summary>
        private void ApplyRole(PlayerRole role)
        {
            foreach (var tool in _tools)
            {
                tool.gameObject.SetActive(tool.IsValidForRole(role));
            }
        }
    }
}
