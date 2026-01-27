using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PanelResumenRegistro : MonoBehaviour
{
    [Header("Actividad Target")]
    [Tooltip("La actividad a la que se le llamar� CompleteActivity()")]
    [SerializeField] private PesajeMermaActivity activity;

    [Header("UI")]
    [SerializeField] private Button btnContinuar;
    
    private void Awake()
    {
        if (activity == null)
            activity = FindObjectOfType<PesajeMermaActivity>();
    }

    private void OnEnable()
    {
        if (btnContinuar)
        {
            btnContinuar.onClick.RemoveListener(OnContinuar);
            btnContinuar.onClick.AddListener(OnContinuar);
        }
    }

    private void OnDisable()
    {
        if (btnContinuar)
        {
            btnContinuar.onClick.RemoveListener(OnContinuar);
        }
    }

    private void OnContinuar()
    {
        if (activity != null)
        {
            activity.CompleteActivity();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
