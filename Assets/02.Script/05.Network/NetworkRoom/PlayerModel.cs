using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace Capstone.Photon
{
    public class PlayerModel : NetworkBehaviour
    {
        private Transform _target;

        [Header("Renderer")]
        [ContextMenuItem("GetAllRenderer","GetAllRenderer")]
        [SerializeField] protected List<Renderer> modelRenderer;


        public ObjectTracker body;
        public ObjectTracker head;
        public ObjectTracker leftHand;
        public ObjectTracker rightHand;


        public virtual void Init(LocalPlayerController controller)
        {
            //render disable
            modelRenderer.ForEach(r => r.enabled = false );

            // start tracking
            body.Init(controller.body);
            head.Init(controller.head);
            leftHand.Init(controller.leftHand);
            rightHand.Init(controller.rightHand);

        }
        
        
        public void GetAllRenderer()
        {
            modelRenderer.AddRange( GetComponentsInChildren<Renderer>().ToList());
        }
        
    }
}
