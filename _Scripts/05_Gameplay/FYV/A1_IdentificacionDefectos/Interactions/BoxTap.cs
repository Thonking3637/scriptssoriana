using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public class BoxTap : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Id de posición de cámara opcional para esta caja (si quieres usarlo)")]
    public string cameraPositionId = "A1_Caja_01";

    public System.Action<BoxTap> OnOpened;

    public int loteIndex = -1;
    [SerializeField] Collider clickCollider;
    [SerializeField] BoxBlinker blinker;

    // Mouse / Touch sin EventSystem (rápido y simple)
    void OnMouseUpAsButton()
    {
        // Evita tocar a través de UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        OnOpened?.Invoke(this);
    }

    // Pointer con EventSystem + PhysicsRaycaster (opcional)
    public void OnPointerClick(PointerEventData eventData)
    {
        OnOpened?.Invoke(this);
    }

    public void SetInteractable(bool enabled)
    {
        if (clickCollider == null) clickCollider = GetComponentInChildren<Collider>(true);
        if (clickCollider) clickCollider.enabled = enabled;
    }

    public void StartBlink()
    {
        if (!blinker) blinker = GetComponent<BoxBlinker>();
        if (blinker) blinker.Play();
    }

    public void StopBlink()
    {
        if (!blinker) blinker = GetComponent<BoxBlinker>();
        if (blinker) blinker.Stop();
    }
}
