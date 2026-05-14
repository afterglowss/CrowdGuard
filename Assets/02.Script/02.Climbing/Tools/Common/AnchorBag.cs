using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CrowdGuard.Climbing.Tools.IceAnchor;
using Fusion;
using TMPro;

namespace CrowdGuard.Climbing.Tools.Common
{
    /// <summary>
    /// 앵커 가방. 앵커 개수 관리 + 꺼내기/넣기 상호작용 + 오브젝트 풀 반환 처리.
    /// XRSimpleInteractable을 사용하여 가방은 벨트에 고정된 채, 앵커만 스폰/회수합니다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class AnchorBag : NetworkBehaviour
    {
        [Header("Anchor Pool")]
        [Tooltip("풀에서 관리할 앵커 프리팹")]
        [SerializeField] private GameObject _anchorPrefab;

        [Tooltip("초기 앵커 보유 개수")]
        [SerializeField] private int _initialCount = 3;

        [Tooltip("앵커를 놓은 후 풀 반환까지 대기 시간 (초)")]
        [SerializeField] private float _despawnDelay = 5f;

        [Tooltip("앵커가 스폰될 위치 (가방 위)")]
        [SerializeField] private Transform _spawnPoint;

        [Header("UI")]
        [Tooltip("남아있는 앵커 개수를 표시할 월드 스페이스 TMP 텍스트")]
        [SerializeField] private TMP_Text _remainingAnchorText;

        private int _currentCount;
        private readonly List<GameObject> _pool = new();
        private readonly Dictionary<GameObject, Coroutine> _despawnCoroutines = new();
        private readonly Dictionary<GameObject, Action<bool>> _heldHandlers = new();

        private XRSimpleInteractable _simpleInteractable;
        private XRInteractionManager _interactionManager;

        /// <summary>
        /// 현재 보유 앵커 개수.
        /// </summary>
        public int CurrentCount => _currentCount;

        /// <summary>
        /// 보유 개수 변경 시 발화.
        /// </summary>
        public event Action<int> OnCountChanged;

        // ===================== 생명주기 =====================

        private void Awake()
        {
            _simpleInteractable = GetComponent<XRSimpleInteractable>();
        }

        private void Start()
        {
            _interactionManager = FindAnyObjectByType<XRInteractionManager>();
            Debug.Log($"[AnchorBag] Start — InteractionManager: {(_interactionManager != null ? "OK" : "NULL")}");

            // Guard: ToolBeltManager에 의해 비활성화된 경우 풀 초기화하지 않음
            if (!gameObject.activeInHierarchy) return;

            InitializePool();
        }

        private void OnEnable()
        {
            _simpleInteractable.selectEntered.AddListener(OnBagGrabbed);
            Debug.Log($"[AnchorBag] OnEnable — selectEntered 리스너 등록. interactable={_simpleInteractable.name}, layer={gameObject.layer}");
        }

        private void OnDisable()
        {
            _simpleInteractable.selectEntered.RemoveListener(OnBagGrabbed);
        }

        // ===================== 풀 초기화 =====================

        private void InitializePool()
        {
            _currentCount = _initialCount;
            UpdateRemainingAnchorText();
            // 프리팹을 미리 스폰하지 않음 — TakeAnchor 시 Runner.Spawn으로 생성
            Debug.Log($"[AnchorBag] 풀 초기화 완료. 초기 개수={_currentCount}");
        }

        // ===================== 꺼내기 (XRI 연동) =====================

        /// <summary>
        /// 가방을 그랩하면 앵커를 꺼내서 해당 손에 강제 그랩시킵니다.
        /// </summary>
        private void OnBagGrabbed(SelectEnterEventArgs args)
        {
            Debug.Log($"[AnchorBag] >>> OnBagGrabbed 호출됨! interactor={args.interactorObject.transform.name}");

            var anchor = TakeAnchor();
            if (anchor == null)
            {
                Debug.LogWarning("[AnchorBag] TakeAnchor 실패 — 앵커가 없거나 풀이 비어있음");
                return;
            }

            var anchorGrab = anchor.GetComponentInChildren<XRGrabInteractable>();
            if (anchorGrab == null)
            {
                Debug.LogError($"[AnchorBag] 앵커에 XRGrabInteractable이 없음! anchor={anchor.name}");
                return;
            }
            if (_interactionManager == null)
            {
                Debug.LogError("[AnchorBag] InteractionManager가 null!");
                return;
            }

            Debug.Log($"[AnchorBag] 그랩 전환 코루틴 시작. anchor={anchor.name}, anchorGrab={anchorGrab.name}");
            StartCoroutine(TransferGrabNextFrame(args.interactorObject, anchorGrab));
        }

        /// <summary>
        /// 가방 selectEntered 처리 완료 후 물리 업데이트를 기다린 뒤 그랩 전환을 수행합니다.
        /// </summary>
        private IEnumerator TransferGrabNextFrame(IXRSelectInteractor interactor, XRGrabInteractable anchorGrab)
        {
            Debug.Log("[AnchorBag] TransferGrab — WaitForFixedUpdate 대기 중...");
            yield return new WaitForFixedUpdate();
            yield return null;
            Debug.Log($"[AnchorBag] TransferGrab — 대기 완료. bag.isSelected={_simpleInteractable.isSelected}, anchorGrab.enabled={anchorGrab.enabled}, anchorGrab.gameObject.activeSelf={anchorGrab.gameObject.activeSelf}");

            // 가방에서 손 해제 (해당 인터랙터가 실제로 잡고 있을 때만)
            if (_simpleInteractable.interactorsSelecting.Contains(interactor))
            {
                Debug.Log("[AnchorBag] TransferGrab — 가방 SelectExit 실행");
                _interactionManager.SelectExit(
                    interactor,
                    (IXRSelectInteractable)_simpleInteractable);
            }
            else
            {
                Debug.Log("[AnchorBag] TransferGrab — 가방이 이미 해제됨 (skip SelectExit)");
            }

            // 같은 손으로 앵커를 그랩
            Debug.Log($"[AnchorBag] TransferGrab — 앵커 SelectEnter 실행. interactor={interactor.transform.name}");
            _interactionManager.SelectEnter(
                interactor,
                (IXRSelectInteractable)anchorGrab);
            Debug.Log($"[AnchorBag] TransferGrab — 완료! anchorGrab.isSelected={anchorGrab.isSelected}");
        }

        /// <summary>
        /// 풀에서 비활성 앵커를 꺼냅니다. 보유 개수를 감소시키고 소실 감시를 시작합니다.
        /// </summary>
        public GameObject TakeAnchor()
        {
            if (_currentCount <= 0) return null;

            Transform spawn = _spawnPoint != null ? _spawnPoint : transform;
            var anchorNO = Runner.Spawn(_anchorPrefab, spawn.position, spawn.rotation);
            var anchor = anchorNO.gameObject;

            _pool.Add(anchor);

            _currentCount--;
            UpdateRemainingAnchorText();
            OnCountChanged?.Invoke(_currentCount);

            // 소실 감시 시작
            var model = anchor.GetComponent<IceAnchorModel>();
            if (model != null)
            {
                Action<bool> handler = held => OnAnchorHeldChanged(anchor, model, held);
                _heldHandlers[anchor] = handler;
                model.OnHeldStateChanged += handler;
            }

            Debug.Log($"[AnchorBag] 앵커 꺼냄. 남은 개수: {_currentCount}");
            return anchor;
        }

        // ===================== 넣기 (트리거 콜라이더 연동) =====================

        /// <summary>
        /// 앵커를 가방에 반환합니다. 보유 개수를 증가시킵니다.
        /// </summary>
        public bool ReturnAnchor(GameObject anchor)
        {
            if (!_pool.Contains(anchor)) return false;

            CancelDespawn(anchor);
            UnsubscribeAnchor(anchor);

            var no = anchor.GetComponent<NetworkObject>();
            if (no != null && no.IsValid)
                Runner.Despawn(no);

            _pool.Remove(anchor);

            _currentCount++;
            UpdateRemainingAnchorText();
            OnCountChanged?.Invoke(_currentCount);

            Debug.Log($"[AnchorBag] 앵커 반환. 남은 개수: {_currentCount}");
            return true;
        }

        /// <summary>
        /// 외부에서 호출하는 넣기 진입점. AnchorReturnZone에서 호출합니다.
        /// </summary>
        public void TryReturnAnchor(Collider other)
        {
            var model = other.GetComponentInParent<IceAnchorModel>();
            if (model == null) return;

            var anchorRoot = model.gameObject;
            if (!_pool.Contains(anchorRoot)) return;

            if (model.IsHeld) return;
            if (model.IsFullySecured) return;
            if (model.IsInserted) return;

            ReturnAnchor(anchorRoot);
        }

        // ===================== 소실 타이머 =====================

        private void OnAnchorHeldChanged(GameObject anchor, IceAnchorModel model, bool isHeld)
        {
            if (isHeld)
            {
                CancelDespawn(anchor);
                return;
            }

            // 놓음 — 삽입 중이 아니면 소실 타이머 시작
            if (!model.IsInserted && !model.IsFullySecured)
            {
                StartDespawnTimer(anchor, model);
            }
        }

        private void StartDespawnTimer(GameObject anchor, IceAnchorModel model)
        {
            CancelDespawn(anchor);
            var coroutine = StartCoroutine(DespawnAfterDelay(anchor, model));
            _despawnCoroutines[anchor] = coroutine;
        }

        private void CancelDespawn(GameObject anchor)
        {
            if (_despawnCoroutines.TryGetValue(anchor, out var coroutine))
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
                _despawnCoroutines.Remove(anchor);
            }
        }

        /// <summary>
        /// 대기 후 조건부 풀 반환 (소실). 보유 개수는 복구되지 않습니다.
        /// </summary>
        private IEnumerator DespawnAfterDelay(GameObject anchor, IceAnchorModel model)
        {
            yield return new WaitForSeconds(_despawnDelay);

            if (model.IsHeld || model.IsInserted || model.IsFullySecured)
            {
                _despawnCoroutines.Remove(anchor);
                yield break;
            }

            Debug.Log("[AnchorBag] 앵커 소실 — 네트워크 Despawn (개수 복구 없음)");
            UnsubscribeAnchor(anchor);

            var no = anchor.GetComponent<NetworkObject>();
            if (no != null && no.IsValid)
                Runner.Despawn(no);

            _pool.Remove(anchor);
            _despawnCoroutines.Remove(anchor);
        }
        /// <summary>
        /// 앵커의 OnHeldStateChanged 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeAnchor(GameObject anchor)
        {
            if (!_heldHandlers.TryGetValue(anchor, out var handler)) return;

            var model = anchor.GetComponent<IceAnchorModel>();
            if (model != null)
                model.OnHeldStateChanged -= handler;

            _heldHandlers.Remove(anchor);
        }

        private void UpdateRemainingAnchorText()
        {
            if (_remainingAnchorText == null) return;

            _remainingAnchorText.text = $"Remaining Anchor: {_currentCount}";
        }
    }
}
