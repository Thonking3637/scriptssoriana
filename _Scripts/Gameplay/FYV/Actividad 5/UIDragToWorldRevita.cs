using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragToWorldRevita : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Config básica")]
    public RevitaAccionTipo tipoAccion = RevitaAccionTipo.Ninguno;
    public Camera worldCamera;

    [Tooltip("En qué capas puede pegar el preview mientras arrastras.")]
    public LayerMask movementMask;

    [Tooltip("En qué capas puede soltar (colisiona con RevitaDropTarget).")]
    public LayerMask dropMask;

    public float rayDistance = 20f;

    [Header("Preview en 3D")]
    public GameObject previewPrefab;
    public bool alignToNormal = false;
    public Vector3 previewOffset;

    [Header("Instanciado al soltar")]
    [Tooltip("Si true, instanciamos un prefab real al soltar en un target válido.")]
    public bool instantiateOnDrop = true;

    [Tooltip("Prefab real al soltar. Si es null usa previewPrefab.")]
    public GameObject worldPrefab;

    [Tooltip("Si true, el realInstance se parenteará al RevitaDropTarget.")]
    public bool parentToTarget = true;

    [Tooltip("Si true, usamos el snapPoint del target como posición/rotación.")]
    public bool useTargetSnapPoint = true;

    [Header("Clamp de movimiento (no se mueva de más)")]
    public bool clampX = false;
    public float minX = -5f;
    public float maxX = 5f;

    public bool clampY = false;
    public float minY = 0f;
    public float maxY = 5f;

    public bool clampZ = false;
    public float minZ = -5f;
    public float maxZ = 5f;

    private GameObject _previewInstance;
    private bool _dragging;

    void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tipoAccion == RevitaAccionTipo.Ninguno) return;
        
        //SoundManager.Instance?.PlaySound("success");
        
        _dragging = true;
        
        if (previewPrefab != null)
        {
            _previewInstance = Instantiate(previewPrefab);
        }

        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
            cg.blocksRaycasts = false;

        UpdatePreviewPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        UpdatePreviewPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        // Recuperar clicks normales
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
            cg.blocksRaycasts = true;

        Vector3 hitPos;
        RevitaDropTarget target = RaycastDropTarget(eventData, out hitPos);

        if (target != null)
        {
            // 1) Lógica del target (validar tipo y estado)
            bool aceptado = target.HandleDrop(tipoAccion, hitPos);

            // 2) Instanciar objeto final en la zona (como Claseo/Repro/Fruta)
            if (aceptado && instantiateOnDrop)
            {
                if (!target.CanSpawnInstance())
                {
                    // Slot ocupado → podrías disparar sonido de error aquí también
                    SoundManager.Instance?.PlaySound("error");
                }
                else
                {
                    GameObject prefabReal = worldPrefab != null ? worldPrefab : previewPrefab;
                    if (prefabReal != null)
                    {
                        Vector3 spawnPos = hitPos;
                        Quaternion spawnRot = Quaternion.identity;

                        // Si el target tiene snapPoint y lo queremos usar
                        if (useTargetSnapPoint && target.snapPoint != null)
                        {
                            spawnPos = target.snapPoint.position;
                            spawnRot = target.snapPoint.rotation;
                        }
                        else if (alignToNormal)
                        {
                            spawnRot = Quaternion.identity;
                        }

                        GameObject realInstance = Instantiate(prefabReal, spawnPos, spawnRot);
                        if (parentToTarget)
                            realInstance.transform.SetParent(target.transform, true);

                        // Marcamos la zona como ocupada
                        target.MarkOccupied(realInstance);
                    }
                }
            }
        }
        else
        {
            SoundManager.Instance?.PlaySound("error");
        }

        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    void UpdatePreviewPosition(PointerEventData eventData)
    {
        if (_previewInstance == null || worldCamera == null) return;

        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        RaycastHit hit;

        // Usamos movementMask para limitar dónde pega el preview
        if (Physics.Raycast(ray, out hit, rayDistance, movementMask))
        {
            Vector3 pos = hit.point + previewOffset;

            if (clampX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (clampY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
            if (clampZ) pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

            _previewInstance.transform.position = pos;

            if (alignToNormal)
            {
                _previewInstance.transform.rotation =
                    Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90f, 0f, 0f);
            }
        }
        else
        {
            // Fallback: frente a la cámara
            Vector3 pos = ray.origin + ray.direction * 2.0f;

            if (clampX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (clampY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
            if (clampZ) pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

            _previewInstance.transform.position = pos;
        }
    }

    RevitaDropTarget RaycastDropTarget(PointerEventData eventData, out Vector3 hitPos)
    {
        hitPos = Vector3.zero;
        if (worldCamera == null) return null;

        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rayDistance, dropMask))
        {
            hitPos = hit.point;
            return hit.collider.GetComponentInParent<RevitaDropTarget>();
        }

        return null;
    }
}
