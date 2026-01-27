using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class PedidoPorHojaActivity : ActivityBase
{
    [Header("Fase 1: Hoja Inicial")]
    public GameObject hojaVisual;
    public Button hojaButton;
    public GameObject hojaCanvas;
    public Button continueButton;

    [Header("Fase 2: Comando")]
    public List<Button> commandButtons;

    [Header("Fase 3: Input Pedido")]
    public GameObject panelContainerGeneral;
    public GameObject panelInputPedido;
    public TMP_InputField amountInputField;
    public List<Button> numberButtons;
    public Button enterInputButton;
    public string numeroPedidoCorrecto = "123456";

    [Header("Fase 4: Productos")]
    public TextMeshProUGUI listaProductosTMP;
    public TextMeshProUGUI totalTMP;

    public override void StartActivity()
    {
        base.StartActivity();

        hojaVisual.SetActive(false);
        hojaCanvas.SetActive(false);
        panelContainerGeneral.gameObject.SetActive(false);
        panelInputPedido.gameObject.SetActive(false);
        enterInputButton.gameObject.SetActive(false);

        foreach (var numbers in numberButtons)
        {
            numbers.gameObject.SetActive(false);
        }
        DOVirtual.DelayedCall(0.2f, () =>
        {
            cameraController.MoveToPosition("Vista Hoja", () =>
            {
                UpdateInstructionOnce(0, () =>
                {
                    hojaVisual.SetActive(true);
                    hojaButton.gameObject.SetActive(true);
                    hojaButton.onClick.RemoveAllListeners();
                    hojaButton.onClick.AddListener(MostrarHoja);
                });
            });

        });
    }

    private void MostrarHoja()
    {
        AnimateCanvas(hojaCanvas);
        UpdateInstructionOnce(1);
        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            HideCanvas(hojaCanvas);
            hojaVisual.SetActive(false);

            cameraController.MoveToPosition("Vista Monitor", () =>
            {
                panelContainerGeneral.gameObject.SetActive(true);

                UpdateInstructionOnce(2, () =>
                {
                    AnimateButtonsSequentiallyWithActivation(commandButtons, ActivarPanelInput);
                });
            });
        });
    }

    private void ActivarPanelInput()
    {
        SoundManager.Instance.PlaySound("success");

        foreach( var buttonsdeactivate in commandButtons)
        {
            buttonsdeactivate.gameObject.SetActive(false); 
        }

        panelInputPedido.SetActive(true);
        amountInputField.text = "";
        amountInputField.gameObject.SetActive(true);
        amountInputField.DeactivateInputField();
        amountInputField.ActivateInputField();

        List<Button> inputButtons = GetButtonsForAmount(numeroPedidoCorrecto, numberButtons);

        UpdateInstructionOnce(3, () =>
        {
            AnimateButtonsSequentiallyWithActivation(inputButtons, () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    AnimateButtonsSequentiallyWithActivation(new List<Button> { enterInputButton }, () =>
                    {
                        ValidarPedido();
                    });
                });
            });
        });

       
    }

    private void ValidarPedido()
    {
        UpdateInstructionOnce(5, () =>
        {
            if (amountInputField.text == numeroPedidoCorrecto)
            {
                SoundManager.Instance.PlaySound("success");
                panelInputPedido.SetActive(false);

                string lista = "Leche        1        20.00\n" +
                               "Galletas     1        30.00\n" +
                               "Refresco     1        40.00\n" +
                               "Carne        1        80.00";
                listaProductosTMP.text = lista;

                totalTMP.text = "$170.00";

                UpdateInstructionOnce(6, () =>
                {
                    cameraController.MoveToPosition("Vista Monitor", () =>
                    {
                        DesactivarTodo();
                        SoundManager.Instance.PlaySound("win");
                        UpdateInstructionOnce(7, () =>
                        {
                            CompleteActivity();
                        });
                    });
                });


            }
            else
            {
                SoundManager.Instance.PlaySound("error");
                amountInputField.text = "";
            }
        });
    }

    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    protected override void Initialize()
    {
    }

    private void DesactivarTodo()
    {
        hojaVisual.SetActive(false);
        hojaCanvas.SetActive(false);
        hojaButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);

        panelContainerGeneral.SetActive(false);
        panelInputPedido.SetActive(false);
        amountInputField.gameObject.SetActive(false);
        enterInputButton.gameObject.SetActive(false);

        foreach (var b in numberButtons)
            b.gameObject.SetActive(false);

        foreach (var b in commandButtons)
            b.gameObject.SetActive(false);

        if (listaProductosTMP != null) listaProductosTMP.text = "";
        if (totalTMP != null) totalTMP.text = "";
    }
}
