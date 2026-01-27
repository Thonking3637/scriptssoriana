using UnityEngine;
using UnityEngine.EventSystems;

public class ProductoDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Transform originalParent;
    private int originalIndex;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 offset;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();

        // 🔹 Feedback visual: se vuelve más transparente y se escala
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;
        transform.localScale = Vector3.one * 1.1f;

        // 🔹 Sacamos del layout temporalmente
        transform.SetParent(transform.root);
        offset = eventData.position - new Vector2(rectTransform.position.x, rectTransform.position.y);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 newPos = eventData.position - offset;
        rectTransform.position = newPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;

        // 🔁 Calcular inserción en base a posición Y
        int insertIndex = originalParent.childCount;

        for (int i = 0; i < originalParent.childCount; i++)
        {
            if (transform.position.y > originalParent.GetChild(i).position.y)
            {
                insertIndex = i;
                break;
            }
        }

        transform.SetParent(originalParent);
        transform.SetSiblingIndex(insertIndex);
    }
}
