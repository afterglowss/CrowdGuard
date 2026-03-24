using System;
using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class PlayerManager : NetworkBehaviour
    {
        [Networked]
        public NetworkDictionary<int, NetworkObject> Players { get; }

        public event Action<int> OnPlayerAdded;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AddPlayer(PlayerRef player, NetworkObject obj)
        {
            Debug.Log($"Player Added {player} : {obj.name}");
            Players.Set(player.AsIndex, obj);
            
            Debug.Log(Players.Count);
            
            // NOTE : 게임 시작 로직(2인 시 시작, 1인 시 중지)
            OnPlayerAdded?.Invoke(Players.Count);
            
            if (Players.Count < 2) return;
            Debug.Log("Let's Start Game");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RemovePlayer(PlayerRef player)
        {
            Debug.Log($"Player Added {player} : {Players.Get(player.AsIndex).name}");
            Players.Remove(player.AsIndex);
        }

    }
}