using UnityEngine;
using UnityEngine.EventSystems;

public class InteractiveButton : MonoBehaviour, IPointerClickHandler
{
    public CashRegisterActivationActivity activity;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (activity == null) return;
        activity.OnInteractionComplete();
    }
}
