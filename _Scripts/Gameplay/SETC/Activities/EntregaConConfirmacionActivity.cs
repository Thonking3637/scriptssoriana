using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class EntregaConConfirmacionActivity : ActivityBase
{
    [Header("Cliente")]
    public CustomerSpawner customerSpawner;

    [Header("Canvas y Entrega")]
    public GameObject canvasEntrega;
    public List<GameObject> objetosEntregables;
    public List<DropZone> zonasCliente;

    [Header("Dialogos")]
    public List<DialogStep> pasosDialogo;

    private int entregasCorrectas = 0;
    private GameObject clienteActual;
    private Client currentClient;

    public override void StartActivity()
    {
        base.StartActivity();

        ResetInstructions();
        ShowInstructionsPanel();

        canvasEntrega.SetActive(false);

        cameraController.MoveToPosition("A3", () =>
        {
            UpdateInstructionOnce(0, SpawnClienteYMostrarDialogo);
        });
    }

    private void SpawnClienteYMostrarDialogo()
    {
        clienteActual = customerSpawner.SpawnCustomer();
        currentClient = clienteActual.GetComponent<Client>();
        clienteActual.GetComponent<CustomerMovement>().rotationClient = 0;

        clienteActual.GetComponent<CustomerMovement>().MoveToCheckout(() =>
        {
            ReproducirDialogo(
                pasosDialogo[0],
                currentClient,
                onCorrect: () => UpdateInstructionOnce(1, ActivarCanvasEntrega),
                onIncorrect: () => SoundManager.Instance.PlaySound("error"),
                onComplete: null
            );
        });
    }

    private void ActivarCanvasEntrega()
    {
        entregasCorrectas = 0;
        canvasEntrega.SetActive(true);

        foreach (var zona in zonasCliente)
            zona.onDropCorrect += ValidarEntrega;
    }

    private void ValidarEntrega()
    {
        entregasCorrectas++;

        foreach (var objeto in objetosEntregables)
        {
            var arrastrable = objeto.GetComponent<ArrastrableBase>();
            if (arrastrable != null && arrastrable.zonaAsignada != null)
            {
                objeto.SetActive(false);
                break;
            }
        }

        if (entregasCorrectas >= zonasCliente.Count)
        {
            foreach (var zona in zonasCliente)
                zona.onDropCorrect -= ValidarEntrega;

            canvasEntrega.SetActive(false);
            UpdateInstructionOnce(2, MostrarPreguntaFinal);
        }
    }

    private void MostrarPreguntaFinal()
    {
        ReproducirDialogo(
            pasosDialogo[1],
            currentClient,
            onCorrect: () => UpdateInstructionOnce(3, MostrarUltimaRespuesta),
            onIncorrect: () => SoundManager.Instance.PlaySound("error"),
            onComplete: null
        );
    }

    private void MostrarUltimaRespuesta()
    {
        ReproducirDialogo(
            pasosDialogo[2],
            currentClient,
            onCorrect: () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    SoundManager.Instance.PlaySound("win");
                    clienteActual.GetComponent<CustomerMovement>().MoveToExit(() =>
                    {
                        CompleteActivity();
                    });
                });
            },
            onIncorrect: () => SoundManager.Instance.PlaySound("error"),
            onComplete: null
        );
    }

    private void ReproducirDialogo(DialogStep step, Client cliente, System.Action onCorrect, System.Action onIncorrect, System.Action onComplete)
    {
        DialogSystem.Instance.ShowClientDialog(
            cliente,
            step.clientText,
            step.customClientAudio,
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    step.question,
                    step.options,
                    step.correctAnswer,
                    step.customQuestionAudio,
                    onCorrect: () =>
                    {
                        if (step.customAnswerAudio != null)
                        {
                            SoundManager.Instance.PlaySound(step.customAnswerAudio, () =>
                            {
                                onCorrect?.Invoke();
                            });
                        }
                        else
                        {
                            onCorrect?.Invoke();
                        }
                    },
                    onIncorrect: onIncorrect
                );
            }
        );
    }

    protected override void Initialize() { }
}







/*
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class EntregaConConfirmacionActivity : ActivityBase
{
    [Header("Cliente")]
    public CustomerSpawner customerSpawner;

    [Header("Canvas y Entrega")]
    public GameObject canvasEntrega;
    public List<GameObject> objetosEntregables;
    public List<DropZone> zonasCliente;

    private int entregasCorrectas = 0;
    private GameObject clienteActual;
    private Client currentClient;

    public override void StartActivity()
    {
        base.StartActivity();

        ResetInstructions();
        ShowInstructionsPanel();

        canvasEntrega.SetActive(false);

        cameraController.MoveToPosition("A3", () =>
        {
            UpdateInstructionOnce(0, SpawnClienteYMostrarDialogo);
        });
    }

    private void SpawnClienteYMostrarDialogo()
    {
        clienteActual = customerSpawner.SpawnCustomer();
        currentClient = clienteActual.GetComponent<Client>();
        clienteActual.GetComponent<CustomerMovement>().rotationClient = 0;

        clienteActual.GetComponent<CustomerMovement>().MoveToCheckout(() =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentClient,
                "Hola, vengo por mi compra. ¿Está lista?. Mi Nº de pedido es 54243",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo deberías responder a ello?",
                        new List<string>
                        {
                                "Sí, deme un momento por favor",
                                "No se la verdad, soy nueva en esto.",
                                "La verdad, es que no la hice jaja.",
                                "Disculpa, ¿Quien eres?"
                        },
                        "Sí, deme un momento por favor",
                        onCorrect: () =>
                        {
                            UpdateInstructionOnce(1, ActivarCanvasEntrega);
                        },
                        onIncorrect: () => SoundManager.Instance.PlaySound("error")
                    );
                });
        });
    }

    private void ActivarCanvasEntrega()
    {
        entregasCorrectas = 0;
        canvasEntrega.SetActive(true);

        foreach (var zona in zonasCliente)
            zona.onDropCorrect += ValidarEntrega;
    }

    private void ValidarEntrega()
    {
        entregasCorrectas++;

        foreach (var objeto in objetosEntregables)
        {
            var arrastrable = objeto.GetComponent<ArrastrableBase>();
            if (arrastrable != null && arrastrable.zonaAsignada != null)
            {
                objeto.SetActive(false);
                break;
            }
        }

        if (entregasCorrectas >= zonasCliente.Count)
        {
            foreach (var zona in zonasCliente)
                zona.onDropCorrect -= ValidarEntrega;

            canvasEntrega.SetActive(false);
            UpdateInstructionOnce(2, MostrarPreguntaFinal);
        }
    }

    private void MostrarPreguntaFinal()
    {
        DialogSystem.Instance.ShowClientDialog(
            currentClient,
            "Muchas gracias, eso sería todo, ¿cierto?",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Como deberias preguntarle sobre su pago?",
                    new List<string>
                    {
                        "¿Usted ya pagó en línea?",
                        "No sé, déjame preguntarle a mi supervisor",
                        "Sí, ya vayase.",
                        "Voy a ir a comer"
                    },
                   "¿Usted ya pagó en línea?",
                    onCorrect: () =>
                    {
                        UpdateInstructionOnce(3, MostrarUltimaRespuesta);
                    },
                    onIncorrect: () => SoundManager.Instance.PlaySound("error")
                );
            });
    }

    private void MostrarUltimaRespuesta()
    {
        DialogSystem.Instance.ShowClientDialog(
            currentClient,
            "Sí, ya pagué todo antes de venir",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Cómo deberías responder a ello?",
                    new List<string>
                    {
                        "Muchas gracias, eso sería todo. Tenga excelente día",
                        "¿Por qué sigue acá entonces?",
                        "Sí, ya vayase.",
                        "Ah ya, esta bien, adiós."
                    },
                    "Muchas gracias, eso sería todo. Tenga excelente día",
                    onCorrect: () =>
                    {
                        UpdateInstructionOnce(4, () =>
                        {
                            clienteActual.GetComponent<CustomerMovement>().MoveToExit(() =>
                            {
                                CompleteActivity();
                            });
                        });
                    },
                    onIncorrect: () => SoundManager.Instance.PlaySound("error")
                );
            });
    }

    protected override void Initialize() { }
}
*/