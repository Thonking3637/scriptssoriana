using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Elements")]
    public Text activityText;
    public GameObject mainUIPanel;
    public GameObject trainingCompletePanel;

    [Header("Activity Panels")]
    public List<GameObject> activityPanels; // Lista de paneles para cada actividad

    private bool _subscribed = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void OnDestroy()
    {
        // ✅ Extra seguro: por si el objeto se destruye sin pasar por OnDisable
        TryUnsubscribe();

        if (Instance == this)
            Instance = null;
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        // GameManager puede no estar listo aún dependiendo del orden de ejecución
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnActivityChange += UpdateUI;
        GameManager.Instance.OnTrainingComplete += ShowTrainingCompletePanel;
        _subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnActivityChange -= UpdateUI;
            GameManager.Instance.OnTrainingComplete -= ShowTrainingCompletePanel;
        }

        _subscribed = false;
    }

    /// <summary>
    /// Actualiza la UI para reflejar la actividad actual.
    /// </summary>
    private void UpdateUI(int activityIndex)
    {
        if (activityText != null)
            activityText.text = $"Actividad {activityIndex + 1}";

        ActivateActivityPanel(activityIndex);
    }

    /// <summary>
    /// Activa el panel correcto de la actividad y oculta los demás.
    /// </summary>
    private void ActivateActivityPanel(int activityIndex)
    {
        if (activityPanels == null) return;

        for (int i = 0; i < activityPanels.Count; i++)
        {
            if (activityPanels[i] != null)
                activityPanels[i].SetActive(i == activityIndex);
        }
    }

    /// <summary>
    /// Muestra la pantalla de finalización del entrenamiento.
    /// </summary>
    private void ShowTrainingCompletePanel()
    {
        if (mainUIPanel != null) mainUIPanel.SetActive(false);
        if (trainingCompletePanel != null) trainingCompletePanel.SetActive(true);
    }

    /// <summary>
    /// Actualiza la lista de actividades en la UI.
    /// </summary>
    public void UpdateActivityList()
    {
        Debug.Log("Actualizando lista de actividades...");
    }
}
