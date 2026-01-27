using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using DG.Tweening;

public class UnifiedScrollSelector : MonoBehaviour
{
    public enum Orientation { Horizontal, Vertical }

    [Header("Refs")]
    public ScrollRect scroll;                  // Tu ScrollRect (H o V)
    public RectTransform content;              // Content del ScrollRect
    public RectTransform focusFrame;           // Marco/indicador opcional
    public List<Button> items;                 // Vacío = auto con Buttons hijos

    [Header("Comportamiento")]
    public Orientation orientation = Orientation.Horizontal;
    public bool wrap = true;
    public int startIndex = 0;

    [Header("Animación")]
    public float selectScale = 1.06f;
    public float normalScale = 1f;
    public float snapDuration = 0.18f;
    public Ease snapEase = Ease.OutCubic;
    public Ease scaleEase = Ease.OutQuad;

    [Header("Audio")]
    public AudioSource uiAudio;
    public AudioClip sMove, sSubmit, sCancel;

    [Header("Eventos")]
    public UnityEvent<int> onSelectionChanged;

    [Header("Fade")]
    public float fadeDuration = 0.2f;
    [Range(0, 1f)] public float alphaSelected = 1f;
    [Range(0, 1f)] public float alphaUnselected = 0.65f;

    int index = -1;
    readonly List<Tween> _tw = new();

    public int CurrentIndex => index;

    void Reset()
    {
        if (!scroll) scroll = GetComponentInChildren<ScrollRect>(true);
        if (!content && scroll) content = scroll.content;
    }

    void Awake()
    {
        if (!content && scroll) content = scroll.content;
        if ((items == null || items.Count == 0) && content)
            items = new List<Button>(content.GetComponentsInChildren<Button>(true));

        if (items != null)
        {
            foreach (var b in items)
            {
                if (!b) continue;
                var cg = b.GetComponent<CanvasGroup>();
                if (!cg) cg = b.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alphaUnselected;
            }
        }
    }

    void OnEnable()
    {
        if (items.Count > 0)
        {
            startIndex = Mathf.Clamp(startIndex, 0, items.Count - 1);
            Select(startIndex, instant: true, playSound: false);

            // Forzar estados de alpha al entrar
            for (int k = 0; k < items.Count; k++)
            {
                var cg = items[k]?.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = (k == CurrentIndex) ? alphaSelected : alphaUnselected;
            }
        }
    }

    void OnDisable()
    {
        foreach (var t in _tw) if (t.IsActive()) t.Kill(false);
        _tw.Clear();
    }

    void Update()
    {
        if (orientation == Orientation.Horizontal)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Prev();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) Next();
            if (Input.GetKeyDown(KeyCode.UpArrow)) Prev();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Submit();
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Cancel();
    }

    // ===== API para flechas en pantalla =====
    public void OnArrowRight() { if (orientation == Orientation.Horizontal) Next(); else Submit(); }
    public void OnArrowLeft() { if (orientation == Orientation.Horizontal) Prev(); else Cancel(); }
    public void OnArrowDown() { if (orientation == Orientation.Vertical) Next(); }
    public void OnArrowUp() { if (orientation == Orientation.Vertical) Prev(); }
    public void OnSubmit() => Submit();
    public void OnCancel() => Cancel();

    // ===== Navegación =====
    public void Next()
    {
        if (items.Count == 0) return;
        int ni = index + 1;
        if (ni >= items.Count) ni = wrap ? 0 : items.Count - 1;
        Select(ni);
    }

    public void Prev()
    {
        if (items.Count == 0) return;
        int ni = index - 1;
        if (ni < 0) ni = wrap ? items.Count - 1 : 0;
        Select(ni);
    }

    public void Submit()
    {
        if (!Valid(index)) return;
        Play(sSubmit);
        items[index].onClick.Invoke();

        if (Valid(index))
        {
            var t = items[index].transform;
            t.DOKill();
            t.DOScale(selectScale * 1.05f, 0.08f).OnComplete(() =>
                t.DOScale(selectScale, 0.12f));
        }
    }

    public void Cancel() => Play(sCancel);

    public void SelectIndexExternal(int i) => Select(i, instant: false);

    // ===== Núcleo =====
    void Select(int newIndex, bool instant = false, bool playSound = true)
    {
        if (!Valid(newIndex)) return;
        if (newIndex == index && !instant) return;

        KillTweens();

        if (Valid(index))
            _tw.Add(items[index].transform.DOScale(normalScale, snapDuration).SetEase(scaleEase));

        index = newIndex;

        _tw.Add(items[index].transform.DOScale(selectScale, snapDuration).SetEase(scaleEase));

        // mover focusFrame
        if (focusFrame)
        {
            var rt = (RectTransform)items[index].transform;
            var world = rt.TransformPoint(rt.rect.center);
            var local = focusFrame.parent.InverseTransformPoint(world);
            if (instant) focusFrame.anchoredPosition = local;
            else _tw.Add(focusFrame.DOAnchorPos(local, snapDuration).SetEase(snapEase));
        }

        for (int k = 0; k < items.Count; k++)
        {
            FadeButton(k, k == index ? alphaSelected : alphaUnselected);
        }

        // snap del scroll (robusto con anchoredPosition)
        SnapTo(items[index].transform as RectTransform, instant ? 0f : snapDuration);

        if (playSound) Play(sMove);

        EventSystem.current?.SetSelectedGameObject(items[index].gameObject);
        onSelectionChanged?.Invoke(index);
    }

    void SnapTo(RectTransform item, float dur)
    {
        if (!scroll || !content || !item) return;
        var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;

        if (orientation == Orientation.Horizontal)
        {
            float contentW = content.rect.width;
            float viewW = viewport.rect.width;
            var world = item.TransformPoint(item.rect.center);
            var cPoint = content.InverseTransformPoint(world);
            float targetX = -(cPoint.x - viewW * 0.5f);
            float minX = -(contentW - viewW);
            float maxX = 0f;
            float x = Mathf.Clamp(targetX, minX, maxX);

            if (dur <= 0f) content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
            else _tw.Add(content.DOAnchorPosX(x, dur).SetEase(snapEase));
        }
        else
        {
            float contentH = content.rect.height;
            float viewH = viewport.rect.height;
            var world = item.TransformPoint(item.rect.center);
            var cPoint = content.InverseTransformPoint(world);
            float targetY = -(cPoint.y - viewH * 0.5f);
            float minY = -(contentH - viewH);
            float maxY = 0f;
            float y = Mathf.Clamp(targetY, minY, maxY);

            if (dur <= 0f) content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            else _tw.Add(content.DOAnchorPosY(y, dur).SetEase(snapEase));
        }
    }

    public void ForceRefreshSelection(bool instant = true, bool playSound = false)
    {
        if (items == null || items.Count == 0) return;
        int safe = Mathf.Clamp(index, 0, items.Count - 1);
        Select(safe, instant, playSound);
    }

    // ===== Util =====
    bool Valid(int i) => (i >= 0 && i < items.Count);
    void Play(AudioClip c)
    {
        if (c == null) return;

        if (SoundManager.Instance)
        {
            SoundManager.Instance.PlaySFX(c);
            return;
        }

        if (uiAudio) uiAudio.PlayOneShot(c);
    }
    void KillTweens() { foreach (var t in _tw) if (t.IsActive()) t.Kill(false); _tw.Clear(); }

    void FadeButton(int i, float target)
    {
        if (!Valid(i) || !items[i]) return;
        var cg = items[i].GetComponent<CanvasGroup>();
        if (!cg) return;
        cg.DOKill();
        cg.DOFade(target, fadeDuration);
    }
}
