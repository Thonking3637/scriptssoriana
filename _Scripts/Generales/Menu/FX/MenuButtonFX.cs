using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class MenuButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public float pressScale = 0.95f;
    public float selectScale = 1.08f;
    public float normalScale = 1f;
    public float dur = 0.12f;

    void IPointerDownHandler.OnPointerDown(PointerEventData e) =>
        transform.DOScale(pressScale, dur);

    void IPointerUpHandler.OnPointerUp(PointerEventData e) =>
        transform.DOScale(selectScale, dur);

    public void OnSelect(BaseEventData e) =>
        transform.DOScale(selectScale, dur);

    public void OnDeselect(BaseEventData e) =>
        transform.DOScale(normalScale, dur);
}
