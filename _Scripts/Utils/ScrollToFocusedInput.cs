using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScrollToFocusedInput : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float scrollSpeed = 5f;

    private TMP_InputField[] _inputFields;
    private RectTransform _contentRect;

    private void Awake()
    {
        _inputFields = GetComponentsInChildren<TMP_InputField>(true);
        _contentRect = scrollRect.content;

        // Suscribir a cada input
        foreach (var input in _inputFields)
        {
            input.onSelect.AddListener(_ => ScrollToInput(input));
        }
    }

    private void ScrollToInput(TMP_InputField input)
    {
        Canvas.ForceUpdateCanvases();

        var inputRect = input.GetComponent<RectTransform>();
        float targetY = -inputRect.anchoredPosition.y - (scrollRect.viewport.rect.height * 0.3f);

        targetY = Mathf.Clamp(targetY, 0, _contentRect.rect.height - scrollRect.viewport.rect.height);

        StopAllCoroutines();
        StartCoroutine(SmoothScroll(targetY));
    }

    private System.Collections.IEnumerator SmoothScroll(float targetY)
    {
        while (Mathf.Abs(_contentRect.anchoredPosition.y - targetY) > 1f)
        {
            var pos = _contentRect.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, targetY, Time.unscaledDeltaTime * scrollSpeed);
            _contentRect.anchoredPosition = pos;
            yield return null;
        }
    }
}