using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class IceAxe : MonoBehaviour
{
    [Header("Axe Settings")]
    [Tooltip("아이스 바일 헤드(끝부분)의 판정 반경 (구형 판정)")]
    public float anchorRadius = 0.15f;
    [Tooltip("아이스 바일 헤드 위치로 쓸 로컬 오프셋 (Z축 앞으로 살짝 둠)")]
    public Vector3 hitOffset = new Vector3(0, 0, 0.2f);
    [Tooltip("아이스 바일이 박힐 수 있는 지형의 레이어 (예: IceWall)")]
    public LayerMask iceLayer;

    [Header("Debug (에디터 환경 테스트용)")]
    public bool useDebugKey = true;
    public Key debugTriggerKey = Key.Space;

    public bool IsAnchored { get; private set; }
    public Vector3 AnchoredPosition { get; private set; }

    public event Action<IceAxe> OnAxeHitIce;
    public event Action<IceAxe> OnAxeReleased;

    private void Update()
    {
        if (useDebugKey && Keyboard.current != null)
        {
            if (Keyboard.current[debugTriggerKey].wasPressedThisFrame) OnTriggerPressed();
            if (Keyboard.current[debugTriggerKey].wasReleasedThisFrame) OnTriggerReleased();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsAnchored ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), anchorRadius);
    }

    [ContextMenu("Debug: Force Trigger Press (디버그 강제 실행)")]
    public void OnTriggerPressed()
    {
        if (IsAnchored) return;

        Vector3 detectionCenter = transform.TransformPoint(hitOffset);

        // 1단계: 레이어 구별 없이 구(Sphere) 안의 "모든" 콜라이더를 싹 다 쓸어담습니다.
        Collider[] allHits = Physics.OverlapSphere(detectionCenter, anchorRadius);

        if (allHits.Length > 0)
        {
            string hitNames = "";

            foreach (var hit in allHits)
            {
                hitNames += $"[{hit.gameObject.name} (레이어: {LayerMask.LayerToName(hit.gameObject.layer)})] ";

                // 2단계: 담긴 콜라이더 둥, 인스펙터의 'Ice Layer' 변수에 체크된 레이어와 겹치는 게 있는지 확인
                if ((iceLayer.value & (1 << hit.gameObject.layer)) > 0)
                {
                    Vector3 closestPoint = hit.ClosestPoint(detectionCenter);
                    AnchorToIce(closestPoint);
                    return; // 성공했으니 즉시 종료!
                }
            }

            // 3단계: 어떤 물체와 충돌은 했지만, 타겟 레이어가 아니라서 튕겨진 경우 명확히 로그 출력
            Debug.Log($"[IceAxe] 물체와 겹치긴 했습니다! 겹친 녀석들: {hitNames}\n" +
                      $"하지만 이 중에서 인스펙터상 IceAxe 컴포넌트의 'Ice Layer' 드롭다운 변수에 체크된 레이어가 단 하나도 없습니다! (현재 세팅값: {iceLayer.value})");
        }
        else
        {
            // 4단계: 구(Sphere) 반경 내에 진짜 문자 그대로 아무 3D 오브젝트도 없는 경우
            Debug.Log($"[IceAxe] 구체(반경 {anchorRadius}) 안에 감지된 '3D 콜라이더'가 말 그대로 아무것도 없습니다.\n" +
                      $"큐브에 BoxCollider 컴포넌트가 떨어져나갔거나 비활성화 되어있는지 확인해 주세요!");
        }
    }

    [ContextMenu("Debug: Force Trigger Release (디버그 강제 해제)")]
    public void OnTriggerReleased()
    {
        if (!IsAnchored) return;

        ReleaseAxe();
    }

    private void AnchorToIce(Vector3 point)
    {
        IsAnchored = true;
        AnchoredPosition = point;
        Debug.Log($"[IceAxe] 얼음에 고정됨! {gameObject.name}");
        OnAxeHitIce?.Invoke(this);
    }

    private void ReleaseAxe()
    {
        IsAnchored = false;
        Debug.Log($"[IceAxe] 얼음에서 뽑음! {gameObject.name}");
        OnAxeReleased?.Invoke(this);
    }
}
