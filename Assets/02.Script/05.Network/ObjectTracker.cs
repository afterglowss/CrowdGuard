using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ObjectTracker : NetworkBehaviour
{
    private Transform _target;
    [SerializeField] private bool isTrackingPos;
    
    [Header("Rotation Tracking")]
    [SerializeField] private bool isTrackingRotX;
    [SerializeField] private bool isTrackingRotY;
    [SerializeField] private bool isTrackingRotZ;
    
    
    /// <summary>
    /// Sync Transform
    /// </summary>
    /// <param name="target"> tracking target</param>
    public void Init(Transform target)
    {
        _target = target;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!_target) return;
        if (isTrackingPos) transform.position = Vector3.Lerp(transform.position, _target.position, 0.9f);
        
        // 1. 현재 오브젝트와 타겟의 오일러 각도(0~360도)를 가져옴
        Vector3 currentEuler = transform.eulerAngles;
        Vector3 targetEuler = _target.eulerAngles;

        // 2. 체크된 축만 타겟의 각도로 변경, 나머지는 현재 유지
        float nextX = isTrackingRotX ? targetEuler.x : currentEuler.x;
        float nextY = isTrackingRotY ? targetEuler.y : currentEuler.y;
        float nextZ = isTrackingRotZ ? targetEuler.z : currentEuler.z;

        // 3. 목표 회전값 생성
        Quaternion targetRotation = Quaternion.Euler(nextX, nextY, nextZ);

        // 4. Lerp를 이용해 부드럽게 회전 적용
        // Time.deltaTime을 곱해 프레임 독립적인 부드러운 이동 구현
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.9f);
    }
}
