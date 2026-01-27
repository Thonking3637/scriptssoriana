using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UIDragToWorldUbicacion : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public GameObject prefabMundo;
    public Material previewMaterial;

    [Header("Preview")]
    public bool constrainToFixedX = true;
    public float fixedXWorld = 0f;
    public float alturaPreview = 0.0f;
    public bool clampYZ = false;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);

    [Header("Validación")]
    public JabaTipo tipoJaba;

    public LayerMask dropMaskUbicacion = ~0;

    public static event Action<JabaTipo, GameObject, UbicacionArea, UbicacionSlot> OnDropUbicacion;

    private Camera _cam;
    private bool _arrastrando;
    private GameObject _preview;

    private void Awake() { _cam = Camera.main; }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!prefabMundo || !_cam) return;
        _arrastrando = true;
        _preview = Instantiate(prefabMundo);
        foreach (var r in _preview.GetComponentsInChildren<Renderer>())
        {
            if (previewMaterial) r.material = previewMaterial;
            if (r.material.HasProperty("_Color"))
            { var c = r.material.color; c.a = 0.5f; r.material.color = c; }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_arrastrando || !_preview) return;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (constrainToFixedX)
        {
            Plane plane = new Plane(Vector3.right, new Vector3(fixedXWorld, 0f, 0f));
            if (plane.Raycast(ray, out float t))
            {
                var p = ray.GetPoint(t);
                p.x = fixedXWorld; p.y += alturaPreview;
                if (clampYZ) { p.y = Mathf.Clamp(p.y, yRange.x, yRange.y); p.z = Mathf.Clamp(p.z, zRange.x, zRange.y); }
                _preview.transform.position = p;
            }
        }
        else if (Physics.Raycast(ray, out RaycastHit hit, 50f))
        {
            _preview.transform.position = hit.point + Vector3.up * alturaPreview;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_arrastrando) return;
        _arrastrando = false;

        if (_preview)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            UbicacionArea area = null; UbicacionSlot slot = null;

            var hits = Physics.RaycastAll(ray, 50f, dropMaskUbicacion);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                slot = h.collider.GetComponentInParent<UbicacionSlot>();
                if (slot) { area = slot.area; break; }
                var a = h.collider.GetComponentInParent<UbicacionArea>();
                if (a) { area = a; slot = null; break; }
            }

            OnDropUbicacion?.Invoke(tipoJaba, prefabMundo, area, slot);
            Destroy(_preview);
        }
    }
}
