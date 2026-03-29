using System;
using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class PlayerManager : NetworkBehaviour
    {
        [Networked,OnChangedRender(nameof(OnPlayerChanged))]
        public NetworkDictionary<int, NetworkObject> Players { get; }

        public event Action<int> OnPlayerChanged;

        public override void Spawned()
        {
            OnPlayerChanged?.Invoke(Players.Count);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AddPlayer(PlayerRef player, NetworkObject obj)
        {
            Debug.Log($"Player Added {player} : {obj.name}");
            Players.Set(player.AsIndex, obj);
            
            Debug.Log(Players.Count);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RemovePlayer(PlayerRef player)
        {
            Debug.Log($"Player Added {player} : {Players.Get(player.AsIndex).name}");
            Players.Remove(player.AsIndex);
        }

    }
}