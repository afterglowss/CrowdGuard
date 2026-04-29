using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class PlayerModel : NetworkBehaviour
    {
        private Transform _target;

        [SerializeField] protected Renderer headObj;
        [SerializeField] protected Renderer leftHandObj;
        [SerializeField] protected Renderer rightHandObj;


        public ObjectTracker body;
        public ObjectTracker head;
        public ObjectTracker leftHand;
        public ObjectTracker rightHand;


        public virtual void Init(LocalPlayerController controller)
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
}
