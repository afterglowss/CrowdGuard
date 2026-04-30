using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// VR 양안 렌더링 호환 화면 효과 매니저.
/// Screen Space Canvas 대신 카메라 자식 Quad Mesh를 사용하여
/// OpenXR / Meta Quest 컴포지터 레이어 문제를 우회합니다.
///
/// ※ 동결 서리 효과는 별도 셰이더 Quad로 이미 구현되어 있으므로 여기서 관리하지 않습니다.
///
/// [씬 세팅 방법]
/// 1. 게임 씬의 아무 빈 오브젝트에 이 컴포넌트를 추가합니다.
/// 2. fadeQuad / vignetteQuad 슬롯을 비워두면 런타임에 자동으로 생성됩니다.
/// 3. 각 Quad는 Camera.main의 자식으로 자동 배치됩니다.
/// </summary>
public class ScreenEffectManager : MonoBehaviour
{
    public static ScreenEffectManager Instance;

    [Header("VR Quad References (비워두면 자동 생성)")]
    [Tooltip("암전용 Quad. 비워두면 자동 생성됩니다.")]
    public MeshRenderer fadeQuad;

    [Tooltip("추락 비네팅용 Quad. 비워두면 자동 생성됩니다.")]
    public MeshRenderer vignetteQuad;

    [Header("Quad Position Settings")]
    [Tooltip("카메라로부터 Quad까지의 거리(m). 너무 가까우면 Near Clip에 잘림.")]
    public float quadDistance = 0.15f;

    [Tooltip("FOV를 여유있게 덮는 Quad 크기. quadDistance=0.15 기준 0.5이면 약 118° 커버.")]
    public float quadScale = 0.5f;

    // 내부 Material 캐시
    private Material _fadeMat;
    private Material _vignetteMat;

    // 현재 실행 중인 효과 코루틴 (중단 가능하도록 추적)
    private Coroutine _activeEffectCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        InitQuads();

        // 시작 시 투명으로 초기화
        SetQuadAlpha(_fadeMat, 0f);
        SetQuadAlpha(_vignetteMat, 0f);
    }

    // ───────────────────────────────────────────────
    //  Quad 초기화
    // ───────────────────────────────────────────────

    private void InitQuads()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[ScreenEffectManager] Camera.main을 찾을 수 없습니다!");
            return;
        }

        // 암전 Quad (검정)
        fadeQuad = GetOrCreateQuad(fadeQuad, "_FadeQuad", cam, new Color(0f, 0f, 0f, 0f));
        _fadeMat = fadeQuad.material;

        // 비네팅 Quad (검정)
        vignetteQuad = GetOrCreateQuad(vignetteQuad, "_VignetteQuad", cam, new Color(0f, 0f, 0f, 0f));
        _vignetteMat = vignetteQuad.material;
    }

    /// <summary>
    /// 기존 MeshRenderer가 없으면 런타임에 Quad를 생성해 카메라 자식으로 배치합니다.
    /// </summary>
    private MeshRenderer GetOrCreateQuad(MeshRenderer existing, string quadName,
                                          Camera parentCam, Color baseColor)
    {
        if (existing != null)
        {
            // 이미 있으면 카메라 자식인지만 확인
            existing.transform.SetParent(parentCam.transform, false);
            SetupQuadTransform(existing.transform);
            var mat = CreateUnlitTransparentMaterial(baseColor);
            existing.material = mat;
            return existing;
        }

        // 없으면 런타임 생성
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = quadName;

        // Collider 불필요
        Destroy(go.GetComponent<MeshCollider>());

        // 카메라 자식 배치
        go.transform.SetParent(parentCam.transform, false);
        SetupQuadTransform(go.transform);

        // Unlit Transparent 머티리얼 할당
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.material = CreateUnlitTransparentMaterial(baseColor);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return renderer;
    }

    private void SetupQuadTransform(Transform t)
    {
        // 카메라 로컬 좌표 기준, 정면 quadDistance 앞에 배치
        t.localPosition = new Vector3(0f, 0f, quadDistance);
        t.localRotation = Quaternion.identity;
        // quadDistance=0.15, quadScale=0.5 → 118° 커버 (Quest2 기준 96° FOV 충분)
        t.localScale = new Vector3(quadScale, quadScale, 1f);
    }

    /// <summary>
    /// 양안 렌더링에 안전한 Unlit + Alpha Blend 머티리얼 생성.
    /// "Unlit/Transparent" 셰이더가 없는 URP/HDRP 환경에서도
    /// 기본 셰이더 폴백으로 작동합니다.
    /// </summary>
    private Material CreateUnlitTransparentMaterial(Color baseColor)
    {
        // URP 환경이면 "Universal Render Pipeline/Unlit", 빌트인이면 "Unlit/Transparent"
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.color = baseColor;

        // Transparent 렌더링 설정
        mat.SetFloat("_Surface", 1f);            // URP: Transparent
        mat.SetFloat("_Blend", 0f);              // URP: Alpha
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return mat;
    }

    // ───────────────────────────────────────────────
    //  공개 API
    // ───────────────────────────────────────────────

    /// <summary>
    /// 텐트 진입/퇴장, 리스폰 등에서 호출하는 단순 페이드.
    /// fadeIn=true  → 검은 화면에서 밝아짐 (Fade In)
    /// fadeIn=false → 밝은 화면이 어두워짐 (Fade Out)
    /// </summary>
    public IEnumerator FadeScreenRoutine(float duration, bool fadeIn)
    {
        if (_fadeMat == null) yield break;

        float startAlpha = fadeIn ? 1f : 0f;
        float endAlpha   = fadeIn ? 0f : 1f;

        SetQuadAlpha(_fadeMat, startAlpha);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            SetQuadAlpha(_fadeMat, Mathf.Lerp(startAlpha, endAlpha, timer / duration));
            yield return null;
        }

        SetQuadAlpha(_fadeMat, endAlpha);
    }

    /// <summary>
    /// 추락 중 비네팅만 서서히 짙어집니다.
    /// 리스폰 타이밍과 분리되어 있으며, StopAllEffects()로 언제든 중단 가능.
    /// </summary>
    public void StartFallVignette(float fallDuration)
    {
        StopAllEffects();
        _activeEffectCoroutine = StartCoroutine(FallVignetteCoroutine(fallDuration));
    }

    private IEnumerator FallVignetteCoroutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            SetQuadAlpha(_vignetteMat, Mathf.Lerp(0f, 1f, timer / duration));
            yield return null;
        }
    }

    /// <summary>
    /// 현재 실행 중인 모든 효과를 즉시 중단하고 화면을 초기화합니다.
    /// Respawn 직전에 호출하여 엇박자 페이드를 방지합니다.
    /// </summary>
    public void StopAllEffects()
    {
        if (_activeEffectCoroutine != null)
        {
            StopCoroutine(_activeEffectCoroutine);
            _activeEffectCoroutine = null;
        }
    }

    /// <summary>
    /// 리스폰 시퀀스:
    ///   1. 빠르게 암전 (fadeOutDuration)
    ///   2. onBlackScreen 콜백 실행 → 이 시점에 텔레포트
    ///   3. 서서히 밝아짐 (fadeInDuration)
    ///
    /// 텔레포트가 검은 화면 뒤에서 일어나므로 순간이동이 보이지 않습니다.
    /// </summary>
    public void StartRespawnSequence(float fadeOutDuration, Action onBlackScreen, float fadeInDuration)
    {
        StopAllEffects();
        _activeEffectCoroutine = StartCoroutine(RespawnSequenceCoroutine(fadeOutDuration, onBlackScreen, fadeInDuration));
    }

    private IEnumerator RespawnSequenceCoroutine(float fadeOut, Action onBlack, float fadeIn)
    {
        // 1. 현재 비네팅 유지하면서 빠르게 암전
        float startVignette = _vignetteMat != null ? _vignetteMat.color.a : 0f;
        float timer = 0f;
        while (timer < fadeOut)
        {
            timer += Time.deltaTime;
            float t = timer / fadeOut;
            SetQuadAlpha(_fadeMat, Mathf.Lerp(0f, 1f, t));
            SetQuadAlpha(_vignetteMat, Mathf.Lerp(startVignette, 1f, t));
            yield return null;
        }
        SetQuadAlpha(_fadeMat, 1f);
        SetQuadAlpha(_vignetteMat, 1f);

        // 2. 완전히 검은 화면에서 텔레포트 (플레이어는 아무것도 안 보임)
        onBlack?.Invoke();

        // 3. 서서히 밝아짐
        timer = 0f;
        while (timer < fadeIn)
        {
            timer += Time.deltaTime;
            float t = timer / fadeIn;
            SetQuadAlpha(_fadeMat, Mathf.Lerp(1f, 0f, t));
            SetQuadAlpha(_vignetteMat, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        SetQuadAlpha(_fadeMat, 0f);
        SetQuadAlpha(_vignetteMat, 0f);
        _activeEffectCoroutine = null;
    }

    // ─── 기존 API 유지 (텐트 등 다른 곳에서 사용) ───────────────────

    // ───────────────────────────────────────────────
    //  내부 유틸
    // ───────────────────────────────────────────────

    private void SetQuadAlpha(Material mat, float alpha)
    {
        if (mat == null) return;
        Color c = mat.color;
        c.a = alpha;
        mat.color = c;
    }
}
