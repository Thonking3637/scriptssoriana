using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using TMPro;

public class CeldaRegistroUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum TipoCelda { Producto, Kg, Registro }
    [Header("Config")]
    public TipoCelda tipo;
    public string dia;
    public string productoFila;

    [Header("UI")]
    public TMP_Text texto;
    public UnityEngine.UI.Image bg;

    [System.Serializable]
    public class StickerEvent : UnityEvent<string, string, string> { } // (tipo, valor, dia)
    [Header("Eventos")]
    public StickerEvent OnStickerColocado = new StickerEvent();

    public int slotIndex = 0;

    // 🔒 Ahora es un lock explícito controlado por el Tablero
    private bool _locked = false;

    public void AcceptSticker(string tipoStr, string valor)
    {
        if (_locked) return; // si ya se congeló por trío correcto, no aceptar más

        bool ok =
            (tipo == TipoCelda.Producto && tipoStr == "Producto") ||
            (tipo == TipoCelda.Kg && tipoStr == "Kg") ||
            (tipo == TipoCelda.Registro && tipoStr == "Registro");

        if (!ok) return; // tipo no coincide → ignorar

        if (texto) texto.text = valor;

        // 🔊 sonido en cada drop
        SoundManager.Instance?.PlaySound("pencilsound");

        // Avisar al tablero (tipo, valor, dia)
        OnStickerColocado?.Invoke(tipo.ToString(), valor, dia);
    }

    // 👉 Llamado por el Tablero cuando valida el trío correcto
    public void Lock()
    {
        _locked = true;
        // Opcional: feedback visual sutil
        if (bg) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.12f);
    }

    public void Unlock()
    {
        _locked = false;
    }

    public string GetValor() => texto ? texto.text : string.Empty;

    // helpers UI menores…
    public void OnPointerEnter(PointerEventData _) { if (bg) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.5f); }
    public void OnPointerExit(PointerEventData _) { if (bg) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.2f); }
}
