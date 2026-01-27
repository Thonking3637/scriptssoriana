using UnityEngine;
using TMPro;

public class ReviHUD : MonoBehaviour
{
    [Header("Refs")]
    public GameObject root;           // panel HUD raíz
    public TMP_Text stationLabel;     // "Recorte / Remojo / Escurrido / Refrigeración / Vitrina"
    public GameObject timerGroup;     // contenedor del timer
    public TMP_Text timerLabel;       // "15 s"

    public void Show(bool on) { if (root) root.SetActive(on); }

    public void SetStation(string name)
    {
        if (stationLabel) stationLabel.text = name ?? "";
    }

    public void ShowTimer(bool on) { if (timerGroup) timerGroup.SetActive(on); }

    public void SetTimerSeconds(int secs)
    {
        if (timerLabel) timerLabel.text = $"{Mathf.Clamp(secs, 0, 999):00} s";
    }

    public void ResetHUD()
    {
        Show(true);
        SetStation("");
        ShowTimer(false);
        SetTimerSeconds(0);
    }
}
