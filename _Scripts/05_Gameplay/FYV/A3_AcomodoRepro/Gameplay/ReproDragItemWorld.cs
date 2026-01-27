using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class ReproDragItemWorld : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Tipos")]
    public JabaTipo tipoJaba;
    public ProductoTipo productoTipo;

    [Header("Prefab fuente (para slot.Place)")]
    public GameObject prefabFuente;

    [Header("Drag")]
    public float alturaOffset = 0.1f;
    public LayerMask rayMask = ~0; // todo por defecto

    private Camera _cam;
    private Vector3 _startPos;

    private void Awake()
    {
        _cam = Camera.main;
        // Asegurar Collider (requerido) y que esté en un layer raycasteable
    }

    public void OnBeginDrag(PointerEventData e)
    {
        _startPos = transform.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_cam) return;

        Ray ray = _cam.ScreenPointToRay(e.position);
        if (Physics.Raycast(ray, out var hit, 100f, rayMask))
        {
            Vector3 p = hit.point + Vector3.up * alturaOffset;
            transform.position = p;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_cam) return;

        Ray ray = _cam.ScreenPointToRay(e.position);

        // Buscar primero un slot, si no, un área
        UbicacionSlot slot = null;
        UbicacionArea area = null;

        var hits = Physics.RaycastAll(ray, 100f, rayMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            slot = h.collider.GetComponentInParent<UbicacionSlot>();
            if (slot) { area = slot.area; break; }

            var a = h.collider.GetComponentInParent<UbicacionArea>();
            if (a) { area = a; slot = null; break; }
        }

        bool valido = false;

        if (area != null && TipoUtils.Coincide(tipoJaba, area.tipoArea))
        {
            // Si no vino slot, busca el primero libre
            if (slot == null && area.slots != null)
            {
                foreach (var s in area.slots) { if (s != null && !s.ocupado) { slot = s; break; } }
            }

            if (slot != null && !slot.ocupado && prefabFuente != null)
            {
                slot.Place(prefabFuente);   // Apila en la jaba correcta
                valido = true;
                Destroy(gameObject);        // El botón/ítem del scroll se elimina
            }
        }

        if (!valido)
        {
            // Regresar al origen si no fue válido
            transform.position = _startPos;
            ReproEvents.OnErrorDrop?.Invoke();
        }

        ReproEvents.OnConveyorItemDropped?.Invoke(tipoJaba, productoTipo, gameObject, area, slot, valido);
    }
}
