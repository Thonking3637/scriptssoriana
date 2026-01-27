// LevelChooseController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelChooseController : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Lista de selectores posibles (pueden estar en paneles distintos). El controlador usará el que esté activo en pantalla.")]
    public List<UnifiedScrollSelector> selectors = new();

    [Tooltip("Botón Elegir (opcional). Si es null, el launch se puede invocar por código llamando a ChooseCurrent().")]
    public Button chooseButton;

    [Tooltip("MenuManager de tu escena (si no lo pones, se busca automáticamente).")]
    public MenuManager menuManager;

    [Header("Cambio de panel por índice (fallback si no hay escena)")]
    public List<GameObject> panelByIndex = new();   // deja null donde no aplique
    public bool preferMetaOverIndex = true;

    private UnifiedScrollSelector activeSelector; // el selector “vigente”
    private LevelItemMeta currentMeta;

    public UISwitcher uiSwitcher;
    private Button currentButton;
    void Awake()
    {
        if (!menuManager) menuManager = FindObjectOfType<MenuManager>();
        // Suscribir al cambio de selección de todos los selectores
        foreach (var s in selectors)
            if (s) s.onSelectionChanged.AddListener(_ => OnAnySelectionChanged());

        if (chooseButton) chooseButton.onClick.AddListener(ChooseCurrent);
    }

    void OnDestroy()
    {
        foreach (var s in selectors)
            if (s) s.onSelectionChanged.RemoveListener(_ => OnAnySelectionChanged());
        if (chooseButton) chooseButton.onClick.RemoveListener(ChooseCurrent);
    }

    void OnEnable()
    {
        RefreshActiveSelector();
        UpdateCurrentMetaAndButton();
    }

    // Llamado por los selectores cuando cambia la selección
    void OnAnySelectionChanged()
    {
        RefreshActiveSelector();
        UpdateCurrentMetaAndButton();
    }

    // Elige como activo el primer selector que esté visible/activo en jerarquía
    void RefreshActiveSelector()
    {
        // Si el actual ya no está activo, busca otro
        if (activeSelector == null || !activeSelector.gameObject.activeInHierarchy)
        {
            activeSelector = null;
            foreach (var s in selectors)
            {
                if (s && s.gameObject.activeInHierarchy)
                {
                    activeSelector = s;
                    break;
                }
            }
        }
    }

    void UpdateCurrentMetaAndButton()
    {
        currentMeta = null;
        currentButton = null;

        if (activeSelector && activeSelector.CurrentIndex >= 0 &&
            activeSelector.CurrentIndex < activeSelector.items.Count)
        {
            currentButton = activeSelector.items[activeSelector.CurrentIndex];
            if (currentButton) currentMeta = currentButton.GetComponent<LevelItemMeta>();
        }

        int idx = (activeSelector ? activeSelector.CurrentIndex : -1);
        bool hasIndexPanel = uiSwitcher && idx >= 0 && idx < panelByIndex.Count && panelByIndex[idx] != null;
        bool hasMetaPanel = (currentMeta && currentMeta.switchToPanel);

        if (chooseButton)
            chooseButton.interactable = (currentButton != null) && (currentMeta == null || currentMeta.canLaunch || hasMetaPanel || hasIndexPanel);
    }

    // Puedes llamarlo desde el botón "Elegir" o desde cualquier otro script
    public void ChooseCurrent()
    {
        if (!currentButton) return;

        // 1) Si hay escena/actividad configurada, usa tu flujo actual
        if (menuManager && currentMeta && currentMeta.canLaunch)
        {
            if (currentMeta.launchType == LevelLaunchType.SceneByName &&
                !string.IsNullOrEmpty(currentMeta.sceneName))
            { menuManager.LoadLevel(currentMeta.sceneName); return; }

            if (currentMeta.launchType == LevelLaunchType.ActivityIndex)
            { menuManager.StartActivity(currentMeta.activityIndex, currentMeta.sceneName); return; }
        }

        // 2) Cambio de panel explícito desde el meta (si usas LevelItemMeta.switchToPanel)
        if (uiSwitcher && currentMeta && preferMetaOverIndex && currentMeta.switchToPanel)
        {
            uiSwitcher.fallbackFromPanel = FindPanelRoot(currentButton.gameObject);
            uiSwitcher.SwitchTo(currentMeta.switchToPanel);
            return;
        }

        // 3) Cambio de panel por ÍNDICE (tabla panelByIndex)
        int idx = (activeSelector ? activeSelector.CurrentIndex : -1);
        if (uiSwitcher && idx >= 0 && idx < panelByIndex.Count && panelByIndex[idx] != null)
        {
            uiSwitcher.fallbackFromPanel = FindPanelRoot(currentButton.gameObject);
            uiSwitcher.SwitchTo(panelByIndex[idx]);
            return;
        }

        // 4) Fallback general: imitar el botón del scroll (ejecuta su onClick ya cableado)
        currentButton.onClick.Invoke();
    }

    // Ayudín para detectar el panel contenedor como "from"
    GameObject FindPanelRoot(GameObject go)
    {
        var t = go.transform;
        while (t != null)
        {
            if (t.GetComponent<MenuPanelFX>() || t.GetComponent<CanvasGroup>())
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    // ✅ Útil por la limitación de OnClick (un parámetro): pásale el panel destino y este localiza su selector
    public void ActivateFor(GameObject panel)
    {
        UnifiedScrollSelector found = panel ? panel.GetComponentInChildren<UnifiedScrollSelector>(true) : null;
        if (found && !selectors.Contains(found)) selectors.Add(found);
        activeSelector = found;
        UpdateCurrentMetaAndButton();
    }
}
