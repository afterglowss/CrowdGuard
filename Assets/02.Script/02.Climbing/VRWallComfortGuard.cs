using UnityEngine;

/// <summary>
/// VR 플레이어가 물리적으로 HMD를 IceWall 안으로 넣었을 때 멀미를 줄이기 위한 컴포넌트.
///
/// [동작 원리]
/// - 카메라 위치를 기준으로 가장 가까운 IceWall 콜라이더까지의 거리를 매 프레임 측정합니다.
/// - 벽에 가까워질수록 비네팅(주변 시야 어두워짐)을 서서히 적용합니다.
/// - 벽 안에 완전히 들어간 경우 거의 암전에 가까운 페이드를 적용합니다.
/// - rig(xrRigPivot)는 절대 강제로 이동시키지 않습니다.
///   (클라이밍 중 갑작스러운 위치 보정은 심각한 멀미를 유발)
///
/// [씬 세팅]
/// - 씬 내 아무 오브젝트에 붙이면 됩니다. Awake에서 Camera.main을 자동 탐색합니다.
/// - ScreenEffectManager와 완전히 독립적으로 동작합니다. (Quad, Material 별도 생성)
/// </summary>
public class VRWallComfortGuard : MonoBehaviour
{
    [Header("Detection Range")]
    [Tooltip("이 거리부터 비네팅이 시작됩니다 (m)")]
    [SerializeField] private float _vignetteStartDist = 0.25f;

    [Tooltip("이 거리에서 비네팅이 최대가 됩니다. 벽 표면 바로 앞.")]
    [SerializeField] private float _vignetteMaxDist = 0.04f;

    [Header("Effect Intensity")]
    [Tooltip("벽에 가까울 때 최대 비네팅 알파 (0~1). 0.65 권장 — 시야는 유지하되 불편하게.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxVignetteAlpha = 0.65f;

    [Tooltip("벽 내부에 들어왔을 때 페이드 알파. 0.88 권장 — 거의 보이지 않지만 완전 암전은 아님.")]
    [Range(0f, 1f)]
    [SerializeField] private float _insideWallAlpha = 0.88f;

    [Tooltip("알파값 변화 속도. 높을수록 빠르게 반응.")]
    [SerializeField] private float _alphaLerpSpeed = 12f;

    [Header("Quad Settings")]
    [Tooltip("카메라로부터 Quad 거리(m). ScreenEffectManager의 fadequad(0.15)보다 약간 앞.")]
    [SerializeField] private float _quadDistance = 0.13f;

    [SerializeField] private float _quadScale = 0.5f;

    // IceWall 레이어 고정 (아이스 바일과 동일 정책)
    private int _wallLayerMask;

    private Material _mat;
    private Camera _cam;
    private float _targetAlpha;

    private void Awake()
    {
        _wallLayerMask = LayerMask.GetMask("IceWall");
        _cam = Camera.main;
        CreateVignetteQuad();
    }

    private void Update()
    {
        if (_cam == null || _mat == null) return;

        _targetAlpha = ComputeTargetAlpha(_cam.transform.position);

        // 부드럽게 알파 보간
        Color c = _mat.color;
        c.a = Mathf.Lerp(c.a, _targetAlpha, _alphaLerpSpeed * Time.deltaTime);
        _mat.color = c;
    }

    private float ComputeTargetAlpha(Vector3 camPos)
    {
        // 탐색 반경: 비네팅 시작 거리 + 여유
        Collider[] nearby = Physics.OverlapSphere(camPos, _vignetteStartDist + 0.01f, _wallLayerMask);
        if (nearby.Length == 0) return 0f;

        float minDist = float.MaxValue;
        bool isInside = false;

        foreach (Collider col in nearby)
        {
            Vector3 closest = col.ClosestPoint(camPos);
            float distToSurface = Vector3.Distance(camPos, closest);

            // ClosestPoint가 camPos를 그대로 반환하면 콜라이더 내부에 있는 것
            if (distToSurface < 0.001f)
            {
                isInside = true;
                break;
            }

            if (distToSurface < minDist)
                minDist = distToSurface;
        }

        if (isInside)
            return _insideWallAlpha;

        // _vignetteStartDist(먼 쪽) → _vignetteMaxDist(가까운 쪽) 를 0 → 1로 매핑
        float t = 1f - Mathf.InverseLerp(_vignetteMaxDist, _vignetteStartDist, minDist);
        return Mathf.Lerp(0f, _maxVignetteAlpha, t);
    }

    private void CreateVignetteQuad()
    {
        if (_cam == null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "_WallComfortVignetteQuad";
        Destroy(go.GetComponent<MeshCollider>());

        go.transform.SetParent(_cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, _quadDistance);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = new Vector3(_quadScale, _quadScale, 1f);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Sprites/Default");

        _mat = new Material(shader)
        {
            color = new Color(0f, 0f, 0f, 0f)
        };
        _mat.SetFloat("_Surface", 1f);
        _mat.SetFloat("_Blend", 0f);
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite", 0);
        _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        var rend = go.GetComponent<MeshRenderer>();
        rend.material = _mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }
}
