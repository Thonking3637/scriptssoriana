using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class DialogStep
{
    [TextArea] public string clientText;
    [TextArea] public string question;
    public List<string> options;
    public string correctAnswer;

    public AudioClip customClientAudio;
    public AudioClip customQuestionAudio;
    public AudioClip customAnswerAudio;
}

[Serializable]
public class ClientDialogSequence
{
    public List<DialogStep> steps;
}

public class DialogWithRecordingActivity : ActivityBase
{
    public CustomerSpawner customerSpawner;
    public VoiceRecorder voiceRecorder;
    public List<ClientDialogSequence> clientesDialogo;

    private List<GameObject> clientesInstanciados = new();
    private int clienteIndex = 0;
    private int stepIndex = 0;
    private ClientDialogSequence secuenciaActual;
    private GameObject clienteActual;
    private Client clienteData;
    public Transform checkoutPoint;
    public override void StartActivity()
    {
        base.StartActivity();
        ResetInstructions();
        ShowInstructionsPanel();

        InstanciarClientesEnFila();
    }

    private void InstanciarClientesEnFila()
    {
        cameraController.MoveToPosition("A7 3 Clientes", () =>
        {
            customerSpawner.SpawnClientesEnSecuencia(clientesDialogo.Count, checkoutPoint, (clientes) =>
            {
                clientesInstanciados = clientes;
            });

            UpdateInstructionOnce(0, () =>
            {
                cameraController.MoveToPosition("A7 Vista Cliente", () =>
                {
                    UpdateInstructionOnce(1, () =>
                    {
                        IniciarPrimerDialogo();
                    });
                });              
            });
        });       
    }

    private void IniciarPrimerDialogo()
    {
        clienteActual = clientesInstanciados[0];
        clienteData = clienteActual.GetComponent<Client>();

        clienteActual.GetComponent<CustomerMovement>().MoveToCheckout(() =>
        {
            secuenciaActual = clientesDialogo[clienteIndex];
            stepIndex = 0;
            MostrarPasoActual();
        });
    }

    private void MostrarPasoActual()
    {
        var paso = secuenciaActual.steps[stepIndex];

        bool tieneOpciones = !(string.IsNullOrEmpty(paso.question) || paso.options == null || paso.options.Count == 0);

        if (!tieneOpciones)
        {
            DialogSystem.Instance.ShowClientDialog(
                clienteData,
                paso.clientText,
                paso.customClientAudio,
                ContinuarConDialogo
            );
            return;
        }

        DialogSystem.Instance.ShowClientDialog(
            clienteData,
            paso.clientText,
            paso.customClientAudio,
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    paso.question,
                    paso.options,
                    paso.correctAnswer,
                    paso.customQuestionAudio,
                    onCorrect: () =>
                    {
#if UNITY_WEBGL
                    // 🔁 En WebGL, omitir voiceRecorder y pasar de frente
                    if (paso.customAnswerAudio != null)
                    {
                        SoundManager.Instance.PlaySound(paso.customAnswerAudio);
                        DOVirtual.DelayedCall(paso.customAnswerAudio.length + 0.2f, ContinuarConDialogo).SetUpdate(true);
                    }
                    else
                    {
                        ContinuarConDialogo();
                    }
#else
                        if (paso.customAnswerAudio != null)
                        {
                            SoundManager.Instance.PlaySound(paso.customAnswerAudio);
                            DOVirtual.DelayedCall(paso.customAnswerAudio.length + 0.2f, () =>
                            {
                                voiceRecorder.SetTextoARepetir(paso.correctAnswer);
                                voiceRecorder.ResetRecorder();
                                SoundManager.Instance.LowerMusicVolume();
                                voiceRecorder.gameObject.SetActive(true);
                                voiceRecorder.OnRecordingFinished = ContinuarConDialogo;
                            }).SetUpdate(true);
                        }
                        else
                        {
                            voiceRecorder.SetTextoARepetir(paso.correctAnswer);
                            voiceRecorder.ResetRecorder();
                            SoundManager.Instance.LowerMusicVolume();
                            voiceRecorder.gameObject.SetActive(true);
                            voiceRecorder.OnRecordingFinished = ContinuarConDialogo;
                        }
#endif
                    },
                    onIncorrect: () =>
                    {
                        SoundManager.Instance.PlaySound("error");
                    }
                );
            }
        );
    }

    private void ContinuarConDialogo()
    {
        SoundManager.Instance.RestoreMusicVolume();
        voiceRecorder.gameObject.SetActive(false);
        stepIndex++;

        if (stepIndex < secuenciaActual.steps.Count)
        {
            MostrarPasoActual();
        }
        else
        {
            clienteActual.GetComponent<CustomerMovement>().MoveToExit(() =>
            {
                clienteIndex++;
                if (clienteIndex < clientesDialogo.Count)
                {
                    clienteActual = clientesInstanciados[clienteIndex];
                    clienteData = clienteActual.GetComponent<Client>();
                    secuenciaActual = clientesDialogo[clienteIndex];
                    stepIndex = 0;

                    clienteActual.GetComponent<CustomerMovement>().MoveToPosition(
                        checkoutPoint.position,
                        clienteActual.GetComponent<CustomerMovement>().rotationClient,
                        -1f,
                        MostrarPasoActual
                    );
                }
                else
                {
                    SoundManager.Instance.PlaySound("win");
                    UpdateInstructionOnce(2, CompleteActivity);
                }
            });
        }
    }

    protected override void Initialize() { }
}
