using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDragToWorldFruta))]
[RequireComponent(typeof(Image))]
public class FrutaUIButtonHighlight : MonoBehaviour
{
    // ─────────────────────────────
    // REGISTRO GLOBAL POR ACCIÓN
    // ─────────────────────────────
    static readonly Dictionary<FrutaAccionTipo, List<FrutaUIButtonHighlight>> registry
        = new Dictionary<FrutaAccionTipo, List<FrutaUIButtonHighlight>>();

    // Qué tipos están actualmente "encendidos"
    static readonly HashSet<FrutaAccionTipo> highlightedTypes
        = new HashSet<FrutaAccionTipo>();

    private UIDragToWorldFruta drag;
    private Image img;
    private Coroutine blinkCR;

    [Header("Colores")]
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    [Header("Animación")]
    public float pulseSpeed = 4f;

    public FrutaAccionTipo TipoAccion => drag ? drag.tipoAccion : FrutaAccionTipo.Ninguno;

    void Awake()
    {
        drag = GetComponent<UIDragToWorldFruta>();
        img = GetComponent<Image>();

        if (img != null)
            img.color = normalColor;
    }

    void OnEnable()
    {
        Register();
        // 👇 Aplicar estado actual al habilitarse
        ApplyCurrentState();
    }

    void OnDisable()
    {
        Unregister();
        StopBlink();
        ResetColor();
    }

    void Register()
    {
        if (drag == null) return;
        var tipo = drag.tipoAccion;
        if (!registry.TryGetValue(tipo, out var list))
        {
            list = new List<FrutaUIButtonHighlight>();
            registry.Add(tipo, list);
        }
        if (!list.Contains(this))
            list.Add(this);
    }

    void Unregister()
    {
        if (drag == null) return;
        var tipo = drag.tipoAccion;
        if (registry.TryGetValue(tipo, out var list))
        {
            list.Remove(this);
        }
    }

    void ApplyCurrentState()
    {
        // Si mi tipo está marcado como activo globalmente → parpadeo
        if (highlightedTypes.Contains(TipoAccion))
            SetLocalHighlight(true);
        else
            SetLocalHighlight(false);
    }

    // ─────────────────────────────
    // API ESTÁTICA PARA CONTROLADORES
    // ─────────────────────────────

    public static void ClearAll()
    {
        highlightedTypes.Clear();

        foreach (var kv in registry)
            foreach (var h in kv.Value)
                h.SetLocalHighlight(false);
    }

    public static void SetHighlight(FrutaAccionTipo tipo, bool on)
    {
        if (on) highlightedTypes.Add(tipo);
        else highlightedTypes.Remove(tipo);

        if (!registry.TryGetValue(tipo, out var list)) return;
        foreach (var h in list)
            h.SetLocalHighlight(on);
    }

    public static void HighlightOnly(params FrutaAccionTipo[] tiposOn)
    {
        // Actualizar set global
        highlightedTypes.Clear();
        if (tiposOn != null)
        {
            foreach (var t in tiposOn)
                highlightedTypes.Add(t);
        }

        // Apagar todos los botones
        foreach (var kv in registry)
            foreach (var h in kv.Value)
                h.SetLocalHighlight(false);

        // Encender solo los tipos marcados
        if (tiposOn == null) return;
        foreach (var t in tiposOn)
        {
            if (registry.TryGetValue(t, out var list))
                foreach (var h in list)
                    h.SetLocalHighlight(true);
        }
    }

    // ─────────────────────────────
    // IMPLEMENTACIÓN LOCAL
    // ─────────────────────────────

    void SetLocalHighlight(bool on)
    {
        if (on)
        {
            if (blinkCR == null)
                blinkCR = StartCoroutine(BlinkRoutine());
        }
        else
        {
            StopBlink();
            ResetColor();
        }
    }

    void StopBlink()
    {
        if (blinkCR != null)
        {
            StopCoroutine(blinkCR);
            blinkCR = null;
        }
    }

    void ResetColor()
    {
        if (img != null)
            img.color = normalColor;
    }

    IEnumerator BlinkRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * pulseSpeed;
            float f = 0.5f + 0.5f * Mathf.Sin(t);
            if (img != null)
                img.color = Color.Lerp(normalColor, highlightColor, f);
            yield return null;
        }
    }
}
