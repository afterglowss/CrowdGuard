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

        [Header("Equipment (프리팹 인스펙터에서 연결)")]
        [Tooltip("왼손 IceAxeModel 컴포넌트 — 프리팹 자식 오브젝트에서 드래그")]
        public IceAxeModel leftIceAxe;
        [Tooltip("오른손 IceAxeModel 컴포넌트 — 프리팹 자식 오브젝트에서 드래그")]
        public IceAxeModel rightIceAxe;

        public override void Spawned()
        {
            base.Spawned();
            // TODO : 오브젝트 등록하기 현재 권한, 해당 오브젝트
            Debug.Log($"{RoleManager.Instance}");
            CurrentRole = RoleManager.Instance.Roles[Object.StateAuthority];
            Debug.Log($"{CurrentRole}");
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
                // 2. RoleVisualManager: 역할별 시야(셰이더 글로벌 변수) 적용
                if (RoleVisualManager.Instance != null)
                {
                    RoleVisualManager.Instance.SetRole(isLeader);
                    Debug.Log($"[GamePlayerModel] RoleVisualManager 역할 적용: isLeader={isLeader}");
                }
                else
                {
                    Debug.LogWarning("[GamePlayerModel] RoleVisualManager.Instance가 null입니다. 씬에 배치되어 있는지 확인하세요.");
                }
            }
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
