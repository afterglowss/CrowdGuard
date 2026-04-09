using UnityEngine;

public class AvalancheDamageZone : MonoBehaviour
{
    public float damagePerSecond = 50f;

    void OnTriggerStay(Collider other)
    {
        Debug.Log("ouch");
    }
}