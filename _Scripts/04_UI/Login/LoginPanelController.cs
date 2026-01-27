// LoginPanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoginPanelController : MonoBehaviour
{
    [SerializeField] TMP_InputField inputNombre;
    [SerializeField] Button btnAceptar;
    [SerializeField] IntroPuertasSequence intro; // arrástralo en el Inspector

    void Awake()
    {
        btnAceptar.onClick.RemoveAllListeners();
        btnAceptar.onClick.AddListener(OnAceptar);
    }

    void OnEnable()
    {
        // si quieres autoselección del input
        inputNombre.text = "";
        inputNombre.ActivateInputField();
    }

    void OnAceptar()
    {
        string nombre = (inputNombre.text ?? "").Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            inputNombre.ActivateInputField();
            return;
        }

        // Guardar nombre (fuente de verdad)
        ProfileManager.SaveName(nombre);

        // Avisar a la intro que ya puede mostrar el menú/extraPanel
        if (intro) intro.OnUserAcceptedName();
    }
}
