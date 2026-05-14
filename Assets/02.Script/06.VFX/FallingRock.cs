using UnityEngine;

public class FallingRock : MonoBehaviour
{
    [SerializeField] private ParticleSystem rockDebris;
    [SerializeField] private LayerMask iceWallLayer;
    [SerializeField] private float surfaceOffset = 0.08f;

    private Rigidbody _rb;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;

        _rb.isKinematic = true;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 낙석을 활성화하고 물리 시뮬레이션을 시작합니다.
    /// HazardManager.RPC_TriggerRockfall()에서 호출합니다.
    /// </summary>
    public void Activate()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        _rb.isKinematic = false;
    }

    /// <summary>
    /// 돌을 초기 위치/회전으로 되돌리고 비활성화합니다.
    /// HazardManager.RPC_ResetRockfall()에서 호출합니다.
    /// </summary>
    public void ResetRock()
    {
        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        gameObject.SetActive(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsInLayerMask(collision.gameObject, iceWallLayer)) return;
        if (rockDebris == null) return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 spawnPoint = contact.point + contact.normal * surfaceOffset;
        Quaternion rot = Quaternion.LookRotation(contact.normal);

        ParticleSystem fx = Instantiate(rockDebris, spawnPoint, rot);
        fx.Play(true);
        Destroy(fx.gameObject, 3f);
    }

    private bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
}
