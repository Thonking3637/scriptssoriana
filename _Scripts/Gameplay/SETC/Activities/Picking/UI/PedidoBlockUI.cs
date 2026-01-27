using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class PedidoBlockUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public TMP_Text estadoText;
    public Button blockButton;
    public GameObject FirstPanel;
    public GameObject panelPickingEstantes;
    public TiempoPickingUI cronometro;

    [Header("Configuración")]
    public float delayAntesDeMostrarSiguiente = 1.5f;

    private bool yaEmpezo = false;
    private bool _subscribed = false;

    private void OnEnable()
    {
        ResetState();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        DOTween.Kill(this); // corta delayed calls si el panel se apaga
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (blockButton == null) return;

        blockButton.onClick.AddListener(OnBlockClicked);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (blockButton != null)
            blockButton.onClick.RemoveListener(OnBlockClicked);

        _subscribed = false;
    }

    private void ResetState()
    {
        yaEmpezo = false;

        if (estadoText != null)
            estadoText.text = "PENDIENTE";
    }

    private void OnBlockClicked()
    {
        if (yaEmpezo) return;
        yaEmpezo = true;

        estadoText.text = "EN CURSO";
        cronometro?.IniciarCronometro();

        DOVirtual.DelayedCall(delayAntesDeMostrarSiguiente, () =>
        {
            FirstPanel?.SetActive(false);
            panelPickingEstantes?.SetActive(true);

            if (PickingActivity.Instance != null &&
                PickingActivity.Instance.gameObject.activeInHierarchy)
            {
                PickingActivity.Instance.EmpezarPicking();
                return;
            }

            if (ChangePickingOrderActivity.Instance != null &&
                ChangePickingOrderActivity.Instance.gameObject.activeInHierarchy)
            {
                ChangePickingOrderActivity.Instance.EmpezarPicking();
                return;
            }

            Debug.LogWarning("[PedidoBlockUI] No hay PickingActivity activa para iniciar.");
        });
    }
}
