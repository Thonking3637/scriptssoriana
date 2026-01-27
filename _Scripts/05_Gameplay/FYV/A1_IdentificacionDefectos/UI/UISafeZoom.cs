using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Zoom & pan seguro para UI (Image) dentro de un contenedor (viewport).
/// - Doble click / doble tap: zoom in/out rápido.
/// - Pinch (2 dedos): zoom continuo (sin Input.touches).
/// - Rueda del mouse: zoom.
/// - Drag: pan cuando zoom > 1.
/// Todo clamped a los límites del viewport.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UISafeZoom : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IScrollHandler
{
    [Header("Refs")]
    [Tooltip("Rect del viewport (contenedor). Usualmente el panel del afiche).")]
    public RectTransform viewport; 
    [Tooltip("Rect de contenido (el que se escala y se hace pan). Si es null, usa este mismo.")]
    public RectTransform content;

    [Header("Zoom")]
    public float minScale = 1f;
    public float maxScale = 3f;
    public float wheelZoomSpeed = 0.1f;      // PC: rueda
    public float pinchZoomSpeed = 0.005f;    // pinch: deltaPx * speed
    public float doubleTapScale = 2f;        // escala a alternar con doble tap/click
    public float doubleTapMaxDelay = 0.25f;  // seg entre taps
    public float doubleTapMaxMove = 25f;     // px permitido entre taps

    [Header("Pan")]
    public bool dragToPan = true;            // arrastrar cuando escalado
    public bool inertial = false;            // inercia pan (opcional)
    public float inertiaDamp = 10f;

    // Pan
    private Vector2 lastPointerLocalPos;
    private Vector2 velocity;

    // Double tap
    private float lastTapTime = -99f;
    private Vector2 lastTapPos;

    // Multi-pointer (pinch)
    private readonly Dictionary<int, Vector2> pointerPos = new();
    private readonly Dictionary<int, Vector2> pointerPrev = new();

    private Canvas _canvas;
    
    public Action<float> OnScaleChanged;
    public Action OnFirstZoomUsed;
    private bool _firstZoomSent = false;
    private float _lastScaleNotified = -1f;
    public float zoomUsedThreshold = 0.05f;

    void Awake()
    {
        if (!content) content = GetComponent<RectTransform>();
        if (!viewport) viewport = content.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();

        ResetZoom();
    }

    void Update()
    {
        // Pinch por pointers: si hay EXACTAMENTE 2 pointers activos, calculamos pinch
        if (pointerPos.Count == 2)
        {
            var ids = GetTwoPointerIds();
            int id0 = ids.Item1;
            int id1 = ids.Item2;

            Vector2 p0 = pointerPos[id0];
            Vector2 p1 = pointerPos[id1];

            Vector2 p0Prev = pointerPrev[id0];
            Vector2 p1Prev = pointerPrev[id1];

            float prevMag = (p0Prev - p1Prev).magnitude;
            float currMag = (p0 - p1).magnitude;
            float delta = currMag - prevMag;

            float targetScale = Mathf.Clamp(content.localScale.x + delta * pinchZoomSpeed, minScale, maxScale);
            ZoomAtScreenPoint((p0 + p1) * 0.5f, targetScale);

            // Actualizar prev para el siguiente frame
            pointerPrev[id0] = p0;
            pointerPrev[id1] = p1;

            // Mientras hay pinch, cortamos inercia de pan
            velocity = Vector2.zero;
            return;
        }

        // Inercia opcional
        if (inertial && velocity != Vector2.zero)
        {
            content.anchoredPosition += velocity * Time.unscaledDeltaTime;
            ClampToViewport();
            velocity = Vector2.Lerp(velocity, Vector2.zero, Time.unscaledDeltaTime * inertiaDamp);
        }
    }

    public void ResetZoom()
    {
        if (!content) return;
        content.localScale = Vector3.one * minScale;
        content.anchoredPosition = Vector2.zero;
        velocity = Vector2.zero;

        pointerPos.Clear();
        pointerPrev.Clear();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Registrar pointer
        pointerPos[eventData.pointerId] = eventData.position;
        pointerPrev[eventData.pointerId] = eventData.position;

        // Doble tap / doble click
        float t = Time.unscaledTime;
        bool withinTime = (t - lastTapTime) <= doubleTapMaxDelay;
        bool withinMove = (eventData.position - lastTapPos).sqrMagnitude <= (doubleTapMaxMove * doubleTapMaxMove);

        if (withinTime && withinMove)
        {
            float target = (Mathf.Abs(content.localScale.x - minScale) < 0.001f)
                ? Mathf.Clamp(doubleTapScale, minScale, maxScale)
                : minScale;

            ZoomAtScreenPoint(eventData.position, target);
            lastTapTime = -99f; // consume
        }
        else
        {
            lastTapTime = t;
            lastTapPos = eventData.position;
        }

        // Guardar punto local para pan (solo si no estamos en pinch)
        if (pointerPos.Count == 1 && dragToPan)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                eventData.position,
                GetEventCamera(eventData),
                out lastPointerLocalPos);

            velocity = Vector2.zero;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPos.Remove(eventData.pointerId);
        pointerPrev.Remove(eventData.pointerId);
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        // Actualizar posición del pointer (para pinch)
        pointerPos[eventData.pointerId] = eventData.position;

        // Si hay pinch activo, no paneamos
        if (pointerPos.Count == 2) return;

        if (!dragToPan) return;
        if (content.localScale.x <= minScale + 0.0001f) return; // no pan si no hay zoom

        // Pan con 1 pointer
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, eventData.position, GetEventCamera(eventData), out localPoint))
            return;

        Vector2 delta = localPoint - lastPointerLocalPos;
        lastPointerLocalPos = localPoint;

        content.anchoredPosition += delta;

        if (inertial)
        {
            // velocidad en “local units/seg”
            velocity = Vector2.Lerp(velocity, delta / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.5f);
        }

        ClampToViewport();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // si no hay inercia, cortamos
        if (!inertial) velocity = Vector2.zero;
    }

    public void OnScroll(PointerEventData eventData)
    {
        // Zoom por rueda (PC/WebGL PC)
        float scroll = eventData.scrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        float targetScale = Mathf.Clamp(content.localScale.x + scroll * wheelZoomSpeed, minScale, maxScale);
        ZoomAtScreenPoint(eventData.position, targetScale);
        velocity = Vector2.zero;
    }

    private void ZoomAtScreenPoint(Vector2 screenPoint, float targetScale)
    {
        if (!viewport || !content) return;

        float prevScale = content.localScale.x;
        if (Mathf.Abs(targetScale - prevScale) < 0.0001f) return;

        Camera cam = (_canvas != null) ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, cam, out Vector2 vpLocalBefore);

        content.localScale = Vector3.one * targetScale;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, cam, out Vector2 vpLocalAfter);

        Vector2 delta = vpLocalAfter - vpLocalBefore;
        content.anchoredPosition -= delta;

        ClampToViewport();

        if (Mathf.Abs(targetScale - minScale) < 0.001f)
            content.anchoredPosition = Vector2.zero;
        
        float currentScale = content.localScale.x;
        
        if (_lastScaleNotified < 0f)
            _lastScaleNotified = currentScale;
        
        if (Mathf.Abs(currentScale - _lastScaleNotified) >= zoomUsedThreshold)
        {
            _lastScaleNotified = currentScale;
            OnScaleChanged?.Invoke(currentScale);

            // Primera vez que el usuario hace zoom REAL
            if (!_firstZoomSent && currentScale > minScale + zoomUsedThreshold)
            {
                _firstZoomSent = true;
                OnFirstZoomUsed?.Invoke();
            }
        }
    }

    private void ClampToViewport()
    {
        if (!viewport || !content) return;

        Vector2 vpSize = viewport.rect.size;
        Vector2 ctSize = content.rect.size * content.localScale.x;

        // límites: si el contenido es más pequeño que el viewport en algún eje, lo centramos
        Vector2 maxOffset = (ctSize - vpSize) * 0.5f;
        Vector2 pos = content.anchoredPosition;

        if (ctSize.x <= vpSize.x) pos.x = 0f;
        else pos.x = Mathf.Clamp(pos.x, -maxOffset.x, maxOffset.x);

        if (ctSize.y <= vpSize.y) pos.y = 0f;
        else pos.y = Mathf.Clamp(pos.y, -maxOffset.y, maxOffset.y);

        content.anchoredPosition = pos;
    }

    private (int, int) GetTwoPointerIds()
    {
        // pointerPos.Count == 2 garantizado aquí
        using var e = pointerPos.Keys.GetEnumerator();
        e.MoveNext(); int a = e.Current;
        e.MoveNext(); int b = e.Current;
        return (a, b);
    }

    private Camera GetEventCamera(PointerEventData e)
    {
        // En Screen Space - Camera, esto suele venir correcto:
        if (e.pressEventCamera != null) return e.pressEventCamera;
        if (_canvas != null) return _canvas.worldCamera;
        return null;
    }
}
