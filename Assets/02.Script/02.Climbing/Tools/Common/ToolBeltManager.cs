using System;
using UnityEngine;

namespace CrowdGuard.Climbing.Tools.Common
{
    [Serializable]
    public class ToolSlot
    {
        [Tooltip("알아보기 쉬운 슬롯명 (예: LeftHip)")]
        public string slotName;

        [Tooltip("파우치 위치 (도구가 복귀할 기준점)")]
        public Transform pouchTransform;

        [Tooltip("이 슬롯에 할당될 도구 프리팹 혹은 씬 내 인스턴스")]
        public GameObject toolTarget;

        [Tooltip("어떤 역할에서 활성화할 것인지")]
        public PlayerRole[] validRoles;

        // 인스턴스가 씬에 존재하는지 캐싱하는 용도
        [HideInInspector] public GameObject activeInstance;
    }

    /// <summary>
    /// 플레이어 자식 오브젝트로 부착.
    /// 설정된 역할에 따라 파우치(슬롯)와 도구를 활성화/비활성화.
    /// XR Socket을 쓰지 않고 개별 도구의 RetractableObject에 시작부터 파우치 위치를 주입하는 역할도 겸함.
    /// </summary>
    public class ToolBeltManager : MonoBehaviour
    {
        [Header("Role Settings")]
        [SerializeField] private PlayerRole _currentRole = PlayerRole.Leader;

        [Header("Pouch Slots")]
        [SerializeField] private ToolSlot[] _slots;

        private void Start()
        {
            InitializeSlots();
            ApplyRole(_currentRole);
        }

        private void InitializeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot.toolTarget != null)
                {
                    // 씬에 이미 배치된 도구인지 체크
                    if (slot.toolTarget.scene.IsValid())
                    {
                        slot.activeInstance = slot.toolTarget;
                    }
                    else // 프리팹이면 생성
                    {
                        slot.activeInstance = Instantiate(slot.toolTarget, slot.pouchTransform.position, slot.pouchTransform.rotation);
                        // XR Interaction 안정성을 위해 파우치에 일단 종속
                        slot.activeInstance.transform.SetParent(slot.pouchTransform);
                    }

                    // 해당 도구가 RetractableObject를 쓴다면 파우치 트랜스폼 자동 주입
                    var retractable = slot.activeInstance.GetComponent<RetractableObject>();
                    if (retractable != null)
                    {
                        retractable.pouchTransform = slot.pouchTransform;
                    }

                    // 도구가 처음 생성되거나 관리될 때 파우치 위치로 강제 이동 및 종속.
                    slot.activeInstance.transform.SetPositionAndRotation(slot.pouchTransform.position, slot.pouchTransform.rotation);
                    slot.activeInstance.transform.SetParent(slot.pouchTransform);

                    // --- [Fix] 영구 추락 방지: 초기 상태에서 파우치에 있을 때는 중력 영향을 받지 않도록 강제 설정 ---
                    var rb = slot.activeInstance.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }
                }
            }
        }

        public void SetRole(PlayerRole newRole)
        {
            _currentRole = newRole;
            ApplyRole(_currentRole);
        }

        private void ApplyRole(PlayerRole role)
        {
            foreach (var slot in _slots)
            {
                if (slot.activeInstance != null)
                {
                    bool isValid = false;
                    foreach (var r in slot.validRoles)
                    {
                        if (r == role)
                        {
                            isValid = true;
                            break;
                        }
                    }

                    slot.activeInstance.SetActive(isValid);
                }
            }
        }
    }
}
