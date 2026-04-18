using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Capstone.Photon.Game
{
    public class PlayerManager : NetworkBehaviour
    {
        public static PlayerManager Instance;
        public Dictionary<Role.Role, NetworkObject> players;
        
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
            //TODO : 로프 연결 및 두 플레이어 간 필요한 세팅 구현
            
            // TODO : 게임 시작 기능 구현, 기록 타이머, 재난 세팅
            GameManager.Instance.GameStart();
        }
        

    }
}