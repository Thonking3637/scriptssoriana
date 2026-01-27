using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonSFX : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler
{
    [Header("Audio")]
    public AudioClip click;

    [Header("Comportamiento")]
    [Tooltip("Reproducir el sonido en PointerDown (recomendado para evitar bloqueos cuando el botón se desactiva en el mismo frame).")]
    public bool playOnDown = true;

    [Tooltip("Si está activo, solo arma/reproduce el sonido si el botón era interactable en el momento del Down.")]
    public bool requireInteractableAtDown = true;

    private Button _button;
    private bool _armed; // se armó en Down (era interactable) → permitido sonar aunque luego se desactive

    void Awake()
    {
        _button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // ¿estaba interactable en el momento del Down?
        bool canInteract = (!_button || _button.interactable);

        _armed = canInteract || !requireInteractableAtDown;

        if (playOnDown && _armed)
            PlaySfxSafe();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Si ya sonamos en Down, no repetimos (a menos que quieras doble sonido).
        if (playOnDown) return;

        // Si fue armado en Down, sonamos aunque el botón ya esté no-interactable en este frame
        if (_armed)
        {
            PlaySfxSafe();
            return;
        }

        // Fallback: si no usamos armado, exigir que siga interactable
        if (_button && !_button.interactable) return;
        PlaySfxSafe();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // Para teclado/mandos: tratamos igual que Click (no hay Down aquí)
        if (playOnDown)
        {
            // En Submit no hubo Down; si se requiere armado, no sonar.
            if (requireInteractableAtDown) return;
            PlaySfxSafe();
            return;
        }

        if (_button && !_button.interactable) return;
        PlaySfxSafe();
    }

    private void PlaySfxSafe()
    {
        if (!click) return;

        // Soporte a dos estilos de SoundManager (Instance o estático con PlaySFX/Play)
        try
        {
            var sm = typeof(SoundManager).GetProperty("Instance")?.GetValue(null, null);
            if (sm != null)
            {
                var m = sm.GetType().GetMethod("PlaySFX", new[] { typeof(AudioClip) });
                if (m != null) { m.Invoke(sm, new object[] { click }); return; }
            }
        }
        catch { /* ignorar */ }

        try
        {
            // Si tienes SoundManager con método estático PlaySFX(AudioClip)
            var m = typeof(SoundManager).GetMethod("PlaySFX", new[] { typeof(AudioClip) });
            if (m != null) { m.Invoke(null, new object[] { click }); return; }
        }
        catch { /* ignorar */ }

        // Último fallback: si usas SoundManager.Play("ui_click") por key, cámbialo aquí
        try { SoundManager.Instance.PlaySound(click); } catch { /* ignorar */ }
    }
}
