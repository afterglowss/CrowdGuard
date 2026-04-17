using System;
using Capstone.Photon.Room;
using Fusion;
using UnityEngine;

public class PlayerModel : NetworkBehaviour
{
    public static PlayerModel LocalPlayerModel;
    private Transform _target;

    [SerializeField] private Renderer headObj;
    [SerializeField] private Renderer leftHandObj;
    [SerializeField] private Renderer rightHandObj;
    
    public ObjectTracker body;
    public ObjectTracker head;
    public ObjectTracker leftHand;
    public ObjectTracker rightHand;


    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("IsSpawned");
    }

    public void Init(LocalPlayerController controller)
    {
        //render disable
        headObj.enabled = false;
        leftHandObj.enabled = false;
        rightHandObj.enabled = false;
        
        // start tracking
        body.Init(controller.head);
        head.Init(controller.head);
        leftHand.Init(controller.leftHand);
        rightHand.Init(controller.rightHand);
        
    }
    
}
