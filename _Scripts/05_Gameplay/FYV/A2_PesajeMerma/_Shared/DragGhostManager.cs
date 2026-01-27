// DragGhostManager.cs (texto puro)
using UnityEngine;
using TMPro;

public class DragGhostManager : MonoBehaviour
{
    public static DragGhostManager Instance { get; private set; }

    [Header("Canvas Overlay (Screen Space - Overlay)")]
    public Canvas overlayCanvas;

    [Header("Ghost (solo texto)")]
    public RectTransform ghostRoot; // panel vacío o con un fondo neutro
    public TMP_Text ghostText;      // el texto grande que ves al arrastrar

    private bool _active;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (ghostRoot) ghostRoot.gameObject.SetActive(false);
    }

    public void BeginGhostText(string display)
    {
        if (!ghostRoot || !ghostText) return;
        ghostText.text = display;
        ghostRoot.gameObject.SetActive(true);
        _active = true;
    }

    public void UpdateGhost(Vector2 screenPos)
    {
        if (!_active || !ghostRoot) return;

        var parent = ghostRoot.parent as RectTransform;
        // Canvas Overlay → cámara null
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, null, out var local))
            ghostRoot.anchoredPosition = local;
    }

    public void EndGhost()
    {
        _active = false;
        if (ghostRoot) ghostRoot.gameObject.SetActive(false);
    }
}
