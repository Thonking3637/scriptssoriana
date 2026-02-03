using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// SupervisorVerification - Actividad de Verificación con Supervisor (Alta de Caja)
/// 
/// FLUJO:
/// 1. Presionar SUPER → Llega supervisor
/// 2. Diálogo con opciones → Responder correctamente
/// 3. Supervisor va al teclado → Ingresar contraseña
/// 4. Supervisor se va → Completar actividad
/// 
/// TIPO DE EVALUACIÓN: AccuracyBased
/// - Único punto de error: Responder incorrectamente al diálogo
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: AccuracyBased
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 0 (se calcula automático)
/// 
/// INSTRUCCIONES:
/// 0 = Presionar SUPER
/// 1 = Supervisor llega
/// 2 = Supervisor va al teclado
/// 3 = Ingresar contraseña
/// </summary>
public class SupervisorVerification : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - SUPERVISOR
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Supervisor")]
    [SerializeField] private GameObject supervisorPrefab;
    [SerializeField] private Transform supervisorSpawnPoint;
    [SerializeField] private Transform supervisorEntryPoint;
    [SerializeField] private List<Transform> supervisorMiddlePath;
    [SerializeField] private Transform supervisorExitPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - PANELES Y CONTRASEÑA
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Panel y Contraseña")]
    [SerializeField] private GameObject supervisorPasswordPanel;
    [SerializeField] private GameObject containerPanel;
    [SerializeField] private TextMeshProUGUI passwordText;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Botones")]
    [SerializeField] private List<Button> superCommandButtons;
    [SerializeField] private Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE CÁMARA
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_INIT = "Actividad 6 Iniciando";
    private const string CAM_SUPERVISOR_1 = "Mirada Supervisor 1";
    private const string CAM_SUPERVISOR_2 = "Mirada Supervisor 2";
    private const string CAM_PASSWORD = "Actividad 4 Supervisor Contraseña";
    private const string CAM_SUCCESS = "Actividad 6 Success";

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject _currentSupervisor;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores - Se incrementa cuando el usuario responde incorrectamente.
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos - Se incrementa cuando el usuario responde correctamente.
    /// </summary>
    private int _successCount = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        ResetMetrics();
        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            cameraController.MoveToPosition(CAM_INIT);
            AnimateButtonsSequentiallyWithActivation(superCommandButtons, SpawnSupervisor);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in superCommandButtons)
            button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER",
            requiredActivity = "Day3_SupervisorVerification",
            commandButtons = superCommandButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetMetrics()
    {
        _errorCount = 0;
        _successCount = 0;
    }

    private void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[SupervisorVerification] Error registrado. Total: {_errorCount}");
    }

    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[SupervisorVerification] Acierto registrado. Total: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 1: SPAWN SUPERVISOR
    // ══════════════════════════════════════════════════════════════════════════════

    private void SpawnSupervisor()
    {
        SoundManager.Instance.PlaySound("success");
        _currentSupervisor = Instantiate(
            supervisorPrefab,
            supervisorSpawnPoint.position,
            supervisorPrefab.transform.rotation
        );

        SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
        ConfigureSupervisorMovement(movement);

        UpdateInstructionOnce(1, () =>
        {
            cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
            {
                movement.GoToEntryPoint(() =>
                {
                    cameraController.MoveToPosition(CAM_SUPERVISOR_2, ShowSupervisorDialog);
                });
            });
        });
    }

    private void ConfigureSupervisorMovement(SupervisorMovement movement)
    {
        movement.entryPoint = supervisorEntryPoint;
        movement.middlePath = supervisorMiddlePath;
        movement.exitPoint = supervisorExitPoint;
        movement.animator = _currentSupervisor.GetComponent<Animator>();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 2: DIÁLOGO CON SUPERVISOR
    // ══════════════════════════════════════════════════════════════════════════════

    private void ShowSupervisorDialog()
    {
        DialogSystem.Instance.ShowClientDialog(
            _currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                        "Terminé mi turno, quiero dar el alta de caja.",
                        "Te llamé porque quise.",
                        "Hola, ¿cómo estás?",
                        "Ya me quiero ir, ¿puedo?"
                    },
                    "Terminé mi turno, quiero dar el alta de caja.",
                    OnCorrectSupervisorAnswer,
                    RegisterError
                );
            });
    }

    private void OnCorrectSupervisorAnswer()
    {
        RegisterSuccess();

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentSupervisor.GetComponent<Client>(),
                "Entiendo, lo haremos en este momento.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            UpdateInstructionOnce(2, () =>
                            {
                                cameraController.MoveToPosition(CAM_PASSWORD, HandleChangePassword);
                            });
                        });
                    });
                });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 3: CONTRASEÑA
    // ══════════════════════════════════════════════════════════════════════════════

    private void HandleChangePassword()
    {
        supervisorPasswordPanel.SetActive(true);

        AnimatePasswordEntry(() =>
        {
            SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
            movement.GoToExit(() => Debug.Log("[SupervisorVerification] Supervisor se va"));

            supervisorPasswordPanel.SetActive(false);

            if (containerPanel != null)
                containerPanel.SetActive(false);

            ActivityComplete();
        });
    }

    private void AnimatePasswordEntry(System.Action onComplete)
    {
        passwordText.text = "";
        string[] sequence = { "1", "2", "3", "4" };
        int index = 0;

        DOVirtual.DelayedCall(0.3f, () =>
        {
            AddDigit();

            void AddDigit()
            {
                if (index >= sequence.Length)
                {
                    DOVirtual.DelayedCall(0.3f, () => onComplete?.Invoke());
                    return;
                }

                passwordText.text += "*";
                index++;
                DOVirtual.DelayedCall(0.3f, AddDigit);
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    private void ActivityComplete()
    {
        commandManager.commandList.Clear();

        cameraController.MoveToPosition(CAM_SUCCESS, () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                adapter.NotifyActivityCompleted();
            }
            else
            {
                ShowManualSuccessPanel();
            }
        });
    }

    private void ShowManualSuccessPanel()
    {
        SoundManager.Instance.PlaySound("win");

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            cameraController.MoveToPosition(CAM_START);
            CompleteActivity();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        if (continueButton != null)
            continueButton.onClick.RemoveAllListeners();
    }
}