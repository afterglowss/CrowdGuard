using Fusion;
using UnityEngine;
using CrowdGuard.Climbing.Tools.IceAxe;
using CrowdGuard.Climbing.Tools.Common;

namespace Capstone.Photon.Game
{
    public class GamePlayerModel : PlayerModel
    {
        public static GamePlayerModel LocalPlayerModel;
        [Networked] private PlayerRole CurrentRole { get; set; }

        /// <summary>로컬 클라이언트에서 이 모델이 리더인지 빠르게 확인합니다.</summary>
        public bool IsLeader => CurrentRole == PlayerRole.Leader;

        [Header("Equipment (프리팹 인스펙터에서 연결)")]
        [Tooltip("왼손 IceAxeModel 컴포넌트 — 프리팹 자식 오브젝트에서 드래그")]
        public IceAxeModel leftIceAxe;
        [Tooltip("오른손 IceAxeModel 컴포넌트 — 프리팹 자식 오브젝트에서 드래그")]
        public IceAxeModel rightIceAxe;

        public override void Spawned()
        {
            base.Spawned();

            if (RoleManager.Instance == null)
            {
                Debug.LogWarning("[GamePlayerModel] RoleManager.Instance가 null입니다. 역할 데이터를 읽을 수 없습니다.");
                return;
            }

            if (!RoleManager.Instance.Roles.TryGet(Object.StateAuthority, out PlayerRole role))
            {
                Debug.LogWarning($"[GamePlayerModel] Roles 딕셔너리에 {Object.StateAuthority} 항목이 없습니다.");
                return;
            }

            CurrentRole = role;

            // None(관전자)은 게임 시스템(로프·도구 등)에 영향을 주지 않음
            if (CurrentRole == PlayerRole.None)
            {
                Debug.Log("[GamePlayerModel] 관전자(None) 역할 → 게임 시스템 초기화 건너뜀");
                return;
            }

            PlayerManager.Instance.SetPlayer(CurrentRole, Object);

            bool isLeader = CurrentRole == PlayerRole.Leader;

            // 1. ToolBeltManager: 역할에 따라 도구 활성화/비활성화
            // (ToolBeltManager.Start()는 네트워크 Role 확정 전에 실행되므로 여기서 재적용)
            var toolBelt = GetComponentInChildren<ToolBeltManager>();
            if (toolBelt != null)
            {
                toolBelt.SetRole(CurrentRole);
                Debug.Log($"[GamePlayerModel] ToolBeltManager 역할 적용: {CurrentRole}");
            }

            // 이 오브젝트를 소유한 로컬 머신에서만 클라이언트 측 역할 반영 처리
            if (Object.HasStateAuthority)
            {

                // 2. RoleVisualManager: 역할별 셰이더 글로벌 변수 적용
                if (RoleVisualManager.Instance != null)
                    RoleVisualManager.Instance.SetRole(isLeader);
                else
                    Debug.LogWarning("[GamePlayerModel] RoleVisualManager가 게임 씬에 배치되어 있는지 확인하세요.");
            }

            // 스폰 완료 알림 → 양쪽 모두 완료 시 GameManager가 RoleManager를 파괴
            GameManager.Instance?.OnPlayerModelSpawned();
        }

        public override void Init(LocalPlayerController controller)
        {
            base.Init(controller);
            LocalPlayerModel = this;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            if (LocalPlayerModel == this)
            {
                LocalPlayerModel = null;
            }
        }
    }
}
