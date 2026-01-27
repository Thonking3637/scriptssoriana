using UnityEngine;
using TMPro;

public class WelcomeCorner : MonoBehaviour
{
    public TMP_Text txtBienvenida;
    [TextArea] public string formato = "Bienvenido/a, {0}";

    void OnEnable()
    {
        Actualizar();
    }

    public void Actualizar()
    {
        string nombre = ProfileManager.GetName();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            txtBienvenida.text = "Bienvenido/a";
        }
        else
        {
            txtBienvenida.text = string.Format(formato, nombre);
        }
    }
}
