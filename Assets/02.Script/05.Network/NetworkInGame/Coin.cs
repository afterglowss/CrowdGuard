using System;
using Fusion;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    private void Update()
    {
        transform.rotation = Quaternion.Euler(
            transform.rotation.eulerAngles.x,
            transform.rotation.eulerAngles.y + Time.deltaTime * 100,
            transform.rotation.eulerAngles.z);
    }

    public void OnInteract()
    {
        DataManager.Instance?.RPC_AddCoinCount();
        Debug.Log("GetCoin");
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
