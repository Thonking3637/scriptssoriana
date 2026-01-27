using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class MenuPanelFX : MonoBehaviour
{
    public CanvasGroup cg;
    public float inDur = 0.25f, outDur = 0.18f;

    void Awake() { if (!cg) cg = GetComponent<CanvasGroup>(); }
    void Reset() { cg = GetComponent<CanvasGroup>(); }

    public void Show()
    {
        if (!cg) { cg = GetComponent<CanvasGroup>(); if (!cg) { Debug.LogWarning("MenuPanelFX: falta CanvasGroup"); return; } }
        DOTween.Kill(cg); DOTween.Kill(transform);
        gameObject.SetActive(true);
        cg.alpha = 0f; cg.interactable = true; cg.blocksRaycasts = true;
        transform.localScale = Vector3.one * 0.94f;
        transform.DOScale(1f, inDur).SetEase(Ease.OutBack);
        cg.DOFade(1f, inDur).SetEase(Ease.OutQuad);
    }

    public void Hide()
    {
        if (!cg) { cg = GetComponent<CanvasGroup>(); if (!cg) { gameObject.SetActive(false); return; } }
        cg.interactable = false; cg.blocksRaycasts = false;
        DOTween.Kill(cg); DOTween.Kill(transform);
        transform.DOScale(0.96f, outDur).SetEase(Ease.InQuad);
        cg.DOFade(0f, outDur).SetEase(Ease.InQuad)
          .OnComplete(() => gameObject.SetActive(false));
    }
}
