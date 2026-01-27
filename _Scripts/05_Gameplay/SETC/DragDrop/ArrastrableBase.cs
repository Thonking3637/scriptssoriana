using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasGroup))]
public abstract class ArrastrableBase : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Identificador de Matching")]
    public string id;

    public DropZone zonaAsignada;
    public Action OnDropCorrecto;

    protected CanvasGroup canvasGroup;
    protected Vector3 posicionInicial;
    protected Transform padreOriginal;

    [Header("Bloqueo")]
    [SerializeField] private bool bloquearAlColocar = true;
    private bool fijado = false;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        posicionInicial = transform.localPosition;
        padreOriginal = transform.parent;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (fijado) return;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (fijado) return;
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Raycast UI
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone zonaDetectada = null;
        foreach (var r in results)
        {
            var z = r.gameObject.GetComponent<DropZone>();
            if (z != null) { zonaDetectada = z; break; }
        }

        // ❗ No aceptar si la zona ya está completa
        if (zonaDetectada != null && zonaDetectada.EsZonaCompleta())
        {
            SoundManager.Instance.PlaySound("error");
            VolverAOrigen();
            return;
        }

        // Drop correcto
        if (zonaDetectada != null && zonaDetectada.EsZonaCorrecta(this))
        {
            transform.SetParent(zonaDetectada.transform);
            transform.localPosition = Vector3.zero;
            zonaAsignada = zonaDetectada;

            SoundManager.Instance.PlaySound("success");
            zonaDetectada.RegistrarEntregaCorrecta();
            OnDropCorrecto?.Invoke();

            if (bloquearAlColocar)
            {
                fijado = true;
                enabled = false; // ← desactiva el script de drag
                // (Opcional) desactivar raycasts del gráfico para que no reciba más input
                // foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                // (Opcional) canvasGroup.interactable = false;
            }
        }
        else
        {
            SoundManager.Instance.PlaySound("error");
            VolverAOrigen();
        }
    }

    public void VolverAOrigen()
    {
        transform.SetParent(padreOriginal);
        transform.localPosition = posicionInicial;
    }

    public void ResetObjeto()
    {
        VolverAOrigen();
        zonaAsignada = null;
        // Rehabilitar para próximas rondas
        fijado = false;
        enabled = true;
        // (Opcional) foreach (var g in GetComponentsInChildren<Graphic>()) g.raycastTarget = true;
        // (Opcional) canvasGroup.interactable = true;
    }
}
