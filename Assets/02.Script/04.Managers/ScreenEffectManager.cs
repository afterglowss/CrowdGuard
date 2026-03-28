using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenEffectManager : MonoBehaviour
{
    public static ScreenEffectManager Instance;

    public Image vignetteImage;
    public Image blackScreenImage;

    private void Awake()
    {
        if (Instance == null) Instance = this;
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

    private void SetAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}