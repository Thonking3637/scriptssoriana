using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PagerDots : MonoBehaviour
{
    [Header("References")]
    public UnifiedScrollSelector selector;     // Arrástralo (o se busca en padres)
    public RectTransform container;           // Donde se instancian los puntos (este GO por defecto)
    public Image dotPrefab;                    // Prefab de un punto (Image con sprite circular)

    [Header("Look & Feel")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.35f);
    public float activeScale = 1.1f;
    public float inactiveScale = 0.9f;
    public float animDur = 0.15f;

    [Header("Options")]
    public bool rebuildOnEnable = true;
    public bool autoWatchItemCount = true;    // reconstruye si cambia la cantidad

    private readonly List<Image> dots = new();
    private int lastIndex = -1;
    private int lastCount = -1;

    void Awake()
    {
        if (!selector) selector = GetComponentInParent<UnifiedScrollSelector>();
        if (!container) container = (RectTransform)transform;
    }

    void OnEnable()
    {
        if (selector) selector.onSelectionChanged.AddListener(OnSelectionChanged);
        if (rebuildOnEnable) Rebuild();
        Refresh(selector ? selector.CurrentIndex : 0, instant: true);
    }

    void OnDisable()
    {
        if (selector) selector.onSelectionChanged.RemoveListener(OnSelectionChanged);
    }

    void LateUpdate()
    {
        if (!autoWatchItemCount || selector == null) return;
        int c = selector.items != null ? selector.items.Count : 0;
        if (c != lastCount)
        {
            Rebuild();
            Refresh(selector.CurrentIndex, instant: true);
        }
    }

    void OnSelectionChanged(int idx) => Refresh(idx, instant: false);

    public void Rebuild()
    {
        // limpiar
        for (int i = container.childCount - 1; i >= 0; --i)
            Destroy(container.GetChild(i).gameObject);
        dots.Clear();

        int count = selector && selector.items != null ? selector.items.Count : 0;
        lastCount = count;

        for (int i = 0; i < count; i++)
        {
            var img = Instantiate(dotPrefab, container);
            img.raycastTarget = false;
            img.color = inactiveColor;
            img.transform.localScale = Vector3.one * inactiveScale;
            dots.Add(img);
        }
    }

    void Refresh(int index, bool instant)
    {
        if (dots.Count == 0) return;
        if (index < 0 || index >= dots.Count) index = 0;

        for (int i = 0; i < dots.Count; i++)
        {
            bool isActive = (i == index);
            var targetColor = isActive ? activeColor : inactiveColor;
            var targetScale = Vector3.one * (isActive ? activeScale : inactiveScale);

            if (instant)
            {
                dots[i].color = targetColor;
                dots[i].transform.localScale = targetScale;
            }
            else
            {
                dots[i].DOColor(targetColor, animDur);
                dots[i].transform.DOScale(targetScale, animDur).SetEase(Ease.OutQuad);
            }
        }
        lastIndex = index;
    }
}
