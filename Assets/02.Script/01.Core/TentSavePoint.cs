using UnityEngine;
using System.Collections.Generic;

public class TentSavePoint : MonoBehaviour
{
    [Tooltip("이 텐트 밖으로 나갈 때 플레이어들이 서 있게 될 중앙 위치")]
    public Transform exteriorPos;

    public void EnterTent()
    {
        Debug.Log($"[TentSavePoint] {gameObject.name}에서 2인 진입 시퀀스를 시작합니다.");

        // 1. "Player" 태그를 가진 모든 오브젝트를 찾습니다.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        if (TentInteriorController.Instance != null && players.Length > 0)
        {
            // 2. 내부 컨트롤러에 들어온 텐트 정보와 플레이어 목록을 넘깁니다.
            TentInteriorController.Instance.EnterFromTent(this, players);
        }
    }
}