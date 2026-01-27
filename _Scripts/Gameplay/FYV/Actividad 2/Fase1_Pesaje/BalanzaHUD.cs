using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BalanzaHUD : MonoBehaviour
{
    [Header("UI")]
    public Button btnTara;
    public Button btnTomarNota;
    public TMP_Text txtPeso;

    public event Action OnTaraPressed;
    public event Action OnTomarNotaPressed;

    private float _pesoNeto = 0f;

    private void Awake()
    {
        if (btnTara)
        {
            btnTara.onClick.RemoveAllListeners();
            btnTara.onClick.AddListener(() => OnTaraPressed?.Invoke());
        }

        if (btnTomarNota)
        {
            btnTomarNota.onClick.RemoveAllListeners();
            btnTomarNota.onClick.AddListener(() => OnTomarNotaPressed?.Invoke());
        }
    }

    public void SetInteractableTara(bool value) { if (btnTara) btnTara.interactable = value; }
    public void SetInteractableTomarNota(bool value) { if (btnTomarNota) btnTomarNota.interactable = value; }

    public void SetPeso(float kg)
    {
        _pesoNeto = Mathf.Max(0f, kg);
        if (txtPeso) txtPeso.text = $"{_pesoNeto:0.00} kg";
    }

    public void ResetDisplay()
    {
        _pesoNeto = 0f;
        if (txtPeso) txtPeso.text = "0.00 kg";
    }

    public float GetPeso() => _pesoNeto;
}
