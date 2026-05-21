using System;
using Fusion;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    public Transform coinTransform;
    public float rotationSpeed = 100;
    public float sinSpeed = 10;
    public float sinScale = 1; 
    
    private void Update()
    {
        coinTransform.rotation = Quaternion.Euler(
            coinTransform.rotation.eulerAngles.x,
            coinTransform.rotation.eulerAngles.y + Time.deltaTime * rotationSpeed,
            coinTransform.rotation.eulerAngles.z);
        coinTransform.position = new Vector3(
            coinTransform.position.x,
            transform.position.y + Mathf.Cos(Time.time*sinSpeed)*sinScale,
            coinTransform.position.z);
    }

    public void OnInteract()
    {
        Debug.Log("GetCoin");
        DataManager.Instance?.RPC_AddCoinCount();
        
        if (Object&& Object.IsValid)
        {
            RPC_SetDisable();
        }
        
    }

    [Rpc(RpcSources.All,RpcTargets.All)]
    private void RPC_SetDisable()
    {
        gameObject.SetActive(false);
    }
}
