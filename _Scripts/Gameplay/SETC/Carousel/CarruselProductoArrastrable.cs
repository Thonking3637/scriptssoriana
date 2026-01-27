using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class CarruselProductoArrastrable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string productoId;

    private Transform startParent;
    private CanvasGroup grp;
    private Vector3 originalLocalPos;

    public Action onDropCorrecto;

    void Awake()
    {
        grp = GetComponent<CanvasGroup>();
        if (grp == null) grp = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        startParent = transform.parent;
        originalLocalPos = transform.localPosition;
        transform.SetParent(transform.root); // para no cortar visuales
        grp.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData e)
    {
        transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        grp.blocksRaycasts = true;

        // Detectar si está sobre un DropZone
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(e, results);

        DropZone zonaDetectada = null;

        foreach (var r in results)
        {
            var zona = r.gameObject.GetComponent<DropZone>();
            if (zona != null)
            {
                zonaDetectada = zona;
                break;
            }
        }

        if (zonaDetectada != null && zonaDetectada.idsEsperados.Contains(productoId))
        {
            transform.SetParent(zonaDetectada.transform);
            transform.localPosition = Vector3.zero;

            SoundManager.Instance.PlaySound("success");
            zonaDetectada.RegistrarEntregaCorrecta();

            // ✅ Aquí cambias el material si la zona lo soporta
            var cambiarMaterial = zonaDetectada.GetComponent<CambiarMaterialProducto3D>();
            if (cambiarMaterial != null)
            {
                cambiarMaterial.CambiarMaterialSiCoincide(productoId);
            }

            onDropCorrecto?.Invoke();
        }

        else
        {
            // Drop inválido
            transform.SetParent(startParent);
            transform.localPosition = originalLocalPos;
            SoundManager.Instance.PlaySound("error");
        }
    }
}
