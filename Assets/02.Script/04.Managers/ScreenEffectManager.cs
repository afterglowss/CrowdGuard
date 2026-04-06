using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenEffectManager : MonoBehaviour
{
    public static ScreenEffectManager Instance;

    public Image vignetteImage;
    public Image blackScreenImage;
    [Tooltip("2016년도 에셋 기반의 화면 가장자리 서리 이미지")]
    public Image frostImage;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.OnFreezeGaugeChanged += UpdateFrostEffect;
        }
        
        // 시작할 때 서리 이미지 투명 상태로 초기화
        SetAlpha(frostImage, 0f);
    }

    private void OnDestroy()
    {
        if (SurvivalManager.Instance != null)
        {
            SurvivalManager.Instance.OnFreezeGaugeChanged -= UpdateFrostEffect;
        }
    }

    private void UpdateFrostEffect(float currentGauge)
    {
        if (frostImage == null) return;

        // 동결 게이지가 120 미만이면 효과 없음
        if (currentGauge < SurvivalManager.EFFECT_START_GAUGE)
        {
            SetAlpha(frostImage, 0f);
            return;
        }

        // 120 ~ 600 구간에서 알파값을 0 -> 1로 부드럽게 증가
        float effectRange = SurvivalManager.MAX_FREEZE_GAUGE - SurvivalManager.EFFECT_START_GAUGE;
        float currentProgress = currentGauge - SurvivalManager.EFFECT_START_GAUGE;
        float alpha = Mathf.Clamp01(currentProgress / effectRange);

        SetAlpha(frostImage, alpha);
    }

    public void StartFallEffect(float duration)
    {
        StartCoroutine(FallEffectCoroutine(duration));
    }

    private IEnumerator FallEffectCoroutine(float duration)
    {
        float timer = 0f;

        // 1. 떨어지는 동안 비네팅이 점점 짙어짐 (시야 축소)
        while (timer < duration - 0.5f) // 마지막 0.5초 전까지
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / (duration - 0.5f));
            SetAlpha(vignetteImage, alpha);
            yield return null;
        }

        // 2. 바닥에 닿기 직전 (또는 리스폰 직전) 순식간에 암전!
        SetAlpha(blackScreenImage, 1f);

        // 리스폰이 끝날 때까지 아주 잠깐 대기
        yield return new WaitForSeconds(0.5f);

        // 3. 서서히 밝아지며 부활 (Fade In)
        float fadeTimer = 0f;
        while (fadeTimer < 1.0f)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / 1.0f);
            SetAlpha(blackScreenImage, alpha);
            SetAlpha(vignetteImage, alpha); // 비네팅도 같이 없앰
            yield return null;
        }
    }

    /// <summary>
    /// 외부(텐트 진입 등)에서 부를 수 있는 단순 화면 페이드 인/아웃 코루틴
    /// </summary>
    public IEnumerator FadeScreenRoutine(float duration, bool fadeIn)
    {
        if (blackScreenImage == null) yield break;
        
        float timer = 0f;
        float startAlpha = fadeIn ? 1f : 0f; // fadeIn==true면 검은화면(1)->투명(0)
        float endAlpha = fadeIn ? 0f : 1f;

        // 원초에 명확히 세팅
        SetAlpha(blackScreenImage, startAlpha);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, timer / duration);
            SetAlpha(blackScreenImage, currentAlpha);
            yield return null;
        }

        SetAlpha(blackScreenImage, endAlpha);
    }

    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}