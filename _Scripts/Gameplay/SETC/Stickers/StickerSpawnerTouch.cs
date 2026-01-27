using UnityEngine;
using UnityEngine.EventSystems;

public class StickerSpawnerTouch : MonoBehaviour, IPointerDownHandler
{
    public GameObject stickerPrefab;
    public Canvas canvas;

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("PointerDown detectado para spawn");

        GameObject clone = Instantiate(stickerPrefab, canvas.transform);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out Vector2 pos
        );
        clone.GetComponent<RectTransform>().anchoredPosition = pos;

        var draggable = clone.GetComponent<StickerDraggable>();
        if (draggable != null)
        {
            draggable.BeginImmediateDrag();
        }
        else
        {
            Debug.LogError("El prefab instanciado no tiene StickerDraggable.");
        }
    }

}
