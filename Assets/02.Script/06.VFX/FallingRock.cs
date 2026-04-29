using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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