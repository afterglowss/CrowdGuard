using Fusion;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Photon.Game
{
    public class PlayerManager : NetworkBehaviour
    {
        public static PlayerManager Instance;
        public Dictionary<Role.Role, NetworkObject> players;

        public RopeSystem ropeSystem;

        private void Awake()
        {
            if(!Instance) Instance = this;
            else if(Instance != this) Destroy(gameObject);
            players = new Dictionary<Role.Role, NetworkObject>();
        }
        
        /// <summary>
        /// 플레이어 입장 시 dictionary에 값 추가
        /// </summary>
        /// <param name="player"></param>
        /// <param name="obj"></param>
        public void SetPlayer(Role.Role role, NetworkObject obj)
        {
            players[role] = obj;
            
            foreach (var player in players)
            {
                Debug.Log($"{players.Count} ---- {player.Key} : {player.Value}");
            }

            if (players.Count >= 2)
            {
                SetGameSystem(players[Role.Role.Leader],players[Role.Role.Supporter]);
                
                
            }
        }

        public void SetGameSystem(NetworkObject leader, NetworkObject supporter)
        {
            if (ropeSystem != null)
            {
                // 로컬 플레이어가 Leader인지 Supporter인지 판별
                bool isLeader = leader.HasStateAuthority;

                NetworkObject myObj = isLeader ? leader : supporter;
                NetworkObject partnerObj = isLeader ? supporter : leader;

                if (myObj.TryGetComponent(out GamePlayerModel myModel) &&
                    partnerObj.TryGetComponent(out GamePlayerModel partnerModel))
                {
                    ropeSystem.SetPartners(myModel.body.transform, partnerModel.body.transform);
                }
            }

            // TODO : 게임 시작 기능 구현, 기록 타이머, 재난 세팅
            GameManager.Instance.GameStart();
        }
        

    }
}