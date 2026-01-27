using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragObject : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Product productData;

    private Vector3 startPosition;
    private Camera mainCamera;
    private bool isDragging = false;
    private bool yaEscaneado = false;

    public float scanRadius = 0.5f;
    public float moveSpeed = 1.2f;

    [SerializeField] private Transform endPoint;
    [SerializeField] private string endPointTag = "EndPoint";

    private Coroutine moveCoroutine;

    private string originalPoolName;
    public event Action<DragObject> OnScanned;
    public string OriginalPoolName => originalPoolName;

    private void Start()
    {
        mainCamera = Camera.main;
        startPosition = transform.position;
        moveSpeed = 1.2f;

        IgnoreCollisionsWithOtherProducts();

        if (endPoint == null)
        {
            var go = GameObject.FindWithTag(endPointTag);
            if (go != null) endPoint = go.transform;
        }

        if (endPoint == null)
        {
            Debug.LogError($"No se encontró endPoint. Asigna el Transform o crea un objeto con tag '{endPointTag}'.");
        }

        moveCoroutine = StartCoroutine(MoveProductToEnd());
    }

    // ✅ Begin drag (touch/mouse)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (yaEscaneado) return;

        isDragging = true;
        StopMoving();
    }

    // ✅ Drag (touch/mouse)
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || yaEscaneado) return;

        Vector3 newPosition = GetWorldPosition(eventData.position);
        transform.position = new Vector3(newPosition.x, startPosition.y, startPosition.z);
    }

    // ✅ End drag (touch/mouse)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;

        if (!TryScanProduct())
        {
            MoveImmediatelyToEndPoint();
        }
    }

    /// <summary>
    /// 🔹 Convierte las coordenadas de la pantalla en coordenadas del mundo.
    /// Usa la misma profundidad del objeto para que no “salte”.
    /// </summary>
    private Vector3 GetWorldPosition(Vector2 screenPosition)
    {
        Vector3 worldPosition = new Vector3(
            screenPosition.x,
            screenPosition.y,
            mainCamera.WorldToScreenPoint(transform.position).z
        );
        return mainCamera.ScreenToWorldPoint(worldPosition);
    }

    /// <summary>
    /// 🔹 Intenta escanear el producto.
    /// </summary>
    private bool TryScanProduct()
    {
        if (yaEscaneado) return false;

        Vector3 scanCenter = transform.position + GetColliderCenterOffset();
        Collider[] colliders = Physics.OverlapSphere(scanCenter, scanRadius);

        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Scanner"))
            {
                ProductScanner scanner = collider.GetComponentInParent<ProductScanner>();
                if (scanner == null) scanner = collider.GetComponentInChildren<ProductScanner>();

                if (scanner != null)
                {
                    yaEscaneado = true;

                    SoundManager.Instance.PlaySound("bip");

                    OnScanned?.Invoke(this);

                    Debug.Log($"{gameObject.name} escaneado correctamente.");

                    //ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, originalPoolName, gameObject);
                    return true;
                }
            }
        }

        return false;
    }

    private void StopMoving()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }

    private void MoveImmediatelyToEndPoint()
    {
        if (endPoint == null) return;
        transform.position = endPoint.position;
    }

    private IEnumerator MoveProductToEnd()
    {
        if (endPoint == null) yield break;

        while (transform.position.x > endPoint.position.x)
        {
            transform.position += Vector3.left * moveSpeed * Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 🔹 Obtiene el centro del collider del objeto.
    /// </summary>
    private Vector3 GetColliderCenterOffset()
    {
        Collider collider = GetComponent<Collider>();
        return collider != null ? collider.bounds.center - transform.position : Vector3.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 scanCenter = transform.position + GetColliderCenterOffset();
        Gizmos.DrawWireSphere(scanCenter, scanRadius);
    }

    private void IgnoreCollisionsWithOtherProducts()
    {
        Collider myCollider = GetComponent<Collider>();
        DragObject[] otherProducts = FindObjectsOfType<DragObject>();

        foreach (var other in otherProducts)
        {
            if (other == this) continue;

            Collider otherCollider = other.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(myCollider, otherCollider);
            }
        }
    }

    public string GetOriginalPoolNameSafe()
    {
        return string.IsNullOrEmpty(originalPoolName) ? gameObject.name : originalPoolName;
    }

    public void SetOriginalPoolName(string name)
    {
        originalPoolName = name;
    }
}
