using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class UIDragToWorldClaseo : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Plano principal (ESTANTE)")]
    public Transform planeAnchor;                 // estante diagonal (Transform)
    public Vector3 planeNormalLocal = Vector3.up; // normal local del estante

    [Tooltip("Collider que delimita el área válida del estante (Box/Mesh/Capsule/Sphere). Si es Mesh, ideal que sea CONVEX o usa BoxCollider proxy.")]
    public Collider planeBounds;

    [Header("Fallback (SUELO)")]
    public bool useFloorFallback = true;          // si sales del estante, cae al primer hit (suelo, etc.)

    [Header("Pegado al dedo / movimiento")]
    public float dragDepthOffset = 0.18f;         // 0.1–0.25
    public float followLerp = 0.0f;               // 0 = instantáneo

    [Header("Clamps/altura del preview")]
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreview = 0f;
    public float alturaPreviewExtra = 0f;

    [Header("Preview visual")]
    public Material previewMaterial;
    public bool alignPreviewWithPlane = true;
    public bool holdLastValidIfInvalid = true;

    [Header("Tolerancias")]
    [Tooltip("Umbral para considerar 'dentro' cuando usamos ClosestPoint.")]
    public float closestPointEpsilon = 0.0004f;

    private CanvasGroup cg;
    private Canvas canvas;
    private Camera cam;

    private GameObject previewGO;
    private ClaseoButtonPayload payload;

    private Vector3 lastValidPos;
    private bool hasLastValid;
    private Vector3 currentPos;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        cam = (canvas && canvas.worldCamera) ? canvas.worldCamera : Camera.main;
        payload = GetComponent<ClaseoButtonPayload>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cg) { cg.blocksRaycasts = false; cg.alpha = 0.7f; }
        hasLastValid = false;

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
        currentPos = previewGO ? previewGO.transform.position : Vector3.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!cam || !previewGO) return;

        Vector3 worldPos;
        bool got = TryProjectToShelfOrFloor(eventData, out worldPos);

        if (got)
        {
            if (clampYZ)
            {
                worldPos.y = Mathf.Clamp(worldPos.y + alturaPreview + alturaPreviewExtra, yRange.x, yRange.y);
                worldPos.z = Mathf.Clamp(worldPos.z, zRange.x, zRange.y);
            }
            else
            {
                worldPos.y += (alturaPreview + alturaPreviewExtra);
            }

            currentPos = (followLerp > 0f)
                ? Vector3.Lerp(currentPos, worldPos, 1f - Mathf.Exp(-followLerp * Time.deltaTime))
                : worldPos;

            previewGO.transform.position = currentPos;

            lastValidPos = currentPos;
            hasLastValid = true;

            if (alignPreviewWithPlane && planeAnchor)
            {
                var nWorld = planeAnchor.TransformDirection(planeNormalLocal.normalized);
                var fwd = Vector3.ProjectOnPlane(cam.transform.forward, nWorld).normalized;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                previewGO.transform.rotation = Quaternion.LookRotation(fwd, nWorld);
            }
        }
        else if (holdLastValidIfInvalid && hasLastValid)
        {
            previewGO.transform.position = lastValidPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (cg) { cg.blocksRaycasts = true; cg.alpha = 1f; }

        bool placed = false;

        if (payload && payload.prefabMundo)
        {
            Vector3 dropPoint;
            if (!TryProjectToShelfOrFloor(eventData, out dropPoint) && hasLastValid)
                dropPoint = lastValidPos;

            var cand = FindBestAreaCandidate(eventData, dropPoint);

            if (cand.stack && cand.area)
            {
                bool ok = cand.area.ValidaArea(payload.fruta, payload.madurez) ||
                          cand.area.ValidaArea(payload.fruta, payload.madurez.ToString());

                if (ok && cand.stack.TryPlace(payload.prefabMundo, payload.fruta, out _))
                {
                    placed = true;
                    var act = FindObjectOfType<PhasedActivityBasePro>();
                    if (act) act.ReportSuccess();
                    try { SoundManager.Instance.PlaySound("success"); } catch { }
                    Destroy(gameObject); // botón UI
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
        hasLastValid = false;
    }

    // ================== PROYECCIÓN PRINCIPAL ==================

    /// Proyecta al plano del estante dentro de sus bounds. Si sale, opcionalmente cae al suelo.
    private bool TryProjectToShelfOrFloor(PointerEventData ev, out Vector3 pos)
    {
        pos = Vector3.zero;
        var ray = (cam ? cam : Camera.main).ScreenPointToRay(ev.position);

        // 1) Plano del estante
        if (planeAnchor)
        {
            Vector3 nWorld = planeAnchor.TransformDirection(planeNormalLocal.normalized);
            var plane = new Plane(nWorld, planeAnchor.position);

            if (plane.Raycast(ray, out float t))
            {
                var onPlane = ray.GetPoint(Mathf.Max(0f, t - dragDepthOffset));

                if (planeBounds)
                {
                    // SafeClosestPoint: sin excepciones, funciona para cualquier collider
                    Vector3 cp;
                    bool inside = SafeContainsPoint(planeBounds, onPlane, out cp);

                    if (inside)
                    {
                        pos = onPlane;
                        return true;
                    }
                    else if (!useFloorFallback)
                    {
                        // clamp al borde del estante y listo
                        pos = cp;
                        return true;
                    }
                    // si queremos piso, pasamos al fallback
                }
                else
                {
                    // sin bounds → aceptamos el punto del plano
                    pos = onPlane;
                    return true;
                }
            }
        }

        // 2) Fallback al primer impacto
        if (useFloorFallback)
        {
            if (Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Collide))
            {
                pos = hit.point;
                return true;
            }
        }

        return false;
    }

    // ---------- Helpers de bounds sin romper con MeshCollider no-convexo ----------

    private static bool SupportsClosestPoint(Collider c)
    {
        if (c is BoxCollider) return true;
        if (c is SphereCollider) return true;
        if (c is CapsuleCollider) return true;
        if (c is MeshCollider mc) return mc.convex; // Solo convex soporta ClosestPoint “exacto”
        return false;
    }

    /// Determina si 'p' está dentro del collider y devuelve 'cp' como punto clamp seguro.
    /// - Si el collider soporta ClosestPoint: usa esa vía con epsilon.
    /// - Si no: usa AABB (bounds) como aproximación segura (no crashea).
    private bool SafeContainsPoint(Collider col, Vector3 p, out Vector3 cp)
    {
        cp = p;

        if (SupportsClosestPoint(col))
        {
            var closest = col.ClosestPoint(p);
            float sq = (closest - p).sqrMagnitude;
            bool inside = sq <= (closestPointEpsilon * closestPointEpsilon);
            cp = inside ? p : closest;
            return inside;
        }
        else
        {
            // Aproximación: si el punto cae dentro del AABB del collider, lo consideramos “dentro”
            // y clamp al borde del AABB si está fuera.
            var b = col.bounds;
            bool insideAabb = b.Contains(p);
            cp = new Vector3(
                Mathf.Clamp(p.x, b.min.x, b.max.x),
                Mathf.Clamp(p.y, b.min.y, b.max.y),
                Mathf.Clamp(p.z, b.min.z, b.max.z)
            );
            return insideAabb;
        }
    }

    // ================== CANDIDATO DE ÁREA ==================

    struct Candidate
    {
        public UbicacionAreaAutoStack stack;
        public UbicacionArea area;
        public float score;
    }

    private Candidate FindBestAreaCandidate(PointerEventData ev, Vector3 dropPoint)
    {
        var best = new Candidate { score = float.MaxValue };
        var ray = (cam ? cam : Camera.main).ScreenPointToRay(ev.position);

        // A) primer impacto válido
        var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            var area = h.collider.GetComponentInParent<UbicacionArea>();
            var stack = h.collider.GetComponentInParent<UbicacionAreaAutoStack>();
            if (area && stack)
            {
                float s = i; // más cercano primero
                best = new Candidate { area = area, stack = stack, score = s };
                break;
            }
        }

        // B) proximidad en torno a dropPoint (por si tocó marco)
        var cols = Physics.OverlapSphere(dropPoint, 0.4f, ~0, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var area = c.GetComponentInParent<UbicacionArea>();
            var stack = c.GetComponentInParent<UbicacionAreaAutoStack>();
            if (area && stack)
            {
                float s = Vector3.SqrMagnitude(c.ClosestPoint(dropPoint) - dropPoint);
                if (s < best.score) best = new Candidate { area = area, stack = stack, score = s };
            }
        }

        return best;
    }
}
