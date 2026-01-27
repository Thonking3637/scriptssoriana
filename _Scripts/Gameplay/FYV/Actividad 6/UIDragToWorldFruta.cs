using UnityEngine;
using UnityEngine.EventSystems;

public enum FrutaAccionTipo
{
    Ninguno = 0,
    Lavado_Agua,
    Lavado_Papaya,
    Lavado_Desinfectante,

    Desinfeccion_Jaba,
    Desinfeccion_Desinfectante,
    Desinfeccion_Agua,   
    Desinfeccion_Papaya,
    Desinfeccion_Timer,

    Corte_Exhibicion_Papaya,
    Corte_Exhibicion_Cuchillo,
    Corte_Exhibicion_Bagazo,
    Corte_Exhibicion_Film,
    Corte_Exhibicion_Etiqueta,
    Corte_Exhibicion_Supermarket,

    Corte_Coctel_Papaya,
    Corte_Coctel_Cuchillo1,
    Corte_Coctel_Cuchillo2,
    Corte_Coctel_Bagazo,
    Corte_Coctel_Cuchillo3,
    Corte_Coctel_Taper,
    Corte_Coctel_Etiqueta,
    Corte_Coctel_SuperMarket,

    Vitrina_ProductoFinal
}

public class UIDragToWorldFruta : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Config básica")]
    public FrutaAccionTipo tipoAccion = FrutaAccionTipo.Ninguno;
    public Camera worldCamera;
    public LayerMask movementMask;
    public LayerMask dropMask;
    public float rayDistance = 20f;

    [Header("Preview en 3D")]
    public GameObject previewPrefab;
    public bool alignToNormal = false;
    public Vector3 previewOffset;

    [Header("Instanciado al soltar")]
    public bool instantiateOnDrop = true;
    public GameObject worldPrefab;
    public bool parentToTarget = true;
    public bool useTargetSnapPoint = true;

    [Header("Clamp de movimiento (para que no se mueva de más)")]
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

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (tipoAccion == FrutaAccionTipo.Ninguno) return;

        _dragging = true;
        
        if (previewPrefab != null)
        {
            _previewInstance = Instantiate(previewPrefab);
        }
        
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = false;
        }

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

        // Volver a dejar que el botón reciba clicks
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = true;
        }

        Vector3 hitPos;
        FrutaPicadaDropTarget target = RaycastDropTarget(eventData, out hitPos);

        if (target != null)
        {
            // 1) Lógica de la actividad
            target.HandleDrop(tipoAccion, hitPos);

            // 2) Instanciar objeto final en la zona (como Claseo/Repro)
            if (instantiateOnDrop)
            {
                // Nuevo: respetar ocupación del snap
                if (!target.CanSpawnInstance())
                {
                    // Slot ya ocupado → no instanciamos otro
                    // Aquí podrías llamar un sonido de error global si quieres.
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
                            spawnRot = Quaternion.identity; // Puedes ajustar si quieres usar normal
                        }

                        GameObject realInstance = Instantiate(prefabReal, spawnPos, spawnRot);
                        if (parentToTarget)
                        {
                            realInstance.transform.SetParent(target.transform, true);
                        }

                        // Marcamos la zona como ocupada
                        target.MarkOccupied();
                    }
                }
            }
        }
        else
        {
            // Aquí puedes disparar un sonido de error global si quieres
            // o dejar que el Activity lo maneje.
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

        // Usamos movementMask para limitar dónde "pega" el preview
        if (Physics.Raycast(ray, out hit, rayDistance, movementMask))
        {
            Vector3 pos = hit.point + previewOffset;

            // Clamp por ejes (para que no se vaya donde no debe)
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
            // Si no pega nada, lo ponemos frente a la cámara para no desaparecer
            Vector3 pos = ray.origin + ray.direction * 2.0f;

            if (clampX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (clampY) pos.y = Mathf.Clamp(pos.y, minY, maxY);
            if (clampZ) pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

            _previewInstance.transform.position = pos;
        }
    }

    FrutaPicadaDropTarget RaycastDropTarget(PointerEventData eventData, out Vector3 hitPos)
    {
        hitPos = Vector3.zero;
        if (worldCamera == null) return null;

        Ray ray = worldCamera.ScreenPointToRay(eventData.position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rayDistance, dropMask))
        {
            hitPos = hit.point;
            return hit.collider.GetComponentInParent<FrutaPicadaDropTarget>();
        }

        return null;
    }
}
