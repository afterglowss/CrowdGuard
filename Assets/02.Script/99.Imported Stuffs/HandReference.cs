using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MikeNspired.XRIStarterKit
{
    /// <summary>
    /// 오브젝트를 잡을 때, 손 종류(좌/우)에 맞는 attach 포인트의 역변환을
    /// InteractionAttachController의 follow 타겟에 적용하여 정확한 그립 위치를 제공한다.
    /// </summary>
    public class HandReference : MonoBehaviour
    {
        [field: SerializeField] public HandAnimator Hand { get; private set; }
        [field: SerializeField] public LeftRight LeftRight { get; private set; }
        [field: SerializeField] public HandReference OtherHand { get; private set; }
        [field: SerializeField] public NearFarInteractor NearFarInteractor { get; private set; }


        private InteractionAttachController attachController;
        private Transform originalFollowTarget;
        private Transform customFollowTarget;
        private HandPoser currentHandPoser;

        private void OnValidate()
        {
            if (!Hand)
                Hand = GetComponentInChildren<HandAnimator>();
            if (!NearFarInteractor)
                NearFarInteractor = GetComponent<NearFarInteractor>();
        }

        private void Awake()
        {
            NearFarInteractor.selectEntered.AddListener(OnGrab);
            NearFarInteractor.selectExited.AddListener(_ => ResetAttachTransform());

            attachController = NearFarInteractor.GetComponent<InteractionAttachController>();

            // 컨트롤러 하위에 커스텀 follow 타겟을 생성한다.
            customFollowTarget = new GameObject($"CustomAttachTarget_{LeftRight}").transform;
            customFollowTarget.SetParent(NearFarInteractor.transform, false);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            // Far interaction은 건너뛴다
            if (NearFarInteractor.interactionAttachController.hasOffset)
                return;

            FindHandPoser(args);
            if (!currentHandPoser)
                return;

            var handAttach = LeftRight == LeftRight.Left
                ? currentHandPoser.leftHandAttach
                : currentHandPoser.rightHandAttach;

            // handAttach의 역변환을 계산한다.
            // handAttach.localPosition/Rotation은 interactable 기준 로컬 값이다.
            Vector3 invPos = handAttach.localPosition * -1;
            Quaternion invRot = Quaternion.Inverse(handAttach.localRotation);
            invPos = invRot * invPos;

            // 커스텀 follow 타겟을 역변환 오프셋에 배치한다.
            customFollowTarget.localPosition = invPos;
            customFollowTarget.localRotation = invRot;

            // InteractionAttachController의 follow 타겟을 교체한다.
            // 컨트롤러 기능(stabilization, momentum, anchor movement)을 유지하면서
            // attach 포인트만 오프셋된 위치로 이동시킨다.
            originalFollowTarget = attachController.transformToFollow;
            attachController.transformToFollow = customFollowTarget;
        }

        private void FindHandPoser(SelectEnterEventArgs args)
        {
            currentHandPoser =
                args.interactableObject.transform.GetComponent<XRHandPoser>() ??
                args.interactableObject.transform.GetComponentInChildren<XRHandPoser>();
        }

        /// <summary>
        /// 놓을 때 원래 follow 타겟으로 복원한다.
        /// </summary>
        public void ResetAttachTransform()
        {
            if (attachController && originalFollowTarget)
            {
                attachController.transformToFollow = originalFollowTarget;
            }
            originalFollowTarget = null;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
        {
            var direction = point - pivot;
            direction = Quaternion.Euler(angles) * direction;
            return direction + pivot;
        }
    }
}
