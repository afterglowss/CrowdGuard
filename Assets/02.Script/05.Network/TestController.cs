using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestController : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            transform.position += Vector3.up;
        }
        else if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            transform.position += Vector3.left;
        }
        
    }
}
