using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ObjectTracker : NetworkBehaviour
{
    private Transform _target;
    [SerializeField] private bool isTrackingPos;
    [SerializeField] private bool isTrackingRot;
    
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
        if (isTrackingRot) transform.rotation = Quaternion.Lerp(transform.rotation, _target.rotation, 0.9f);
    }
}
