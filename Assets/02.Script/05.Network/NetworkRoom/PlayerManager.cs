using System;
using Fusion;
using UnityEngine;

namespace Capstone.Photon.Room
{
    public class PlayerManager : NetworkBehaviour
    {
        [Networked,OnChangedRender(nameof(PlayerChanged)), Capacity(8)]
        public NetworkDictionary<int, NetworkObject> Players { get; }

        public event Action<int> OnPlayerChanged;

        public override void Spawned()
        {
            OnPlayerChanged?.Invoke(Players.Count);
        }

        void PlayerChanged()
        {
            OnPlayerChanged?.Invoke(Players.Count);
        }
        
        /// <summary>
        /// 플레이어 입장 시 dictionary에 값 추가
        /// </summary>
        /// <param name="player"></param>
        /// <param name="obj"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AddPlayer(PlayerRef player, NetworkObject obj)
        {
            Players.Set(player.AsIndex, obj);
            Debug.Log($"{player} Added, Player Object's name : {obj.name}, Current Players Count : {Players.Count} ");
        }

        /// <summary>
        /// 플레이어 퇴장 시 dictionary에서 값 제거
        /// </summary>
        /// <param name="player"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RemovePlayer(PlayerRef player)
        {
            Players.Remove(player.AsIndex);
            Debug.Log($"{player} Removed, Current Players Count : {Players.Count}");
        }

    }
}