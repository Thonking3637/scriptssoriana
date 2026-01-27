using UnityEngine;
using UnityEngine.EventSystems;

public class StickerDropZone : MonoBehaviour, IDropHandler
{
    public BlockUI blockUI;

    public void OnDrop(PointerEventData eventData)
    {
        StickerDraggable sticker = eventData.pointerDrag.GetComponent<StickerDraggable>();
        if (sticker != null)
        {
            // Coloca el sprite en el BlockUI
            blockUI.SetSticker(sticker.GetComponent<UnityEngine.UI.Image>().sprite, sticker.esCheck);
            // Destruye el objeto draggeado
            Destroy(sticker.gameObject);
        }
    }
}
