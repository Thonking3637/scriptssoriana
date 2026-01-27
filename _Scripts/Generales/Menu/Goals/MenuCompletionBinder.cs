// MenuCompletionBinder.cs
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// MenuCompletionBinder - Actualiza los badges de completado en el menú.
/// 
/// IMPORTANTE: Este script puede estar en un panel que se activa/desactiva.
/// Por eso usa verificaciones antes de iniciar coroutines.
/// </summary>
public class MenuCompletionBinder : MonoBehaviour
{
    [SerializeField] public UnifiedScrollSelector unified;

    private Coroutine _waitCr;
    private bool _pendingRefresh; // Flag para refrescar cuando se active

    void OnEnable()
    {
        CompletionService.OnProgressChanged += OnProgressChanged;

        // Si había un refresh pendiente, hacerlo ahora
        if (_pendingRefresh)
        {
            _pendingRefresh = false;
            RefreshAll();
        }
        else
        {
            // Refresh inicial
            RefreshAll();
        }
    }

    void OnDisable()
    {
        CompletionService.OnProgressChanged -= OnProgressChanged;

        // Cancelar coroutine si existe
        if (_waitCr != null)
        {
            StopCoroutine(_waitCr);
            _waitCr = null;
        }
    }

    /// <summary>
    /// Handler del evento - verifica si puede refrescar o marca como pendiente
    /// </summary>
    private void OnProgressChanged()
    {
        // Si el objeto está inactivo, marcar como pendiente para cuando se active
        if (!gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }

        RefreshAll();
    }

    /// <summary>
    /// Refresca todos los badges. Seguro llamar incluso si está inactivo.
    /// </summary>
    public void RefreshAll()
    {
        // Si no estamos activos, marcar pendiente y salir
        if (!gameObject.activeInHierarchy)
        {
            _pendingRefresh = true;
            return;
        }

        // Si ya está listo, refrescar inmediatamente
        if (IsReady())
        {
            DoRefreshNow();
            return;
        }

        // Si no está listo, esperar con coroutine (solo si estamos activos)
        if (_waitCr == null)
        {
            _waitCr = StartCoroutine(WaitAndRefresh());
        }
    }

    private bool IsReady()
    {
        return unified != null && unified.items != null && unified.items.Count > 0;
    }

    private IEnumerator WaitAndRefresh()
    {
        // Espera a que el menú termine de construir/poblar items
        float timeout = 3f; // Máximo 3 segundos de espera
        float elapsed = 0f;

        while (!IsReady() && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Un frame extra para asegurar layout/instancias ya activas
        yield return null;

        if (IsReady())
        {
            DoRefreshNow();
        }
        else
        {
            Debug.LogWarning("[MenuCompletionBinder] Timeout esperando items del selector.");
        }

        _waitCr = null;
    }

    private void DoRefreshNow()
    {
        if (unified == null || unified.items == null) return;

        foreach (var btn in unified.items.Where(b => b != null))
        {
            var badge = btn.GetComponentInChildren<LevelItemCompletionBadge>(true);
            if (badge != null)
            {
                badge.Refresh();
            }
        }

        // Forzar repaint de UI
        Canvas.ForceUpdateCanvases();
    }
}