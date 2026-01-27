using System;
using UnityEngine;
using TMPro;
using System.Collections;

[DefaultExecutionOrder(-10000)]
public class IntroManager : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup fadeCanvas;
    public TextMeshProUGUI introText;

    [Header("Config")]
    public float fadeDuration = 1.5f;
    public float messageDuration = 2f;
    public float typingSpeed = 0.05f;

    private Action onIntroComplete;

    private void Awake()
    {
        if (fadeCanvas)
        {
            var canvas = fadeCanvas.GetComponentInParent<Canvas>();
            if (canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
            }
            fadeCanvas.gameObject.SetActive(true);
            fadeCanvas.alpha = 1f;
            fadeCanvas.blocksRaycasts = true;
            fadeCanvas.interactable = false;
        }
        if (introText) introText.text = "";
    }

    public void ShowIntro(string message, System.Action onComplete)
    {
        if (!fadeCanvas) return;

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.alpha = 1f;
        if (introText) introText.text = "";
        onIntroComplete = onComplete;

        StopAllCoroutines();
        StartCoroutine(PlayIntro(message));
    }

    private IEnumerator PlayIntro(string message)
    {
        yield return null;

        yield return StartCoroutine(TypeTextRealtime(message));
        yield return new WaitForSecondsRealtime(messageDuration);

        yield return StartCoroutine(FadeCanvasRealtime(0f, fadeDuration));

        fadeCanvas.gameObject.SetActive(false);
        onIntroComplete?.Invoke();
    }

    private IEnumerator FadeCanvasRealtime(float targetAlpha, float duration)
    {
        if (!fadeCanvas || duration <= 0f) { if (fadeCanvas) fadeCanvas.alpha = targetAlpha; yield break; }

        float start = fadeCanvas.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // tiempo no escalado (por si timeScale=0)
            fadeCanvas.alpha = Mathf.Lerp(start, targetAlpha, t / duration);
            yield return null;
        }
        fadeCanvas.alpha = targetAlpha;
    }

    private IEnumerator TypeTextRealtime(string msg)
    {
        if (!introText) yield break;
        introText.text = "";
        foreach (char c in msg)
        {
            introText.text += c;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, typingSpeed));
        }
    }
}
