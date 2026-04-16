using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Obsolete("MVC 구조의 IceAnchorController/Model로 교체되었습니다. 이 컴포넌트는 더 이상 사용되지 않습니다.")]
public class IceAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    [Tooltip("체결에 필요한 동그라미(Crank) 바퀴 수")]
    public float requiredRotations = 3f;
    [Tooltip("아이스 바일이 박힐 수 있는 지형의 레이어 (예: IceWall)")]
    public LayerMask iceLayer;
    public float anchorRadius = 0.15f;

    [Header("Debug")]
    public bool useDebugKey = true;
    public Key debugPlaceKey = Key.Digit1;
    public Key debugCrankKey = Key.Digit2;

    public bool IsPlaced { get; private set; }
    public bool IsSecured { get; private set; }

    public Transform activeHand; // 원을 그리는 컨트롤러

    // 전역 세이브 매니저에게 알리는 정적(Static) 옵저버 이벤트
    public static event Action<Vector3> OnAnchorSecured;

    private float accumulatedAngle = 0f;
    private float previousAngle = 0f;

    private void Update()
    {
        if (useDebugKey && Keyboard.current != null)
        {
            if (Keyboard.current[debugPlaceKey].wasPressedThisFrame)
                TryPlaceAnchor(transform); // 테스트용: 자기 자신이 손이라고 치고 가배치
                
            if (Keyboard.current[debugCrankKey].isPressed)
            {
                // 테스트용: 숫자 2를 꾹 누르면 빙글빙글 각도가 자동으로 참
                accumulatedAngle += 360f * Time.deltaTime * 2f; 
                CheckSecured();
            }
        }

        // 실제 유저가 손으로 빙빙 원을 그릴 때의 로직
        if (IsPlaced && !IsSecured && activeHand != null)
        {
            ProcessCranking();
        }
    }

    public void TryPlaceAnchor(Transform hand)
    {
        if (IsPlaced) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, anchorRadius, iceLayer);
        if (hits.Length > 0)
        {
            IsPlaced = true;
            activeHand = hand;
            
            // 앵커 좌표계 기준 손의 상대 위치 저장 (시뮬레이터 궤도 계산용)
            Vector3 localHandPos = transform.InverseTransformPoint(activeHand.position);
            previousAngle = Mathf.Atan2(localHandPos.y, localHandPos.x) * Mathf.Rad2Deg;

            Debug.Log("[IceAnchor] 벽에 가배치 됨! 컨트롤러를 잡고 원을 그리듯 빙빙 돌려주세요.");
        }
        else
        {
            Debug.Log("[IceAnchor] 설치 실패! 반경 내에 얼음 벽이 없습니다.");
        }
    }

    private void ProcessCranking()
    {
        Vector3 localHandPos = transform.InverseTransformPoint(activeHand.position);
        float currentAngle = Mathf.Atan2(localHandPos.y, localHandPos.x) * Mathf.Rad2Deg;

        // 원형 궤도를 그리는 각도의 변화량 추출 (-180 ~ 180 사이 자동 보정)
        float deltaAngle = Mathf.DeltaAngle(previousAngle, currentAngle);

        // 유저가 열심히 휘휘 돌린 절대값을 누적 (양방향 모두 허용)
        accumulatedAngle += Mathf.Abs(deltaAngle);
        previousAngle = currentAngle;

        CheckSecured();
    }

    private void CheckSecured()
    {
        if (IsSecured) return;

        if (accumulatedAngle >= requiredRotations * 360f)
        {
            IsSecured = true;
            Debug.Log($"[IceAnchor] 철컥! 3바퀴 빙빙 돌리기 성공. 앵커 체결! (위치: {transform.position})");
            
            // 전역 이벤트 방송
            OnAnchorSecured?.Invoke(transform.position);
            
            // 시각적 피드백 (초록색 변화)
            var rend = GetComponent<Renderer>();
            if (rend != null) rend.material.color = Color.green;
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = IsSecured ? Color.green : (IsPlaced ? Color.yellow : Color.gray);
        Gizmos.DrawWireSphere(transform.position, anchorRadius);
    }
}
