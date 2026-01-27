using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScrollButtons : MonoBehaviour
{
    public enum Orientation { Auto, Horizontal, Vertical }

    [Header("Refs")]
    public ScrollRect scroll;
    public RectTransform viewport;     // = scroll.viewport
    public RectTransform content;      // = scroll.content

    [Tooltip("Auto intenta deducir por el ScrollRect; puedes forzarlo a Horizontal o Vertical.")]
    public Orientation orientation = Orientation.Auto;

    [Header("Layout (opcional, auto-detecta si están en el Content)")]
    public HorizontalLayoutGroup hlg;
    public VerticalLayoutGroup vlg;
    public GridLayoutGroup glg;        // soportado si el grid tiene cellSize fijo (sin tamaños variables por item)

    [Header("Tween")]
    public float duration = 0.25f;

    [Header("Navegación por índice")]
    public int currentIndex = 0;
    public bool centerOnStart = true;

    private Coroutine tweenCo;

    void Awake()
    {
        if (!scroll) scroll = GetComponentInChildren<ScrollRect>(true);
        if (!viewport && scroll) viewport = scroll.viewport;
        if (!content && scroll) content = scroll.content;

        if (!hlg && content) hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (!vlg && content) vlg = content.GetComponent<VerticalLayoutGroup>();
        if (!glg && content) glg = content.GetComponent<GridLayoutGroup>();

        // Detección automática de orientación
        if (orientation == Orientation.Auto && scroll)
        {
            if (scroll.horizontal && !scroll.vertical) orientation = Orientation.Horizontal;
            else if (scroll.vertical && !scroll.horizontal) orientation = Orientation.Vertical;
            else
            {
                // Si ambos están activos, prioriza el layout presente
                if (hlg || (glg && (glg.constraint == GridLayoutGroup.Constraint.FixedRowCount)))
                    orientation = Orientation.Horizontal;
                else
                    orientation = Orientation.Vertical;
            }
        }
    }

    void Start()
    {
        Canvas.ForceUpdateCanvases();
        if (centerOnStart) SnapToIndex(currentIndex, instant: true);
    }

    // Botones horizontales
    public void Left() { if (orientation == Orientation.Horizontal) SetIndex(Mathf.Max(0, currentIndex - 1)); }
    public void Right() { if (orientation == Orientation.Horizontal) SetIndex(Mathf.Min(GetItemCount() - 1, currentIndex + 1)); }

    // Botones verticales
    public void Up() { if (orientation == Orientation.Vertical) SetIndex(Mathf.Max(0, currentIndex - 1)); }
    public void Down() { if (orientation == Orientation.Vertical) SetIndex(Mathf.Min(GetItemCount() - 1, currentIndex + 1)); }

    public void SetIndex(int index)
    {
        index = Mathf.Clamp(index, 0, Mathf.Max(0, GetItemCount() - 1));
        currentIndex = index;
        SnapToIndex(index, instant: false);
    }

    // ===================== core snap =====================
    void SnapToIndex(int index, bool instant)
    {
        int count = GetItemCount();
        if (count == 0 || index < 0 || index >= count) return;

        if (orientation == Orientation.Horizontal)
        {
            float contentLen = GetContentLengthHorizontal();
            float viewLen = viewport.rect.width;

            if (contentLen <= viewLen) { SetNormalizedX(0f, instant); return; }

            float centerX = GetItemCenterX(index);
            float desiredLeft = centerX - viewLen * 0.5f; // cuánto debe “avanzar” el content

            float maxLeft = contentLen - viewLen;
            desiredLeft = Mathf.Clamp(desiredLeft, 0f, maxLeft);

            float normalized = maxLeft <= 0f ? 0f : desiredLeft / maxLeft;
            SetNormalizedX(normalized, instant);
        }
        else // Vertical
        {
            float contentLen = GetContentLengthVertical();
            float viewLen = viewport.rect.height;

            if (contentLen <= viewLen) { SetNormalizedY(1f, instant); return; } // en vertical 1 = top

            float centerY = GetItemCenterY(index);
            float desiredTop = centerY - viewLen * 0.5f;

            float maxTop = contentLen - viewLen;
            desiredTop = Mathf.Clamp(desiredTop, 0f, maxTop);

            // Nota: verticalNormalizedPosition: 1 = top, 0 = bottom
            float normalized = maxTop <= 0f ? 1f : 1f - (desiredTop / maxTop);
            SetNormalizedY(normalized, instant);
        }
    }

    // ===================== medir HORIZONTAL =====================
    float GetContentLengthHorizontal()
    {
        if (glg && glg.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            // Grid horizontal (varias filas fijas, scroll horizontal)
            int n = GetItemCount();
            int rows = Mathf.Max(1, glg.constraintCount);
            int cols = Mathf.CeilToInt((float)n / rows);

            float total = glg.padding.left + glg.padding.right;
            total += cols * glg.cellSize.x + (cols - 1) * glg.spacing.x;
            return total;
        }

        if (hlg)
        {
            float w = hlg.padding.left + hlg.padding.right;
            int n = GetItemCount();
            for (int i = 0; i < n; i++)
            {
                var rt = content.GetChild(i) as RectTransform;
                w += PreferredWidth(rt);
                if (i < n - 1) w += hlg.spacing;
            }
            return w;
        }

        // Fallback: suma widths actuales
        float f = 0f;
        int c = GetItemCount();
        for (int i = 0; i < c; i++)
        {
            var rt = content.GetChild(i) as RectTransform;
            f += rt.rect.width;
        }
        return f;
    }

    float GetItemCenterX(int index)
    {
        if (glg && glg.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            int n = GetItemCount();
            int rows = Mathf.Max(1, glg.constraintCount);
            int col = index / rows;

            float x = glg.padding.left;
            x += col * (glg.cellSize.x + glg.spacing.x);
            x += glg.cellSize.x * 0.5f;
            return x;
        }

        if (hlg)
        {
            float x = hlg.padding.left;
            for (int i = 0; i < index; i++)
            {
                var rti = content.GetChild(i) as RectTransform;
                x += PreferredWidth(rti) + hlg.spacing;
            }
            var rt = content.GetChild(index) as RectTransform;
            x += PreferredWidth(rt) * 0.5f;
            return x;
        }

        // Fallback
        float acc = 0f;
        for (int i = 0; i < index; i++)
        {
            var rti = content.GetChild(i) as RectTransform;
            acc += rti.rect.width;
        }
        var r = content.GetChild(index) as RectTransform;
        acc += r.rect.width * 0.5f;
        return acc;
    }

    // ===================== medir VERTICAL =====================
    float GetContentLengthVertical()
    {
        if (glg && glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            // Grid vertical (varias columnas fijas, scroll vertical)
            int n = GetItemCount();
            int cols = Mathf.Max(1, glg.constraintCount);
            int rows = Mathf.CeilToInt((float)n / cols);

            float total = glg.padding.top + glg.padding.bottom;
            total += rows * glg.cellSize.y + (rows - 1) * glg.spacing.y;
            return total;
        }

        if (vlg)
        {
            float h = vlg.padding.top + vlg.padding.bottom;
            int n = GetItemCount();
            for (int i = 0; i < n; i++)
            {
                var rt = content.GetChild(i) as RectTransform;
                h += PreferredHeight(rt);
                if (i < n - 1) h += vlg.spacing;
            }
            return h;
        }

        // Fallback
        float f = 0f;
        int c = GetItemCount();
        for (int i = 0; i < c; i++)
        {
            var rt = content.GetChild(i) as RectTransform;
            f += rt.rect.height;
        }
        return f;
    }

    float GetItemCenterY(int index)
    {
        if (glg && glg.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            int n = GetItemCount();
            int cols = Mathf.Max(1, glg.constraintCount);
            int row = index / cols;

            float y = glg.padding.top;
            y += row * (glg.cellSize.y + glg.spacing.y);
            y += glg.cellSize.y * 0.5f;
            return y;
        }

        if (vlg)
        {
            float y = vlg.padding.top;
            for (int i = 0; i < index; i++)
            {
                var rti = content.GetChild(i) as RectTransform;
                y += PreferredHeight(rti) + vlg.spacing;
            }
            var rt = content.GetChild(index) as RectTransform;
            y += PreferredHeight(rt) * 0.5f;
            return y;
        }

        // Fallback
        float acc = 0f;
        for (int i = 0; i < index; i++)
        {
            var rti = content.GetChild(i) as RectTransform;
            acc += rti.rect.height;
        }
        var r = content.GetChild(index) as RectTransform;
        acc += r.rect.height * 0.5f;
        return acc;
    }

    // ===================== helpers =====================
    int GetItemCount() => content ? content.childCount : 0;

    float PreferredWidth(RectTransform rt)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredWidth > 0f) return le.preferredWidth;
        return rt.rect.width;
    }

    float PreferredHeight(RectTransform rt)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredHeight > 0f) return le.preferredHeight;
        return rt.rect.height;
    }

    void SetNormalizedX(float x, bool instant)
    {
        if (tweenCo != null) StopCoroutine(tweenCo);
        if (instant) scroll.horizontalNormalizedPosition = x;
        else tweenCo = StartCoroutine(TweenNormX(x));
    }

    void SetNormalizedY(float y, bool instant)
    {
        if (tweenCo != null) StopCoroutine(tweenCo);
        if (instant) scroll.verticalNormalizedPosition = y;
        else tweenCo = StartCoroutine(TweenNormY(y));
    }

    IEnumerator TweenNormX(float target)
    {
        float start = scroll.horizontalNormalizedPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            scroll.horizontalNormalizedPosition = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        scroll.horizontalNormalizedPosition = target;
        tweenCo = null;
    }

    IEnumerator TweenNormY(float target)
    {
        float start = scroll.verticalNormalizedPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            scroll.verticalNormalizedPosition = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        scroll.verticalNormalizedPosition = target;
        tweenCo = null;
    }

    // Si agregas/eliminas dinámicamente:
    public void RebuildAndClampIndex()
    {
        Canvas.ForceUpdateCanvases();
        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, GetItemCount() - 1));
        SnapToIndex(currentIndex, instant: true);
    }
}
