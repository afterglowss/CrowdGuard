using System.Collections;
using UnityEngine;
using Capstone.Photon.Game;
using CrowdGuard.Climbing.Tools.Common;

/// <summary>
/// 플레이어 프리팹의 Trigger 콜라이더로 재난 피격을 감지합니다.
/// Trigger 콜라이더가 붙어 있는 GamePlayerModel 프리팹 오브젝트에 추가하세요.
///
/// [처리 재난]
/// - 눈사태 : AvalanchePathSystem의 CapsuleCollider(Trigger)와 겹칠 때
/// - 낙석   : FallingRock의 Rigidbody 콜라이더와 겹칠 때
///
/// [추락 흐름]
/// 1. 로컬 플레이어 즉시 FallingState 진입
/// 2. RPC_TriggerPartnerFall → 상대방도 FallingState 진입
/// </summary>
public class PlayerHazardReceiver : MonoBehaviour
{
    [Header("피격 비네트")]
    [Tooltip("피격 시 붉은 테두리 강도 (0~1)")]
    [Range(0f, 1f)]
    public float hitVignetteIntensity = 1.0f;

    [Tooltip("비네트가 유지되는 시간(초). 이후 서서히 사라짐.")]
    public float hitVignetteDuration = 2.0f;

    private const string HazardSourceId = "hazard_hit";
    private Coroutine _vignetteCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        // 로컬 플레이어 오브젝트에 붙은 인스턴스만 처리
        var model = GetComponentInParent<GamePlayerModel>();
        if (model == null || model != GamePlayerModel.LocalPlayerModel) return;

        bool isAvalanche = other.GetComponentInParent<AvalanchePathSystem>() != null;
        bool isRock      = other.GetComponentInParent<FallingRock>() != null;

        if (!isAvalanche && !isRock) return;

        Debug.Log($"[PlayerHazardReceiver] {(isAvalanche ? "눈사태" : "낙석")} 피격 — 추락 시작");
        ShowHitVignette();
        TriggerFall(model);
    }

    private void ShowHitVignette()
    {
        var mgr = ScreenEffectManager.Instance;
        if (mgr == null) return;

        if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
        _vignetteCoroutine = StartCoroutine(HitVignetteRoutine(mgr));
    }

    private IEnumerator HitVignetteRoutine(ScreenEffectManager mgr)
    {
        mgr.SetDangerVignette(HazardSourceId, hitVignetteIntensity);
        yield return new WaitForSeconds(hitVignetteDuration);
        mgr.ClearDangerVignette(HazardSourceId);
        _vignetteCoroutine = null;
    }

    private void TriggerFall(GamePlayerModel model)
    {
        var controller = PlayerController.LocalInstance;
        if (controller == null) return;
        if (controller.CurrentState == controller.FallingState) return;

        // 로컬 플레이어 추락
        controller.ChangeState(controller.FallingState);

        // 파트너 추락 RPC (이미 RPC 내부 처리 중인 경우 재발송 방지)
        PlayerRole myRole = model.IsLeader ? PlayerRole.Leader : PlayerRole.Navigator;
        PlayerFallingState.NetworkTriggered = true;
        PlayerManager.Instance?.RPC_TriggerPartnerFall(myRole);
        PlayerFallingState.NetworkTriggered = false;
    }
}
