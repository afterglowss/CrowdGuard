using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class DataManager : NetworkBehaviour
{

    public static DataManager Instance = null;
    
    public Timer timer;
    [Networked] public int FallCount { get; private set; }
    [Networked] public int AnchorCount { get; private set; }


    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            // 방장만 초기화 권한을 가짐.
            timer.Init();
            
        }
        
        base.Spawned();
    }

    public void AddFallCount()
    {
        Debug.Log($"AddFallCount {FallCount}");
        if (!Object.HasStateAuthority) return;
        FallCount++;
    }

    [Rpc(RpcSources.All,RpcTargets.StateAuthority)]
    public void RPC_AddAnchorCount()
    {
        Debug.Log($"AddAnchorCount {AnchorCount}");
        AnchorCount++;
    }
    
}
