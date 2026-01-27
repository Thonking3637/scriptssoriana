using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class SupervisorVerification : ActivityBase
{
    [Header("Supervisor")]
    public GameObject supervisorPrefab;
    public Transform supervisorSpawnPoint;
    public Transform supervisorEntryPoint;
    public List<Transform> supervisorMiddlePath;
    public Transform supervisorExitPoint;
    private GameObject currentSupervisor;

    [Header("Panel & Password")]
    public GameObject supervisorPasswordPanel;
    public GameObject containerPanel;
    public TextMeshProUGUI passwordText;

    [Header("Bot�n o Comando")]
    public List<Button> superCommandButtons;
    public Button continueButton;

    public override void StartActivity()
    {
        base.StartActivity();
        InitializeCommands();
        UpdateInstructionOnce(0, () =>
        {
            cameraController.MoveToPosition("Actividad 6 Iniciando");
            AnimateButtonsSequentiallyWithActivation(superCommandButtons, SpawnSupervisor);
        });
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();
        foreach (var button in superCommandButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER",
            customAction = SpawnSupervisor,
            requiredActivity = "Day6_SupervisorPassword",
            commandButtons = superCommandButtons
        });
    }

    private void SpawnSupervisor()
    {
        GameObject supervisorGO = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, supervisorPrefab.transform.rotation);
        currentSupervisor = supervisorGO;

        SupervisorMovement movement = supervisorGO.GetComponent<SupervisorMovement>();

        movement.entryPoint = supervisorEntryPoint;
        movement.middlePath = supervisorMiddlePath;
        movement.exitPoint = supervisorExitPoint;
        movement.animator = supervisorGO.GetComponent<Animator>();

        UpdateInstructionOnce(1, () =>
        {
            cameraController.MoveToPosition("Mirada Supervisor 1", () =>
            {
                movement.GoToEntryPoint(() =>
                {
                    cameraController.MoveToPosition("Mirada Supervisor 2", () =>
                    {
                        ShowSupervisorDialog();
                    });
                });
            });
        });
    }

    private void ShowSupervisorDialog()
    {
        DialogSystem.Instance.ShowClientDialog(
            currentSupervisor.GetComponent<Client>(),
            "�Qu� sucedi�? Cu�ntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "�Qu� le debes decir a la supervisora?",
                    new List<string>
                    {
                    "Termin� mi turno, quiero dar la alta de caja",
                    "Ese cliente est� loco",
                    "No entiendo qu� quiere",
                    "No s� para qu� vino"
                    },
                    "Termin� mi turno, quiero dar la alta de caja",
                    OnCorrectSupervisorAnswer,
                    OnWrongSupervisorAnswer
                );
            });
    }
    private void OnWrongSupervisorAnswer()
    {
        SoundManager.Instance.PlaySound("error");
    }

    private void OnCorrectSupervisorAnswer()
    {
        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentSupervisor.GetComponent<Client>(),
                "Entiendo, lo haremos en este momento.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition("Mirada Supervisor 1", () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            UpdateInstructionOnce(2, () =>
                            {
                                cameraController.MoveToPosition("Actividad 4 Supervisor Contrase�a", () =>
                                {
                                    HandleChangePassword();
                                });
                            });
                        });
                    });
                });
        });
    }

    private void HandleChangePassword()
    {
        UpdateInstructionOnce(3, () =>
        {
            supervisorPasswordPanel.SetActive(true);
            AnimatePasswordEntry(() =>
            {
                SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();
                movement.GoToExit(() => Debug.Log("Cliente se va"));
                supervisorPasswordPanel.SetActive(false);
                containerPanel.SetActive(false);
                ActivityComplete();
            });
        });
    }

    private void ActivityComplete()
    {
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad 6 Success", () =>
        {
            continueButton.onClick.RemoveAllListeners();
            SoundManager.Instance.RestorePreviousMusic();
            SoundManager.Instance.PlaySound("win");
            continueButton.onClick.AddListener(() =>
            {
                cameraController.MoveToPosition("Iniciando Juego");
                CompleteActivity();
            });
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

    protected override void Initialize() { }
}
