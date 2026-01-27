using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SupervisorButton : MonoBehaviour, IPointerClickHandler
{
    public event Action OnPressed;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnPressed?.Invoke();
    }
}
