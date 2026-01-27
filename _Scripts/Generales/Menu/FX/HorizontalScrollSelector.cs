using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HorizontalScrollSelector : MonoBehaviour
{
    [Header("Refs")]
    public ScrollRect scroll;                  // ScrollRect horizontal
    public RectTransform content;              // Content del ScrollRect
    public RectTransform focusFrame;           // Marco/selector opcional
    public List<Button> items;                 // Vacío = auto con Buttons hijos de 'content'

    [Header("Animación")]
    public float selectScale = 1.08f;
    public float normalScale = 1f;
    public float snapDuration = 0.18f;
    public Ease snapEase = Ease.OutCubic;
    public Ease scaleEase = Ease.OutQuad;

    [Header("Audio")]
    public AudioSource uiAudio;
    public AudioClip sMove;
    public AudioClip sSubmit;
    public AudioClip sCancel;

    [Header("Opciones")]
    public bool wrap = true;
    public int startIndex = 0;

    int index = -1;
    readonly List<Tween> _tw = new();

    void Reset()
    {
        if (!scroll) scroll = GetComponentInChildren<ScrollRect>(true);
        if (!content && scroll) content = scroll.content;
    }

    void Awake()
    {
        if (!content && scroll) content = scroll.content;
        if (items == null || items.Count == 0 && content)
            items = new List<Button>(content.GetComponentsInChildren<Button>(true));
    }

    void OnEnable()
    {
        if (items.Count > 0)
        {
            startIndex = Mathf.Clamp(startIndex, 0, items.Count - 1);
            Select(startIndex, instant: true, playSound: false);
        }
    }

    void OnDisable() => KillTweens();

    // Botones UI en pantalla
    public void OnArrowRight() => Next();
    public void OnArrowLeft() => Prev();
    public void OnSubmit() => Submit();
    public void OnCancel() => Cancel();

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
    }

    public void Cancel() => Play(sCancel);

    // Para hover con mouse (WebGL)
    public void SelectIndexExternal(int newIndex) => Select(newIndex, instant: false);

    void Select(int newIndex, bool instant = false, bool playSound = true)
    {
        if (!Valid(newIndex)) return;
        if (newIndex == index && !instant) return;

        KillTweens();

        // des-animar anterior
        if (Valid(index))
            _tw.Add(items[index].transform.DOScale(normalScale, snapDuration).SetEase(scaleEase));

        index = newIndex;

        // animar seleccionado
        _tw.Add(items[index].transform.DOScale(selectScale, snapDuration).SetEase(scaleEase));

        // mover marco
        if (focusFrame)
        {
            var target = LocalCenterOf(items[index].transform as RectTransform, focusFrame.parent as RectTransform);
            if (instant) focusFrame.anchoredPosition = target;
            else _tw.Add(focusFrame.DOAnchorPos(target, snapDuration).SetEase(snapEase));
        }

        // snap scroll
        SnapTo(items[index].transform as RectTransform, instant ? 0f : snapDuration);

        if (playSound) Play(sMove);

        // focus para gamepad/teclado
        EventSystem.current?.SetSelectedGameObject(items[index].gameObject);
    }

    void SnapTo(RectTransform item, float duration)
    {
        if (!scroll || !content || !item) return;
        var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;

        float contentW = content.rect.width;
        float viewW = viewport.rect.width;

        // Centro del ítem en espacio del Content
        Vector3 worldCenter = item.TransformPoint(item.rect.center);
        Vector2 centerInContent = content.InverseTransformPoint(worldCenter);

        // Queremos ese centro en el centro del viewport
        float targetContentX = -(centerInContent.x - viewW * 0.5f);

        // Limitar dentro de bordes
        float minX = -(contentW - viewW);
        float maxX = 0f;
        float clamped = Mathf.Clamp(targetContentX, minX, maxX);

        if (duration <= 0f)
            content.anchoredPosition = new Vector2(clamped, content.anchoredPosition.y);
        else
            _tw.Add(content.DOAnchorPosX(clamped, duration).SetEase(snapEase));
    }

    Vector2 LocalCenterOf(RectTransform item, RectTransform refParent)
    {
        Vector3 world = item.TransformPoint(item.rect.center);
        return refParent.InverseTransformPoint(world);
    }

    bool Valid(int i) => (i >= 0 && i < items.Count);

    void Play(AudioClip c) { if (uiAudio && c) uiAudio.PlayOneShot(c); }

    void KillTweens()
    {
        foreach (var t in _tw) if (t.IsActive()) t.Kill(false);
        _tw.Clear();
    }
}
