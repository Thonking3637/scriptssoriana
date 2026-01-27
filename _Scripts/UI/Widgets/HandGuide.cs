using UnityEngine;
using DG.Tweening;

public class HandGuide : MonoBehaviour
{
    public RectTransform handTransform; // 🔹 Objeto de la mano en la UI
    private bool isAnimating = false;
    private Tween handTween;

    private void Awake()
    {
        gameObject.SetActive(false); // 🔹 La mano está oculta al inicio
    }

    /// <summary>
    /// Muestra la guía visual y la mantiene en loop hasta que se complete la acción.
    /// </summary>
    public void ShowGuide(Vector2 startPosition, Vector2 endPosition, float duration)
    {
        if (isAnimating) return; // 🔹 Solo se ejecuta si no está ya animándose

        isAnimating = true;
        gameObject.SetActive(true);

        handTransform.anchoredPosition = startPosition;
        handTween = handTransform.DOAnchorPos(endPosition, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo); // 🔹 Loop infinito hasta que se oculte manualmente
    }

    /// <summary>
    /// Detiene la animación y oculta la guía cuando la acción se completa.
    /// </summary>
    public void HideGuide()
    {
        if (!isAnimating) return;

        isAnimating = false;
        handTween.Kill();
        gameObject.SetActive(false);
    }
}
