using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어 시선 기준으로 SphereCast를 쏴서 TutorialTarget을 감지하고,
/// World Space UI 패널의 헤더/설명 텍스트를 업데이트합니다.
///
/// [씬 설정]
/// 1. XR Rig 하위에 World Space Canvas 생성
/// 2. Canvas 하위에 패널 오브젝트, TextMeshPro - Header / Description 배치
/// 3. 이 컴포넌트를 XR Rig 또는 매니저 오브젝트에 추가하고 인스펙터 연결
/// 4. 각 도구 프리팹 루트에 TutorialTarget 컴포넌트 추가 후 텍스트 입력
/// </summary>
public class TutorialUIController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("시선 기준이 되는 카메라 (보통 XR HMD Camera)")]
    [SerializeField] private Camera xrCamera;

    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Gaze Settings")]
    [Tooltip("시선 감지 최대 거리 (m)")]
    [SerializeField] private float gazeDistance = 3f;

    [Tooltip("SphereCast 반경 — 클수록 넓은 범위 감지")]
    [SerializeField] private float gazeRadius = 0.15f;

    [Tooltip("감지할 레이어 마스크")]
    [SerializeField] private LayerMask gazeLayerMask = ~0;

    [Header("UI Fade")]
    [Tooltip("UI 사라지기까지의 딜레이 (s)")]
    [SerializeField] private float hideDelay = 0.4f;

    private TutorialTarget _currentTarget;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (xrCamera == null)
            xrCamera = Camera.main;

        if (uiPanel != null)
            uiPanel.SetActive(false);
    }

    private void Update()
    {
        if (xrCamera == null) return;

        TutorialTarget hit = CastGaze();

        if (hit != null && hit != _currentTarget)
        {
            _currentTarget = hit;
            StopHideCoroutine();
            ShowUI(hit);
        }
        else if (hit == null && _currentTarget != null)
        {
            _currentTarget = null;
            ScheduleHide();
        }
    }

    private TutorialTarget CastGaze()
    {
        Ray ray = new Ray(xrCamera.transform.position, xrCamera.transform.forward);

        if (Physics.SphereCast(ray, gazeRadius, out RaycastHit hitInfo, gazeDistance, gazeLayerMask))
            return hitInfo.collider.GetComponentInParent<TutorialTarget>();

        return null;
    }

    private void ShowUI(TutorialTarget target)
    {
        if (uiPanel != null) uiPanel.SetActive(true);
        if (headerText != null) headerText.text = target.header;
        if (descriptionText != null) descriptionText.text = target.description;
    }

    private void ScheduleHide()
    {
        StopHideCoroutine();
        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        if (uiPanel != null) uiPanel.SetActive(false);
        _hideCoroutine = null;
    }

    private void StopHideCoroutine()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
    }
}
