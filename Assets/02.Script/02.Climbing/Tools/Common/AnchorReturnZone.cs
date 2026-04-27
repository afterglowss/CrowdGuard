using UnityEngine;
using CrowdGuard.Climbing.Tools.IceAnchor;

namespace CrowdGuard.Climbing.Tools.Common
{
    /// <summary>
    /// AnchorBag의 자식 오브젝트에 부착.
    /// 트리거 콜라이더로 앵커 접근을 감지하여 AnchorBag.TryReturnAnchor()를 호출합니다.
    /// 앵커가 트리거를 한 번 벗어나면 IsReturnable이 true가 되고, 재진입 시 반환이 허용됩니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AnchorReturnZone : MonoBehaviour
    {
        private AnchorBag _bag;

        private void Awake()
        {
            _bag = GetComponentInParent<AnchorBag>();
        }

        private void OnTriggerExit(Collider other)
        {
            var model = other.GetComponentInParent<IceAnchorModel>();
            if (model == null) return;

            model.IsReturnable = true;
        }

        private void OnTriggerStay(Collider other)
        {
            if (_bag == null) return;

            var model = other.GetComponentInParent<IceAnchorModel>();
            if (model == null) return;
            if (!model.IsReturnable) return;

            _bag.TryReturnAnchor(other);
        }
    }
}
