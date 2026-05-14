using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// GameOverManager.OnGameOver 이벤트를 받아 VR 월드스페이스 캔버스로 알림을 표시합니다.
/// ScreenEffectManager와 동일하게 Camera.main 자식 오브젝트로 배치됩니다.
///
/// [씬 세팅 방법]
/// 1. 빈 게임오브젝트에 이 컴포넌트를 추가합니다.
/// 2. gameOverCanvas 슬롯에 World Space Canvas를 연결하거나 비워두면 자동 생성됩니다.
/// 3. messageText 슬롯에 TextMeshPro 텍스트를 연결하거나 비워두면 자동 생성됩니다.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI References (비워두면 자동 생성)")]
    public Canvas gameOverCanvas;
    public TextMeshProUGUI messageText;

    [Header("Settings")]
    public string gameOverMessage = "GAME OVER";
    public float fadeInDuration = 1.5f;

    [Tooltip("카메라로부터 캔버스까지의 거리(m)")]
    public float canvasDistance = 1.5f;

    [Tooltip("캔버스 크기(m)")]
    public float canvasSize = 1f;

    private CanvasGroup _canvasGroup;

    private void OnEnable()
    {
        GameOverManager.OnGameOver += ShowGameOver;
    }

    private void OnDisable()
    {
        GameOverManager.OnGameOver -= ShowGameOver;
    }

    private void Start()
    {
        InitCanvas();
    }

    private void InitCanvas()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[GameOverUI] Camera.main을 찾을 수 없습니다.");
            return;
        }

        if (gameOverCanvas == null)
        {
            var canvasGo = new GameObject("_GameOverCanvas");
            canvasGo.transform.SetParent(cam.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0f, canvasDistance);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * (canvasSize / 100f);

            gameOverCanvas = canvasGo.AddComponent<Canvas>();
            gameOverCanvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 100f);
        }

        if (_canvasGroup == null)
            _canvasGroup = gameOverCanvas.gameObject.GetOrAddComponent<CanvasGroup>();

        if (messageText == null)
        {
            var textGo = new GameObject("GameOverText");
            textGo.transform.SetParent(gameOverCanvas.transform, false);

            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            messageText = textGo.AddComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 12f;
            messageText.color = Color.white;
        }

        messageText.text = gameOverMessage;
        _canvasGroup.alpha = 0f;
        gameOverCanvas.gameObject.SetActive(false);
    }

    private void ShowGameOver()
    {
        if (_canvasGroup == null) return;
        gameOverCanvas.gameObject.SetActive(true);
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }
}

public static class GameObjectExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        return go.TryGetComponent(out T comp) ? comp : go.AddComponent<T>();
    }
}
