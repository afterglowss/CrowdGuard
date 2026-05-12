using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class Timer : NetworkBehaviour
{
    public void Init()
    {
        ElapsedTime = 0;
        _isPaused = false;
    }

    // 나중에 다른 플레이어로도 정지가 필요하면 rpc로 전환
    public void Pause()
    {
        if(!Object.HasStateAuthority) return;
        _isPaused = true;
    }

    /// <summary>
    /// 걸린 시간 반환
    /// </summary>
    /// <returns></returns>
    public int GetTime()
    {
        return _lastDisplayedSecond;
    }

    public static string ConvertTimeToString(int time)
    {
        var min =  time / 60;
        var sec =  time % 60;
        return $"{min}:{sec}";
    }

    /// <summary> 현재 걸린 시간 </summary>
    [Networked] private float ElapsedTime { get; set; }

    [Networked] private NetworkBool _isPaused { get; set; }

    private int _lastDisplayedSecond;
    
    public Action<int> onTimerUpdate;


    public override void FixedUpdateNetwork()
    {
        if (_isPaused) return;
        
        ElapsedTime += Runner.DeltaTime;
        
        int currentSecond = Mathf.FloorToInt(ElapsedTime);
        if (currentSecond != _lastDisplayedSecond)
        {
            // 1초가 지나면 
            RPC_TimerUpdate(currentSecond);
            _lastDisplayedSecond = currentSecond;
        }
    }

    [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
    void RPC_TimerUpdate(int time)
    {
        onTimerUpdate?.Invoke(time);
        //Debug.Log(time);
    }
}
