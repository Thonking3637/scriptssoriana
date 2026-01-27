using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class StickerDraggable : MonoBehaviour
{
    public bool esCheck;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private bool isBeingForcedToDrag = false;

    private GraphicRaycaster raycaster;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
        raycaster = canvas.GetComponent<GraphicRaycaster>();
    }

    private void Update()
    {
        if (isBeingForcedToDrag)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out pos
            );
            rectTransform.anchoredPosition = pos;

            if (Input.GetMouseButtonUp(0))
            {
                EndForcedDrag();
            }
        }
    }

    public void BeginImmediateDrag()
    {
        StartDrag();
        isBeingForcedToDrag = true;
    }

    private void StartDrag()
    {
        canvasGroup.blocksRaycasts = false;
    }

    private void EndForcedDrag()
    {
        canvasGroup.blocksRaycasts = true;
        isBeingForcedToDrag = false;

        // Simula drop manual
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        bool dropped = false;

        foreach (var result in results)
        {
            var dropZone = result.gameObject.GetComponent<StickerDropZone>();
            if (dropZone != null)
            {
                dropZone.blockUI.SetSticker(GetComponent<Image>().sprite, esCheck);
                dropped = true;

                Destroy(gameObject);
                break;
            }
        }

        if (!dropped)
        {
            Destroy(gameObject);
        }
    }
}
