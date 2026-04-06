using System;
using UnityEngine;

/// <summary>
/// 장착 가능한 장비 종류
/// </summary>
public enum EquipmentType
{
    IceAxe,      // 기본 아이스 바일
    IceAnchor,   // 설치형 세이브 포인트 앵커
    SensorMap    // 네비게이터용 지도/센서
}

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [Header("Settings")]
    public EquipmentType currentEquipment = EquipmentType.IceAxe;

    [Header("Inventory Settings")]
    [Tooltip("현재 보유 중인 아이스 앵커 갯수")]
    public int currentAnchorCount = 5;
    public const int MAX_ANCHORS = 10;
    public const int MIN_SAFEHOUSE_ANCHORS = 5;

    /// <summary>
    /// 무기가 변경되었을 때 호출되는 이벤트. 타 시스템(모델링 교체, 네트워크 동기화)에서 사용
    /// </summary>
    public event Action<EquipmentType> OnEquipmentChanged;

    /// <summary>
    /// 허리춤 인벤토리 박스(Trigger)에 손이 닿고, 특정 조작을 했을 때 교체를 실행하는 메서드.
    /// 손에서 아이템을 놓치는 로직 대신 벨트에서 꺼내는 방식으로 재설계됨.
    /// </summary>
    public void SwitchEquipment(EquipmentType newEquipment)
    {
        if (currentEquipment == newEquipment) return;

        currentEquipment = newEquipment;
        Debug.Log($"[EquipmentManager] 무기 교체 완료: {currentEquipment}");

        // 옵저버에게 알림 -> 시각적 모델링 활성화/비활성화 등 처리
        OnEquipmentChanged?.Invoke(currentEquipment);
    }

    /// <summary>
    /// XR 기믹 상 앵커를 1개 꺼내어 벽에 고정시킬 때 호출되는 메서드
    /// </summary>
    public void UseAnchor()
    {
        if (currentAnchorCount > 0)
        {
            currentAnchorCount--;
            Debug.Log($"[EquipmentManager] 앵커 사용! 남은 앵커 수: {currentAnchorCount}");
        }
        else
        {
            Debug.LogWarning("[EquipmentManager] 더 이상 사용할 앵커가 없습니다!");
        }
    }

    /// <summary>
    /// 텐트 진입 시 호출되어 5개 미만인 경우 최소수량만큼 앵커를 무상 보충
    /// </summary>
    public void SupplyAnchorsAtTent()
    {
        if (currentAnchorCount < MIN_SAFEHOUSE_ANCHORS)
        {
            currentAnchorCount = MIN_SAFEHOUSE_ANCHORS;
            Debug.Log($"[EquipmentManager] 텐트 보급 성공! 앵커 보유량이 {MIN_SAFEHOUSE_ANCHORS}개로 조정되었습니다.");
        }
        else
        {
            Debug.Log($"[EquipmentManager] 보유량이 충분하여 보급 생략 (현재 {currentAnchorCount}개)");
        }
    }
}
