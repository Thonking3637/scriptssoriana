using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TechSheetPanelController: MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button btnOpen;               // bot�n principal (cuadrado)
    [SerializeField] private RectTransform panelTabs;      // contenedor de tabs
    [SerializeField] private Button[] tabButtons;          // 4�5 botones
    [SerializeField] private RectTransform panelSheet;     // panel del afiche
    [SerializeField] private Image sheetImage;             // imagen grande

    [Header("Animaci�n")]
    [SerializeField] private float durTabs = 0.25f;
    [SerializeField] private float durSheet = 0.20f;
    [SerializeField] private Vector2 tabsHidden = new(0, -600);
    [SerializeField] private Vector2 tabsShown = new(0, 0);
    [SerializeField] private Vector2 sheetHidden = new(800, 0);
    [SerializeField] private Vector2 sheetShown = new(0, 0);
    [SerializeField] private Ease easeIn = Ease.InCubic;
    [SerializeField] private Ease easeOut = Ease.OutCubic;

    // Estado por lote
    private Sprite[] lotSprites = new Sprite[5];
    private bool[] lotEnabled = new bool[5];

    // Estado runtime
    private bool tabsVisible = false;
    private bool sheetVisible = false;
    private int selectedTab = -1;

    [SerializeField] private UISafeZoom safeZoom;
    
    public System.Action OnOpened;
    public System.Action OnFirstTabPressed;
    public System.Action OnZoomUsed;

    public bool openedNotified = false;
    public bool firstTabNotified = false;
    public bool zoomNotified = false;

    void Awake()
    {
        if (btnOpen)
        {
            btnOpen.onClick.RemoveAllListeners();
            btnOpen.onClick.AddListener(OnOpenToggle);
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            if (tabButtons[i] == null) continue;
            tabButtons[i].onClick.RemoveAllListeners();
            tabButtons[i].onClick.AddListener(() => OnTabPressed(idx));
        }
        
        if (safeZoom != null)
        {
            safeZoom.OnFirstZoomUsed += () =>
            {
                if (zoomNotified) return;

                zoomNotified = true;
                OnZoomUsed?.Invoke();
            };
        }
        
        InitHidden();
    }

    private void InitHidden()
    {
        if (panelTabs)
        {
            panelTabs.DOKill();
            panelTabs.gameObject.SetActive(false);
            panelTabs.anchoredPosition = tabsHidden;
        }
        if (panelSheet)
        {
            panelSheet.DOKill();
            panelSheet.gameObject.SetActive(false);
            panelSheet.anchoredPosition = sheetHidden;
        }
        if (sheetImage) sheetImage.sprite = null;

        tabsVisible = false;
        sheetVisible = false;
        selectedTab = -1;
    }

    /// <summary>
    /// Configura los botones y sprites del lote actual (4�5).
    /// </summary>
    public void Configure(Sprite[] sprites, bool[] enabled)
    {
        // Copia segura
        for (int i = 0; i < lotSprites.Length; i++)
            lotSprites[i] = (sprites != null && i < sprites.Length) ? sprites[i] : null;

        for (int i = 0; i < lotEnabled.Length; i++)
            lotEnabled[i] = (enabled != null && i < enabled.Length) ? enabled[i] : false;

        // Actualiza visibilidad de tabs (activos o no)
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null)
                tabButtons[i].gameObject.SetActive(i < lotEnabled.Length && lotEnabled[i]);

        // Reset de estado visual
        selectedTab = -1;
        HideSheetImmediate(); // afiche oculto
        if (tabsVisible) OnOpenToggle(); // si estaba abierto, ci�rralo para reiniciar UX limpia
    }

    public void SetOpenButtonInteractable(bool enabled) => btnOpen.interactable = enabled;

    // ===== Interacci�n =====
    private void OnOpenToggle()
    {
        if (!panelTabs) return;

        if (tabsVisible)
        {
            HideSheet();
            panelTabs.DOKill();
            panelTabs.DOAnchorPos(tabsHidden, durTabs).SetEase(easeIn)
                     .OnComplete(() => panelTabs.gameObject.SetActive(false));
            tabsVisible = false;
            selectedTab = -1;
        }
        else
        {
            panelTabs.gameObject.SetActive(true);
            panelTabs.DOKill();
            panelTabs.anchoredPosition = tabsHidden;
            panelTabs.DOAnchorPos(tabsShown, durTabs).SetEase(easeOut);
            tabsVisible = true;

            HideSheetImmediate();
            selectedTab = -1;

            if (!openedNotified)
            {
                openedNotified = true;
                OnOpened?.Invoke();
            }
        }
    }

    private void OnTabPressed(int idx)
    {
        if (idx < 0 || idx >= lotEnabled.Length || !lotEnabled[idx]) return;

        if (!firstTabNotified)
        {
            firstTabNotified = true;
            OnFirstTabPressed?.Invoke();
        }

        if (!sheetVisible)
        {
            selectedTab = idx;
            SetSheetSprite(idx);
            ShowSheet();
            return;
        }

        if (selectedTab == idx)
        {
            HideSheet();
            return;
        }

        selectedTab = idx;
        SetSheetSprite(idx);
    }

    // ===== Helpers Sheet =====
    private void SetSheetSprite(int idx)
    {
        if (!sheetImage) return;
        sheetImage.sprite = (idx >= 0 && idx < lotSprites.Length) ? lotSprites[idx] : null;
    }

    private void ShowSheet()
    {
        if (!panelSheet) return;
        panelSheet.gameObject.SetActive(true);
        panelSheet.DOKill();
        panelSheet.anchoredPosition = sheetHidden;
        panelSheet.DOAnchorPos(sheetShown, durSheet).SetEase(easeOut);
        sheetVisible = true;
    }

    private void HideSheet()
    {
        if (!panelSheet) return;
        panelSheet.DOKill();
        panelSheet.DOAnchorPos(sheetHidden, durSheet).SetEase(easeIn)
                  .OnComplete(() =>
                  {
                      panelSheet.gameObject.SetActive(false);
                      sheetVisible = false;
                  });
    }

    private void HideSheetImmediate()
    {
        if (!panelSheet) return;
        panelSheet.DOKill();
        panelSheet.gameObject.SetActive(false);
        panelSheet.anchoredPosition = sheetHidden;
        sheetVisible = false;
    }


    // Para que la Activity pueda forzar cerrar todo al cambiar de lote
    public void CloseAll() => InitHidden();
}
