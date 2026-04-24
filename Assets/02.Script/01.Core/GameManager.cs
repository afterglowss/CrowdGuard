using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    private void Awake()
    {
        if(!Instance)Instance = this;
        else if(Instance != this) Destroy(gameObject);
    }

    public void GameStart()
    {
        // TODO : 게임 매니저 NetworkBehaviour로 변경 및 함수에 RPC 추가,
        Debug.Log($"GameStart");
    }
}
