using UnityEngine;
using Capstone.Photon.Game;

/// <summary>
/// AvalanchePathSystem.BuildColliderObjects()에서
/// _AvalancheColliderRoot에 런타임으로 추가되는 컴포넌트.
///
/// [동작 원리]
/// _AvalancheColliderRoot는 kinematic Rigidbody를 가지며,
/// 그 자식에 세그먼트별 CapsuleCollider(isTrigger)가 붙어있다.
/// Unity는 trigger 이벤트를 Rigidbody가 있는 오브젝트의 스크립트에 전달하므로,
/// 이 컴포넌트가 모든 세그먼트 충돌을 한 곳에서 수신한다.
///
/// [멀티플레이어]
/// 양쪽 머신에 두 플레이어 프리팹이 모두 존재하기 때문에,
/// GamePlayerModel.LocalPlayerModel과 일치하는 경우에만 추락을 처리한다.
/// PlayerFallingState.Enter()가 RPC_TriggerPartnerFall을 자동 발송하므로
/// 파트너 동기화는 별도 처리 없이 해결된다.
/// </summary>
public class AvalancheHazardZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 로컬 플레이어 프리팹인지 확인 (상대 플레이어 프리팹은 무시)
        var model = other.GetComponentInParent<GamePlayerModel>();
        if (model == null || model != GamePlayerModel.LocalPlayerModel) return;

        Debug.Log("[AvalancheHazardZone] 로컬 플레이어 눈사태 충돌 → 추락 전환");
        PlayerController.LocalInstance?.ChangeState(PlayerController.LocalInstance.FallingState);
    }
}
