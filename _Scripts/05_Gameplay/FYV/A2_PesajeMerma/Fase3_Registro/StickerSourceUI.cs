// StickerSourceUI.cs (usa ghost de texto)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class StickerSourceUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum TipoSticker { Producto, Kg, Registro }
    public TipoSticker tipo;
    public string valorTexto;

    [Header("Opcional")]
    public ScrollRect scrollRect; // para desactivar el scroll mientras arrastras

    public void OnPointerDown(PointerEventData eventData)
    {
        DragGhostManager.Instance?.BeginGhostText(valorTexto);
        DragGhostManager.Instance?.UpdateGhost(eventData.position);
        if (scrollRect) scrollRect.horizontal = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragGhostManager.Instance?.UpdateGhost(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Raycast UI en Overlay
        var canvas = DragGhostManager.Instance ? DragGhostManager.Instance.overlayCanvas : null;
        var raycaster = canvas ? canvas.GetComponent<GraphicRaycaster>() : null;

        if (raycaster)
        {
            var res = UIRaycast(eventData.position, raycaster);
            foreach (var r in res)
            {
                var celda = r.gameObject.GetComponentInParent<CeldaRegistroUI>();
                if (celda)
                {
                    celda.AcceptSticker(tipo.ToString(), valorTexto);
                    break;
                }
            }
        }

        DragGhostManager.Instance?.EndGhost();
        if (scrollRect) scrollRect.horizontal = true;
    }

    // util
    private static List<RaycastResult> _buf = new List<RaycastResult>(16);
    private static List<RaycastResult> UIRaycast(Vector2 screenPos, GraphicRaycaster raycaster)
    {
        _buf.Clear();
        var ev = new PointerEventData(EventSystem.current) { position = screenPos };
        raycaster.Raycast(ev, _buf);
        return _buf;
    }
}
