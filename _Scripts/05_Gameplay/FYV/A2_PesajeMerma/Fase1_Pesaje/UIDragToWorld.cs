using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UIDragToWorld : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Objeto a instanciar (preview)")]
    public GameObject prefabMundo;
    public Material previewMaterial;

    [Header("Preview - Movimiento")]
    public bool constrainToFixedX = true;  // ← activar para forzar X fijo
    public float fixedXWorld = 0f;         // ← el valor X que tú defines
    public float alturaPreview = 0.0f;     // ← offset vertical opcional

    [Tooltip("Limitar desplazamiento en Y y Z (opcional)")]
    public bool clampYZ = false;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);

    [Header("Modo")]
    public bool esJaba = false;
    public JabaTipo tipoJaba;
    public ProductoCatalogSO producto;

    public LayerMask dropMaskJaba = ~0;
    public LayerMask dropMaskProducto = ~0;

    // Eventos globales (escucha la Activity)
    public static event Action<JabaTipo, GameObject> OnDropJaba;
    public static event Action<ProductoCatalogSO> OnDropProducto;

    private Camera _cam;
    private bool _arrastrando;
    private GameObject _preview;

    private void Awake()
    {
        _cam = Camera.main;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (prefabMundo == null || _cam == null) return;

        _arrastrando = true;
        _preview = Instantiate(prefabMundo);

        foreach (var r in _preview.GetComponentsInChildren<Renderer>())
        {
            if (previewMaterial != null) r.material = previewMaterial;
            if (r.material.HasProperty("_Color"))
            {
                var c = r.material.color; c.a = 0.5f; r.material.color = c;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_arrastrando || _preview == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (constrainToFixedX)
        {
            // Intersección con un plano X = fixedXWorld
            Plane plane = new Plane(Vector3.right, new Vector3(fixedXWorld, 0f, 0f));
            if (plane.Raycast(ray, out float t))
            {
                Vector3 p = ray.GetPoint(t);
                p.x = fixedXWorld;
                p.y += alturaPreview;

                if (clampYZ)
                {
                    p.y = Mathf.Clamp(p.y, yRange.x, yRange.y);
                    p.z = Mathf.Clamp(p.z, zRange.x, zRange.y);
                }

                _preview.transform.position = p;
            }
        }
        else
        {
            // Fallback: raycast a colisionadores
            if (Physics.Raycast(ray, out RaycastHit hit, 30f))
            {
                Vector3 pos = hit.point + Vector3.up * alturaPreview;
                _preview.transform.position = pos;
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_arrastrando) return;
        _arrastrando = false;

        if (_preview != null)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

            if (esJaba)
            {
                // Soltar JABA: buscamos un BalanzaSlot en la cadena
                if (Physics.Raycast(ray, out RaycastHit hit, 50f, dropMaskJaba))
                {
                    var slot = hit.collider.GetComponentInParent<BalanzaSlot>();
                    if (slot) OnDropJaba?.Invoke(tipoJaba, prefabMundo);
                }
            }
            else
            {
                // Soltar PRODUCTO: prioriza cualquier JabaMermaView en la pila de impactos
                var hits = Physics.RaycastAll(ray, 50f, dropMaskProducto);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    var jmv = h.collider.GetComponentInParent<JabaMermaView>();
                    if (jmv != null)
                    {
                        OnDropProducto?.Invoke(producto);
                        break;
                    }
                }
            }
            Destroy(_preview);
        }
    }
}
