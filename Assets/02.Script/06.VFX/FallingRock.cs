using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Capstone.Photon.Game;

public class FallingRock : MonoBehaviour
{
    [SerializeField] private ParticleSystem rockDebris;
    [SerializeField] private LayerMask iceWallLayer;
    [SerializeField] private float surfaceOffset = 0.08f;
    [SerializeField] private float forceAmount = 10f;

    private Rigidbody rb;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        rb.AddForce(Vector3.down * forceAmount, ForceMode.Force);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ── 플레이어 충돌 ──────────────────────────────────────────────
        // 양쪽 머신에 두 플레이어 프리팹이 모두 존재하므로,
        // LocalPlayerModel과 일치하는 쪽만 추락 처리합니다.
        if (collision.gameObject.CompareTag("Player"))
        {
            var model = collision.gameObject.GetComponentInParent<GamePlayerModel>();
            if (model != null && model == GamePlayerModel.LocalPlayerModel)
            {
                Debug.Log("[FallingRock] 로컬 플레이어 충돌 → 추락 전환");
                PlayerController.LocalInstance?.ChangeState(PlayerController.LocalInstance.FallingState);
            }
            return; // 플레이어 충돌은 벽 파편 처리 없이 종료
        }

        // ── 얼음벽 충돌 → 파편 파티클 ────────────────────────────────
        if (!IsInLayerMask(collision.gameObject, iceWallLayer))
            return;

        Debug.Log("IceWall layer hit");

        ContactPoint contact = collision.GetContact(0);

        Vector3 hitPoint = contact.point;
        Vector3 wallNormal = contact.normal;

        Debug.DrawRay(hitPoint, wallNormal * 2f, Color.red, 3f);

        Vector3 spawnPoint = hitPoint + wallNormal * surfaceOffset;
        Quaternion rot = Quaternion.LookRotation(wallNormal);

        ParticleSystem fx = Instantiate(rockDebris, spawnPoint, rot);
        fx.Play(true);

        Destroy(fx.gameObject, 3f);
    }

    private bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
}