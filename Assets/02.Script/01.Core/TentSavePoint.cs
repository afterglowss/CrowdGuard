using UnityEngine;
using Capstone.Photon.Game;

public class TentSavePoint : MonoBehaviour
{
    [Tooltip("이 텐트 밖으로 나갈 때 플레이어들이 서 있게 될 중앙 위치")]
    public Transform exteriorPos;

    private static bool IsNetworkActive() =>
        PlayerManager.Instance != null &&
        PlayerManager.Instance.Runner != null &&
        PlayerManager.Instance.Runner.IsRunning;

    public void EnterTent()
    {
        Debug.Log($"[TentSavePoint] {gameObject.name}에서 2인 진입 시퀀스를 시작합니다.");

        TentInteriorController ctrl = TentInteriorController.Instance;
        if (ctrl == null) return;

        // 내부 배치 위치와 퇴장 위치를 RPC 파라미터로 전달합니다.
        // 각 클라이언트는 수신 후 자신의 역할에 맞는 위치로 이동합니다.
        Vector3 leaderPos    = ctrl.player1InteriorPos != null ? ctrl.player1InteriorPos.position : transform.position;
        Vector3 navigatorPos = ctrl.player2InteriorPos != null ? ctrl.player2InteriorPos.position : transform.position;
        Vector3 exitPos      = exteriorPos != null ? exteriorPos.position : transform.position;

        if (IsNetworkActive())
        {
            PlayerManager.Instance.RPC_TentEnter(leaderPos, navigatorPos, exitPos);
        }
        else
        {
            ctrl.ExecuteTentEnterLocal(leaderPos, navigatorPos, exitPos);
        }
    }
}