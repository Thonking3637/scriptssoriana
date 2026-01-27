// LevelPreview.cs  (panel de la derecha/arriba)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelPreview : MonoBehaviour
{
    public UnifiedScrollSelector selector;
    public Image previewImage;
    public TMP_Text titleText, descriptionText;
    public float fadeDur = 0.2f;

    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
    }
    void OnEnable()
    {
        if (selector != null)
            OnSelectionChanged(selector.CurrentIndex);
    }

    void OnDisable()
    {
        if (cg) DOTween.Kill(cg);
    }

    public void OnSelectionChanged(int index)
    {
        if (!selector || index < 0 || index >= selector.items.Count) return;
        var btn = selector.items[index];
        var meta = btn ? btn.GetComponent<LevelItemMeta>() : null;
        if (!meta) return;

        if (!cg) { cg = GetComponent<CanvasGroup>(); if (!cg) return; }
        cg.DOKill();
        cg.DOFade(0f, fadeDur).OnComplete(() =>
        {
            if (previewImage) previewImage.sprite = meta.preview;
            if (titleText) titleText.text = meta.title;
            if (descriptionText) descriptionText.text = meta.description;
            cg.DOFade(1f, fadeDur);
        });
    }
}
