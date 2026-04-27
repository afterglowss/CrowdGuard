using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingRock : MonoBehaviour
{
    [SerializeField] private ParticleSystem rockDebris;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("IceWall"))
        {
            rockDebris.Play();
        }
    }

}
