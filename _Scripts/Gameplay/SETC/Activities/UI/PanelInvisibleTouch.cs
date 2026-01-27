using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class PanelInvisibleTouch : MonoBehaviour, IPointerClickHandler
{
    private bool activo = false;
    private Action onClickCallback;

    public void Activar(Action callback)
    {
        activo = true;
        onClickCallback = callback;
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!activo) return;

        onClickCallback?.Invoke();

        activo = false;
        gameObject.SetActive(false);
    }
}
