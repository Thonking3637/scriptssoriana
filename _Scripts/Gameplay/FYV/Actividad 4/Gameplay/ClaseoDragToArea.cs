using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class ClaseoDragToArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Preview")]
    public Material previewMaterial;
    public bool useFixedX = true;
    public float fixedXWorld = -150f;
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreview = 0f;
    public float alturaPreviewExtra = 0f;

    private CanvasGroup cg;
    private Canvas canvas;
    private Camera cam;

    private GameObject previewGO;
    private ClaseoButtonPayload payload;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        cam = canvas && canvas.worldCamera ? canvas.worldCamera : Camera.main;
        payload = GetComponent<ClaseoButtonPayload>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cg) { cg.blocksRaycasts = false; cg.alpha = 0.7f; }

        if (previewGO) Destroy(previewGO);
        if (payload && payload.prefabMundo)
        {
            previewGO = Instantiate(payload.prefabMundo);
            previewGO.name = "[Preview] " + payload.prefabMundo.name;

            foreach (var c in previewGO.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in previewGO.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            if (previewMaterial)
                foreach (var r in previewGO.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = previewMaterial;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!cam || !previewGO) return;

        Vector3 worldPos = previewGO.transform.position;
        if (useFixedX)
        {
            var plane = new Plane(Vector3.right, new Vector3(fixedXWorld, 0f, 0f));
            var ray = cam.ScreenPointToRay(eventData.position);
            if (plane.Raycast(ray, out float t)) worldPos = ray.GetPoint(t);
        }
        else
        {
            var ray = cam.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out var hit, 200f, ~0, QueryTriggerInteraction.Collide))
                worldPos = hit.point;
        }

        if (clampYZ)
        {
            worldPos.y = Mathf.Clamp(worldPos.y + alturaPreview + alturaPreviewExtra, yRange.x, yRange.y);
            worldPos.z = Mathf.Clamp(worldPos.z, zRange.x, zRange.y);
        }
        else
        {
            worldPos.y += (alturaPreview + alturaPreviewExtra);
        }

        if (useFixedX) worldPos.x = fixedXWorld;
        previewGO.transform.position = worldPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (cg) { cg.blocksRaycasts = true; cg.alpha = 1f; }

        bool placed = false;

        if (payload && payload.prefabMundo)
        {
            var ray = (cam ? cam : Camera.main).ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out var hit, 200f, ~0, QueryTriggerInteraction.Collide))
            {
                var stack = hit.collider.GetComponentInParent<UbicacionAreaAutoStack>();
                var area = hit.collider.GetComponentInParent<UbicacionArea>();

                if (stack && area)
                {
                    // valida fruta + madurez (enum y compat string)
                    bool ok = area.ValidaArea(payload.fruta, payload.madurez) ||
                              area.ValidaArea(payload.fruta, payload.madurez.ToString());

                    if (ok)
                    {
                        // NO instanciamos aquí. Dejamos que el stack cree y coloque.
                        if (stack.TryPlace(payload.prefabMundo, payload.fruta, out _))
                        {
                            placed = true;
                            var act = FindObjectOfType<PhasedActivityBasePro>();
                            if (act) act.ReportSuccess();
                            try { SoundManager.Instance.PlaySound("success"); } catch { }
                            Destroy(gameObject); // eliminar el botón
                        }
                    }
                }
            }
        }

        if (!placed)
        {
            try { SoundManager.Instance.PlaySound("error"); } catch { }
            var actErr = FindObjectOfType<PhasedActivityBasePro>();
            if (actErr) actErr.ReportError();
            
            ClaseoEvents.OnErrorDrop?.Invoke();
        }

        if (previewGO) Destroy(previewGO);
        previewGO = null;
    }
}
