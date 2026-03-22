using Fusion;
using UnityEngine;

public class PlayerModel : NetworkBehaviour
{
    public static PlayerModel LocalPlayer;
    private Transform _target;

    public ObjectTracker body;
    public ObjectTracker head;
    public ObjectTracker leftHand;
    public ObjectTracker rightHand;

    public void Init(LocalPlayerController controller)
    {
        body.Init(controller.head);
        head.Init(controller.head);
        leftHand.Init(controller.leftHand);
        rightHand.Init(controller.rightHand);
    }
    
}
