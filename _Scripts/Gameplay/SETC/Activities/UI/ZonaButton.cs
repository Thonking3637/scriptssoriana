using UnityEngine;
using UnityEngine.UI;

public class ZonaButton: MonoBehaviour
{
    public string zonaId;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => ReposicionNoRecolectadoActivity.Instance.OnClickUbicacion(zonaId));
    }

    public void Desactivar()
    {
        if (button != null)
            button.interactable = false;
    }
}
