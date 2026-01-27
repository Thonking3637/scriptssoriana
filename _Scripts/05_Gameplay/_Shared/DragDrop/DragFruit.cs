using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragFruit : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Fruit fruitData;

    private Vector3 startPosition;
    private Camera mainCamera;
    private bool isDragging = false;
    private bool yaEscaneado = false;

    public float scanRadius = 0.5f;
    public float moveSpeed = 1.2f;

    [SerializeField] private Transform endPoint;
    [SerializeField] private string endPointTag = "EndPoint";
    private Coroutine moveCoroutine;

    public event Action<DragFruit> OnScanned;
    private void Start()
    {
        mainCamera = Camera.main;
        startPosition = transform.position;
        moveSpeed = 1.2f;

        IgnoreCollisionsWithOtherFruits();

        if (endPoint == null)
        {
            var go = GameObject.FindWithTag(endPointTag);
            if (go != null) endPoint = go.transform;
        }

        if (endPoint == null)
        {
            Debug.LogError($"No se encontró endPoint. Asigna el Transform o crea un objeto con tag '{endPointTag}'.");
        }

        moveCoroutine = StartCoroutine(MoveFruitToEnd());
    }

    //EventSystem: Begin drag (touch/mouse)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (yaEscaneado) return;

        isDragging = true;
        StopMoving();
    }

    //EventSystem: Drag (touch/mouse)
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || yaEscaneado) return;

        Vector3 newPosition = GetWorldPosition(eventData.position);
        transform.position = new Vector3(newPosition.x, startPosition.y, startPosition.z);
    }

    //EventSystem: End drag (touch/mouse)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;

        if (!TryScanFruit())
        {
            MoveImmediatelyToEndPoint();
        }
    }

    /// <summary>
    /// Convierte screen -> world usando la misma profundidad del objeto.
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
    /// Intenta escanear la fruta.
    /// </summary>
    private bool TryScanFruit()
    {
        if (yaEscaneado) return false;

        Vector3 scanCenter = transform.position + GetColliderCenterOffset();
        Collider[] colliders = Physics.OverlapSphere(scanCenter, scanRadius);

        foreach (var collider in colliders)
        {
            if (!collider.CompareTag("Scanner"))
                continue;

            // ✅ robusto: busca el scanner en el GO, padre o hijos
            ProductScanner scanner = collider.GetComponentInParent<ProductScanner>();
            if (scanner == null) scanner = collider.GetComponentInChildren<ProductScanner>();

            if (scanner == null)
            {
                // Debug útil para saber qué collider estás tocando
                Debug.LogWarning($"[DragFruit] Collider con tag Scanner pero sin ProductScanner: {collider.name}");
                continue;
            }

            yaEscaneado = true;
            OnScanned?.Invoke(this);

            Debug.Log($"{gameObject.name} escaneado correctamente.");
            return true;
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

    private IEnumerator MoveFruitToEnd()
    {
        if (endPoint == null) yield break;

        while (transform.position.x > endPoint.position.x)
        {
            transform.position += Vector3.left * moveSpeed * Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Calcula el offset del centro del collider.
    /// </summary>
    private Vector3 GetColliderCenterOffset()
    {
        Collider collider = GetComponent<Collider>();
        return collider != null ? collider.bounds.center - transform.position : Vector3.zero;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 scanCenter = transform.position + GetColliderCenterOffset();
        Gizmos.DrawWireSphere(scanCenter, scanRadius);
    }

    private void IgnoreCollisionsWithOtherFruits()
    {
        Collider myCollider = GetComponent<Collider>();
        DragFruit[] otherFruits = FindObjectsOfType<DragFruit>();

        foreach (var other in otherFruits)
        {
            if (other == this) continue;

            Collider otherCollider = other.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(myCollider, otherCollider);
            }
        }
    }
}
