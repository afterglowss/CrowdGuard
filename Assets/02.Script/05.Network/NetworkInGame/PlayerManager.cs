using Fusion;
using System.Collections.Generic;
using CrowdGuard.Climbing.Tools.Common;
using UnityEngine;

namespace Capstone.Photon.Game
{
    public class PlayerManager : NetworkBehaviour
    {
        public static PlayerManager Instance;
        public Dictionary<PlayerRole, NetworkObject> players;

        public RopeSystem ropeSystem;

        [Tooltip("리더 ↔ 최신 세이브 포인트를 연결하는 안전 밧줄. 씬의 LeaderSafetyRope 컴포넌트를 연결하세요.")]
        public LeaderSafetyRope leaderSafetyRope;

        private void Awake()
        {
            if(!Instance) Instance = this;
            else if(Instance != this) Destroy(gameObject);
            players = new Dictionary<PlayerRole, NetworkObject>();
        }
        
        /// <summary>
        /// 플레이어 입장 시 dictionary에 값 추가
        /// </summary>
        /// <param name="player"></param>
        /// <param name="obj"></param>
        public void SetPlayer(PlayerRole role, NetworkObject obj)
        {
            players[role] = obj;

            foreach (var player in players)
            {
                Debug.Log($"{players.Count} ---- {player.Key} : {player.Value}");
            }

            if (players.Count >= 2)
            {
                SetGameSystem(players[PlayerRole.Leader], players[PlayerRole.Navigator]);
            }
            else
            {
                // 솔로 테스트: 리더 한 명만 스폰되어도 세이프티 로프는 바로 초기화합니다.
                // (RopeSystem 파트너 연결은 2명이 필요하므로 그쪽은 생략)
                if (leaderSafetyRope != null && role == PlayerRole.Leader)
                {
                    if (obj.TryGetComponent(out GamePlayerModel leaderModel))
                        leaderSafetyRope.SetLeaderBody(leaderModel.body.transform);
                }
            }
        }

        public void SetGameSystem(NetworkObject leader, NetworkObject supporter)
        {
            // ── 파트너 로프 (두 플레이어 연결) ──────────────────────────
            if (ropeSystem != null)
            {
                // 로컬 플레이어가 Leader인지 Supporter인지 판별
                bool isLeader = leader.HasStateAuthority;

                NetworkObject myObj      = isLeader ? leader    : supporter;
                NetworkObject partnerObj = isLeader ? supporter : leader;

                if (myObj.TryGetComponent(out GamePlayerModel myModel) &&
                    partnerObj.TryGetComponent(out GamePlayerModel partnerModel))
                {
                    ropeSystem.SetPartners(myModel.body.transform, partnerModel.body.transform);
                }
            }

            // ── 세이프티 로프 (리더 ↔ 최신 세이브 포인트) ───────────────
            // leader.body.transform은 네트워크 동기화된 ObjectTracker이므로
            // 팔로워 클라이언트에서도 리더의 정확한 위치를 추적합니다.
            if (leaderSafetyRope != null)
            {
                if (leader.TryGetComponent(out GamePlayerModel leaderModel))
                    leaderSafetyRope.SetLeaderBody(leaderModel.body.transform);
                else
                    Debug.LogWarning("[PlayerManager] leader NetworkObject에서 GamePlayerModel을 찾지 못했습니다.");
            }

            // TODO : 게임 시작 기능 구현, 기록 타이머, 재난 세팅
            //GameManager.Instance.GameStart();
        }

        // ── 텐트 입장 RPC ──────────────────────────────────────────────────
        // 누가 트리거해도 양쪽 클라이언트에서 동시에 입장 시퀀스를 실행합니다.
        // 각 클라이언트는 자신의 역할(리더/서포터)에 따라 배치 위치를 스스로 결정합니다.

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_TentEnter(Vector3 leaderInteriorPos, Vector3 navigatorInteriorPos, Vector3 exitPos)
        {
            if (TentInteriorController.Instance != null)
                TentInteriorController.Instance.ExecuteTentEnterLocal(leaderInteriorPos, navigatorInteriorPos, exitPos);
        }

        // ── 텐트 퇴장 RPC ──────────────────────────────────────────────────
        // 리더의 ExitTent() 호출에서만 발송됩니다.
        // SurvivalManager 상태 변경은 StateAuthority 한 곳에서만 처리합니다.

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_TentExit(Vector3 exitPos)
        {
            // 퇴장 시각화 + 텔레포트 (전 클라이언트)
            if (TentInteriorController.Instance != null)
                TentInteriorController.Instance.ExecuteTentExitLocal(exitPos);

            // SurvivalManager 상태 복원 종료는 StateAuthority 한 곳에서만 RPC 발송
            if (HasStateAuthority)
            {
                if (SurvivalManager.Instance != null)
                    SurvivalManager.Instance.RPC_SetRestoringState(false);
            }
        }
    }
}