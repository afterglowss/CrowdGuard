using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerModel : NetworkBehaviour
{
    public static PlayerModel LocalPlayer;

    private GameObject _controller;
    /// <summary>
    /// Sync Controller
    /// </summary>
    /// <param name="controller"> TODO : Controller Component</param>
    public void Init(GameObject controller)
    {
        LocalPlayer = this;
        _controller = controller;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        transform.position = Vector3.Lerp(transform.position, _controller.transform.position, 0.5f);
    }
}
