using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TentInteriorController : MonoBehaviour
{
    public static TentInteriorController Instance { get; private set; }

    [Header("플레이어 배치 위치")]
    public Transform player1InteriorPos; // 플레이어 1용 위치
    public Transform player2InteriorPos; // 플레이어 2용 위치

    [Header("랜턴 세팅 (이중 제어)")]
    public Light lanternLight;             // 실제 빛 컴포넌트
    public GameObject lanternEmissionObj;  // Emission 머테리얼이 적용된 메시 오브젝트

    private TentSavePoint currentEnteredTent;
    private GameObject[] currentPlayers;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 입장 로직: 두 명을 서로 마주 보는 위치로 보냅니다.
    public void EnterFromTent(TentSavePoint tent, GameObject[] players)
    {
        currentEnteredTent = tent;
        currentPlayers = players;

        // 입장 시 랜턴 상태 초기화 (둘 다 끔)
        if (lanternLight != null) lanternLight.enabled = false;
        if (lanternEmissionObj != null) lanternEmissionObj.SetActive(false);

        StartCoroutine(TransitionToInterior());
    }

    private IEnumerator TransitionToInterior()
    {
        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, false));

        // 플레이어들을 각각의 위치로 분산 배치
        if (currentPlayers.Length >= 1) currentPlayers[0].transform.position = player1InteriorPos.position;
        if (currentPlayers.Length >= 2) currentPlayers[1].transform.position = player2InteriorPos.position;

        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, true));
    }

    // 랜턴 켜기: 빛과 Emission 오브젝트를 동시에 켭니다.
    public void TurnOnLantern()
    {
        Debug.Log("[TentInteriorController] 랜턴 가동: 빛과 발광 메시를 모두 활성화합니다.");

        if (lanternLight != null) lanternLight.enabled = true;
        if (lanternEmissionObj != null) lanternEmissionObj.SetActive(true);

        if (SurvivalManager.Instance != null)
            SurvivalManager.Instance.SetRestoringState(true);

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.SupplyAnchorsAtTent();
    }

    // 퇴장 로직: 모든 플레이어를 다시 밖으로 보냅니다.
    public void ExitTent()
    {
        if (currentEnteredTent == null || currentPlayers == null) return;

        if (lanternLight != null) lanternLight.enabled = false;
        if (lanternEmissionObj != null) lanternEmissionObj.SetActive(false);
        if (SurvivalManager.Instance != null)
            SurvivalManager.Instance.SetRestoringState(false);

        // 퇴장 시 위치 세이브 (중앙 위치 저장)
        if (SavePointManager.Instance != null)
            SavePointManager.Instance.ForceSetSavePoint(currentEnteredTent.exteriorPos.position);

        StartCoroutine(TransitionToExterior());
    }

    private IEnumerator TransitionToExterior()
    {
        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, false));

        // 밖으로 나갈 때는 겹치지 않게 약간의 오프셋을 줍니다.
        Vector3 exitPos = currentEnteredTent.exteriorPos.position;
        if (currentPlayers.Length >= 1) currentPlayers[0].transform.position = exitPos + new Vector3(0.5f, 0, 0);
        if (currentPlayers.Length >= 2) currentPlayers[1].transform.position = exitPos + new Vector3(-0.5f, 0, 0);

        if (ScreenEffectManager.Instance != null)
            yield return StartCoroutine(ScreenEffectManager.Instance.FadeScreenRoutine(0.5f, true));
    }
}