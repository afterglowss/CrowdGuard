using System.Collections;
using UnityEngine;

public class FallingRock : MonoBehaviour
{
    [SerializeField] private ParticleSystem rockDebris;
    [SerializeField] private LayerMask iceWallLayer;
    [SerializeField] private float surfaceOffset = 0.08f;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField, Tooltip("센서가 가리킬 실제 발생 위치. 비워두면 피봇을 사용합니다.")]
    private Transform _spawnOrigin;

    private Rigidbody _rb;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Coroutine _lifeTimeCoroutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;

        _rb.isKinematic = true;
        gameObject.SetActive(false);
    }

    public Vector3 GetSpawnPosition() =>
        _spawnOrigin != null ? _spawnOrigin.position : transform.position;

    /// <summary>
    /// 낙석을 활성화하고 물리 시뮬레이션을 시작합니다.
    /// HazardManager.RPC_TriggerRockfall()에서 호출합니다.
    /// </summary>
    public void Activate()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _lifeTimeCoroutine = StartCoroutine(LifeTimeRoutine());
    }

    /// <summary>
    /// 돌을 초기 위치/회전으로 되돌리고 비활성화합니다.
    /// HazardManager.RPC_ResetRockfall() 또는 자동 소멸 시 호출됩니다.
    /// </summary>
    public void ResetRock()
    {
        if (_lifeTimeCoroutine != null)
        {
            StopCoroutine(_lifeTimeCoroutine);
            _lifeTimeCoroutine = null;
        }
        _rb.isKinematic = true;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        gameObject.SetActive(false);
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        SpawnDebris(transform.position, Vector3.up);
        ResetRock();
    }

    // 플레이어는 Trigger 콜라이더 → OnTriggerEnter에서 처리
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Vector3 spawnPoint = other.ClosestPoint(transform.position);
        Vector3 normal = (transform.position - spawnPoint).normalized;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;

        SpawnDebris(spawnPoint, normal);
        ResetRock();
    }

    // IceWall은 일반 콜라이더 → OnCollisionEnter에서 처리
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsInLayerMask(collision.gameObject, iceWallLayer)) return;
        ContactPoint contact = collision.GetContact(0);
        SpawnDebris(contact.point, contact.normal);
    }

    private void SpawnDebris(Vector3 point, Vector3 normal)
    {
        if (rockDebris == null) return;
        Vector3 spawnPoint = point + normal * surfaceOffset;
        Quaternion rot = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;
        ParticleSystem fx = Instantiate(rockDebris, spawnPoint, rot);
        fx.Play(true);
        Destroy(fx.gameObject, 3f);
    }

    private bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
}
