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
    [Header("Settings")]
    public EquipmentType currentEquipment = EquipmentType.IceAxe;

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
}
