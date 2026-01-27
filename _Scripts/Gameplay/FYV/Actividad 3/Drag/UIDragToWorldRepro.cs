using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class UIDragToWorldRepro : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Producto (mundo)")]
    public GameObject prefabMundo;                 // fallback (compat)
    public JabaTipo tipoJaba;
    public Material previewMaterial;

    [Header("Variantes (opcional)")]
    [SerializeField] private List<GameObject> prefabVariantesMundo = new List<GameObject>();

    [Header("Preview")]
    public bool constrainToFixedX = true;
    public float fixedXWorld = 0f;
    public float alturaPreview = 0.3f;
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreviewExtra = 0.0f;

    [Header("Raycast")]
    public LayerMask dropMaskUbicacion = ~0;

    private Camera _cam;
    private bool _arrastrando;
    private GameObject _preview;

    // ✅ Prefab elegido para ESTE drag (no cambia durante el arrastre)
    private GameObject _chosenPrefab;

    /// <summary>
    /// (tipo, prefabElegido, valido)
    /// </summary>
    public static event Action<JabaTipo, GameObject, bool> OnDropUIButton;

    private void Awake() { _cam = Camera.main; }

    /// <summary>
    /// El spawner te pasa la lista de variantes. Si está vacía, se usa prefabMundo (compat).
    /// </summary>
    public void SetPrefabVariants(List<GameObject> variants)
    {
        prefabVariantesMundo = variants != null ? variants : new List<GameObject>();
    }

    /// <summary>Usado por el spawner para comparar cuando singleUseGlobal está activo.</summary>
    public bool ContainsPrefabVariant(GameObject prefab)
    {
        if (prefab == null) return false;

        if (prefabVariantesMundo != null && prefabVariantesMundo.Count > 0)
        {
            for (int i = 0; i < prefabVariantesMundo.Count; i++)
                if (prefabVariantesMundo[i] == prefab) return true;
        }

        return prefabMundo == prefab;
    }

    private GameObject ChoosePrefabForThisDrag()
    {
        // Si hay variantes válidas, escoge aleatorio entre esas
        if (prefabVariantesMundo != null && prefabVariantesMundo.Count > 0)
        {
            int validCount = 0;
            for (int i = 0; i < prefabVariantesMundo.Count; i++)
                if (prefabVariantesMundo[i] != null) validCount++;

            if (validCount > 0)
            {
                int pick = UnityEngine.Random.Range(0, validCount);
                for (int i = 0; i < prefabVariantesMundo.Count; i++)
                {
                    var p = prefabVariantesMundo[i];
                    if (p == null) continue;
                    if (pick == 0) return p;
                    pick--;
                }
            }
        }

        // Fallback legacy
        return prefabMundo;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!_cam) return;

        _chosenPrefab = ChoosePrefabForThisDrag();
        if (!_chosenPrefab) return;

        _arrastrando = true;

        _preview = Instantiate(_chosenPrefab);

        // Preview ultraligero
        foreach (var r in _preview.GetComponentsInChildren<Renderer>(true))
        {
            if (previewMaterial) r.sharedMaterial = previewMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_Color"))
            {
                var c = r.sharedMaterial.color; c.a = 0.5f; r.sharedMaterial.color = c;
            }
        }
        foreach (var col in _preview.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_arrastrando || !_preview) return;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (constrainToFixedX)
        {
            Plane plane = new Plane(Vector3.right, new Vector3(fixedXWorld, 0f, 0f));
            if (plane.Raycast(ray, out float t))
            {
                var p = ray.GetPoint(t);
                p.x = fixedXWorld;
                p.y += (alturaPreview + alturaPreviewExtra);
                if (clampYZ)
                {
                    p.y = Mathf.Clamp(p.y, yRange.x, yRange.y);
                    p.z = Mathf.Clamp(p.z, zRange.x, zRange.y);
                }
                _preview.transform.position = p;
            }
        }
        else if (Physics.Raycast(ray, out RaycastHit hit, 50f, dropMaskUbicacion))
        {
            _preview.transform.position = hit.point + Vector3.up * (alturaPreview + alturaPreviewExtra);
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_arrastrando) return;
        _arrastrando = false;

        bool valido = false;

        if (_preview)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            UbicacionArea area = null;
            UbicacionSlot slot = null;

            var hits = Physics.RaycastAll(ray, 50f, dropMaskUbicacion);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Prioridad: trigger compuesto
            UbicacionAreaAutoStack autoStack = null;
            foreach (var h in hits)
            {
                autoStack = h.collider.GetComponentInParent<UbicacionAreaAutoStack>();
                if (autoStack != null) break;
            }

            if (autoStack != null)
            {
                if (autoStack.TryPlace(_chosenPrefab, tipoJaba, out slot))
                {
                    valido = true;
                    GetComponent<ReproUIButtonHandle>()?.RecycleSelf();
                }
            }
            else
            {
                // Fallback: slot/area
                foreach (var h in hits)
                {
                    slot = h.collider.GetComponentInParent<UbicacionSlot>();
                    if (slot) { area = slot.area; break; }
                    var a = h.collider.GetComponentInParent<UbicacionArea>();
                    if (a) { area = a; slot = null; break; }
                }

                if (area != null && area.tipoArea == tipoJaba)
                {
                    if (slot == null && area.slots != null)
                    {
                        foreach (var s in area.slots)
                            if (s != null && !s.ocupado) { slot = s; break; }
                    }

                    if (slot != null && !slot.ocupado && _chosenPrefab != null)
                    {
                        slot.Place(_chosenPrefab);
                        valido = true;
                        GetComponent<ReproUIButtonHandle>()?.RecycleSelf();
                    }
                }
            }

            Destroy(_preview);
        }

        OnDropUIButton?.Invoke(tipoJaba, _chosenPrefab, valido);
        _chosenPrefab = null;
    }
}
