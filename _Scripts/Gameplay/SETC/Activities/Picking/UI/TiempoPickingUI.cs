using UnityEngine;
using TMPro;

public class TiempoPickingUI : MonoBehaviour
{
    public TMP_Text tiempoText;

    private float tiempoTranscurrido = 0f;
    private bool activo = false;

    private void Update()
    {
        if (!activo) return;

        tiempoTranscurrido += Time.deltaTime;
        int minutos = Mathf.FloorToInt(tiempoTranscurrido / 60);
        int segundos = Mathf.FloorToInt(tiempoTranscurrido % 60);
        tiempoText.text = string.Format("{0:00}:{1:00}", minutos, segundos);
    }

    public void IniciarCronometro()
    {
        activo = true;
    }
}
