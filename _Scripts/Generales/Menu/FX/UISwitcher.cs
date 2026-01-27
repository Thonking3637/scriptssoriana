using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class UISwitcher : MonoBehaviour
{
    [Tooltip("Si no logramos deducir el panel origen desde el botón presionado, usamos este como respaldo.")]
    public GameObject fallbackFromPanel;

    [Tooltip("Al entrar al panel destino: enfocar el primer botón y resetear scroll/selector.")]
    public bool seleccionarPrimero = true;

    public UISwitcher uiSwitcher;

    [Header("Back / Historial")]
    public bool useHistory = true;
    public GameObject defaultPanel;
    private Stack<GameObject> history = new Stack<GameObject>();

    [Header("Estado actual")]
    public GameObject currentPanel;
    public List<GameObject> allPanels = new List<GameObject>();

    public void SwitchTo(GameObject toPanel)
    {
        var fromPanel = GetCurrentPanel();
        SwitchInternal(fromPanel, toPanel, seleccionarPrimero);
        currentPanel = toPanel;
        AfterShow(toPanel, seleccionarPrimero);
    }


    // ====== NUEVO: navegar guardando historial ======
    public void SwitchToPush(GameObject toPanel)
    {
        var fromPanel = GetCurrentPanel();
        if (useHistory && fromPanel && fromPanel != toPanel)
            history.Push(fromPanel);

        SwitchInternal(fromPanel, toPanel, seleccionarPrimero);
        currentPanel = toPanel;
        AfterShow(toPanel, seleccionarPrimero);
    }

    // ====== NUEVO: botón Atrás ======
    public void Back()
    {
        GameObject target = null;
        if (useHistory && history.Count > 0) target = history.Pop();
        else if (defaultPanel) target = defaultPanel;
        if (!target) return;

        var from = GetCurrentPanel();
        SwitchInternal(from, target, seleccionarPrimero);
        currentPanel = target;
        AfterShow(target, seleccionarPrimero);
    }

    // ----- Internos -----
    GameObject DetectFromPanel()
    {
        // El botón presionado queda como currentSelected en EventSystem
        var sender = EventSystem.current?.currentSelectedGameObject;
        if (sender != null)
        {
            var t = sender.transform;
            while (t != null)
            {
                // Consideramos panel si tiene MenuPanelFX o CanvasGroup
                if (t.GetComponent<MenuPanelFX>() || t.GetComponent<CanvasGroup>())
                    return t.gameObject;
                t = t.parent;
            }
        }
        return fallbackFromPanel; // por si falla (touch sin focus, etc.)
    }

    void SwitchInternal(GameObject fromPanel, GameObject toPanel, bool selectFirst)
    {
        // Ocultar anterior
        if (fromPanel)
        {
            var fxFrom = fromPanel.GetComponent<MenuPanelFX>();
            if (fxFrom) fxFrom.Hide();
            else fromPanel.SetActive(false);
        }

        // Mostrar destino
        if (toPanel)
        {
            var fxTo = toPanel.GetComponent<MenuPanelFX>();
            if (fxTo) fxTo.Show();
            else toPanel.SetActive(true);

            if (selectFirst)
            {
                // a) foco al primer botón
                var firstBtn = toPanel.GetComponentInChildren<Button>(true);
                if (firstBtn) EventSystem.current?.SetSelectedGameObject(firstBtn.gameObject);

                // b) reset de scroll
                var sr = toPanel.GetComponentInChildren<ScrollRect>(true);
                if (sr)
                {
                    if (sr.horizontal) sr.horizontalNormalizedPosition = 0f;
                    if (sr.vertical) sr.verticalNormalizedPosition = 1f;
                }

                // c) si usas HorizontalScrollSelector, centrar primer ítem
                var horiz = toPanel.GetComponentInChildren<HorizontalScrollSelector>(true);
                if (horiz) horiz.SelectIndexExternal(0);
            }
        }
    }

    void AfterShow(GameObject toPanel, bool selectFirst)
    {
        if (!toPanel) return;

        if (selectFirst)
        {
            // a) foco al primer botón
            var firstBtn = toPanel.GetComponentInChildren<Button>(true);
            if (firstBtn) EventSystem.current?.SetSelectedGameObject(firstBtn.gameObject);

            // b) reset de scroll
            var sr = toPanel.GetComponentInChildren<ScrollRect>(true);
            if (sr)
            {
                if (sr.horizontal) sr.horizontalNormalizedPosition = 0f;
                if (sr.vertical) sr.verticalNormalizedPosition = 1f;
            }
        }

        // c) reset de selectores (horizontal o unificado) + refresco de preview
        var horiz = toPanel.GetComponentInChildren<HorizontalScrollSelector>(true);
        if (horiz) horiz.SelectIndexExternal(0);

        var unified = toPanel.GetComponentInChildren<UnifiedScrollSelector>(true);
        if (unified)
        {
            unified.SelectIndexExternal(0);
            unified.ForceRefreshSelection(true); // re-emite onSelectionChanged
        }
    }

    GameObject GetCurrentPanel()
    {
        // 1) Si ya lo sabemos y sigue activo:
        if (currentPanel && currentPanel.activeInHierarchy) return currentPanel;

        // 2) Intenta deducirlo por EventSystem (botón dentro del panel)
        var fromByEvent = DetectFromPanel();
        if (fromByEvent) return fromByEvent;

        // 3) Busca entre los paneles conocidos cuál está activo
        if (allPanels != null)
        {
            foreach (var p in allPanels)
                if (p && p.activeInHierarchy) return p;
        }
        // 4) Último recurso
        return fallbackFromPanel;
    }

}
