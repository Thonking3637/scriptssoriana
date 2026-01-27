using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System;

public class SuspensionReactivacion : ActivityBase
{
    [Header("Cliente y Productos")]
    public CustomerSpawner customerSpawner;
    public Transform productSpawnPoint;
    public ProductScanner scanner;

    [Header("UI")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public TextMeshProUGUI passwordText;

    [Header("Buttons")]
    public List<Button> superPressedButton;
    public List<Button> superPressedButton2;
    public Button continueButton;

    [Header("Panel Reactivar/Activar")]
    public GameObject superPanel;
    public Button suspendButton;
    public Button reactivateButton;
    public GameObject supervisorPasswordPanel;

    [Header("Supervisor")]
    public GameObject supervisorPrefab;
    public Transform supervisorSpawnPoint;
    public Transform supervisorEntryPoint;
    public List<Transform> supervisorMiddlePath;
    public Transform supervisorExitPoint;
    private GameObject currentSupervisor;
    
    [Header("Ticket")]
    public GameObject ticketPrefab;
    public GameObject ticketPrefab2;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;

    [Header("Ticket S")]
    public Transform ticketSFirst;
    public Transform ticketSLast;

    private GameObject currentCustomer;
    private Client currentClient;
    private GameObject currentProduct;
    private int scannedCount = 0;
    private const int maxProducts = 4;

    private List<DragObject> scannedProducts = new();
    private int lastProductIndex = -1;

    private string savedProductText;
    private string savedTotalText;
    private string suspendedCustomerPrefabName;
    public override void StartActivity()
    {
        base.StartActivity();
        RegenerateProductValues();

        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();
        InitializeCommands();
        UpdateInstructionOnce(0, () =>
        {
            SpawnCustomer();
        });
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in superPressedButton) button.gameObject.SetActive(false);
        foreach (var button in superPressedButton2) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER",
            customAction = HandleSuperPressed,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = superPressedButton
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER2",
            customAction = HandleSuperPressed2,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = superPressedButton2
        });

    }

    private void SpawnCustomer()
    {
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomer.GetComponent<CustomerMovement>().MoveToCheckout(() =>
            {
                UpdateInstructionOnce(1, SpawnNextProduct);
            });
        });
    }

    private void SpawnNextProduct()
    {
        if (scannedCount >= maxProducts) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref lastProductIndex);
        if (prefab == null) return;

        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (currentProduct == null) return;

        currentProduct.transform.position = productSpawnPoint.position;
        currentProduct.transform.rotation = prefab.transform.rotation;
        currentProduct.transform.SetParent(null);
        currentProduct.SetActive(true);

        DragObject drag = currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);
    }

    public void RegisterProductScanned()
    {
        if (currentProduct == null) return;

        DragObject drag = currentProduct.GetComponent<DragObject>();

        if (drag != null)
        {
            scannedProducts.Add(drag);
        }

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, currentProduct.name, currentProduct);
        currentProduct = null;
        scannedCount++;

        if (scannedCount < maxProducts)
        {
            SpawnNextProduct();
        }
        else
        {
            TriggerClientComplaint();
        }
    }

    private void TriggerClientComplaint()
    {
        UpdateInstructionOnce(2, () =>
        {
            cameraController.MoveToPosition("Cliente Camera", () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    currentClient,
                    "Ay no, olvidé mi tarjeta, necesito salir un momento.",
                    () =>
                    {
                        CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
                        if (movement != null)
                        {
                            suspendedCustomerPrefabName = currentCustomer.name.Replace("(Clone)", "").Trim();
                            movement.MoveToExit(() =>
                            {
                                cameraController.MoveToPosition("Actividad 4 Super", () =>
                                {
                                    UpdateInstructionOnce(3, () => 
                                    {
                                        ActivateButtonWithSequence(superPressedButton, 0, HandleSuperPressed);
                                    });                                  
                                });
                            });
                        }
                    });
            });
        });     
    }

    public void HandleSuperPressed()
    {
        superPanel.SetActive(true);
        suspendButton.interactable = false;
        reactivateButton.interactable = false;

        cameraController.MoveToPosition("Actividad 4 Presionar Suspender", () =>
        {
            UpdateInstructionOnce(4, () =>
            {
                suspendButton.interactable = true;
                reactivateButton.interactable = false;
                suspendButton.onClick.RemoveAllListeners();
                suspendButton.onClick.AddListener(() =>
                {
                    superPanel.SetActive(false);
                    SpawnSupervisor();
                });
            });
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

        UpdateInstructionOnce(5, () =>
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
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                    "El cliente se fue, necesito suspender la cuenta",
                    "Ese cliente está loco",
                    "No entiendo qué quiere",
                    "No sé para qué vino"
                    },
                    "El cliente se fue, necesito suspender la cuenta",
                    OnCorrectSupervisorAnswer,
                    OnWrongSupervisorAnswer
                );
            });
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
                            UpdateInstructionOnce(6, () =>
                            {
                                cameraController.MoveToPosition("Actividad 4 Supervisor Contraseña", () =>
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
        UpdateInstructionOnce(7, () =>
        {
            supervisorPasswordPanel.SetActive(true);
            AnimatePasswordEntry(() =>
            {
                SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();
                movement.GoToExit(() =>
                {
                    movement.gameObject.SetActive(false);
                });
                SaveData();
                scanner.ClearUI();
                supervisorPasswordPanel.SetActive(false);                
                cameraController.MoveToPosition("Actividad 4 Mirar Ticket", () =>
                {                  
                    UpdateInstructionOnce(8, () =>
                    {
                        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
                    });
                });

            });
        });
    }

    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");

        if (string.IsNullOrEmpty(suspendedCustomerPrefabName)) return;

        currentCustomer = customerSpawner.SpawnCustomerByName(suspendedCustomerPrefabName);

        if (currentCustomer == null) return;

        currentClient = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomer.GetComponent<CustomerMovement>().MoveToCheckout(() =>
            {
                UpdateInstructionOnce(9, () =>
                {
                    ShowReactivationDialog();
                });
                
            });
        });
    }

    private void ShowReactivationDialog()
    {
        cameraController.MoveToPosition("Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentClient,
                "Hola ya volví, fue super rápido, ¿podemos continuar?",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo debes responder?",
                        new List<string>
                        {
                            "Claro, déjeme reactivar su cuenta",
                            "No, debe volver otro día",
                            "Tiene que hablar con un gerente",
                            "Esa información no la manejo"
                        },
                        "Claro, déjeme reactivar su cuenta",
                        OnCorrectReactivationAnswer,
                        OnWrongSupervisorAnswer
                    );
                });
        });
    }

    private void OnCorrectReactivationAnswer()
    {
        UpdateInstructionOnce(10, () =>
        {
            cameraController.MoveToPosition("Actividad 4 Super", () =>
            {
                ActivateButtonWithSequence(superPressedButton2, 0, HandleSuperPressed2);
            });
        });     
    }

    public void HandleSuperPressed2()
    {
        superPanel.SetActive(true);
        reactivateButton.interactable = false;
        suspendButton.interactable = false;

        cameraController.MoveToPosition("Actividad 4 Presionar Suspender", () =>
        {
            UpdateInstructionOnce(11, () =>
            {
                reactivateButton.interactable = true;
                suspendButton.interactable = false;

                reactivateButton.onClick.RemoveAllListeners();
                reactivateButton.onClick.AddListener(() =>
                {
                    superPanel.SetActive(false);
                    SpawnSupervisor2();
                });
            });
        });
    }
    private void SpawnSupervisor2()
    {
        GameObject supervisorGO = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, supervisorPrefab.transform.rotation);
        currentSupervisor = supervisorGO;

        SupervisorMovement movement = supervisorGO.GetComponent<SupervisorMovement>();

        movement.entryPoint = supervisorEntryPoint;
        movement.middlePath = supervisorMiddlePath;
        movement.exitPoint = supervisorExitPoint;
        movement.animator = supervisorGO.GetComponent<Animator>();

        UpdateInstructionOnce(12, () =>
        {
            cameraController.MoveToPosition("Mirada Supervisor 1", () =>
            {
                movement.GoToEntryPoint(() =>
                {
                    cameraController.MoveToPosition("Mirada Supervisor 2", () =>
                    {
                        ShowSupervisorDialog2();
                    });
                });
            });
        });
    }

    private void ShowSupervisorDialog2()
    {
        DialogSystem.Instance.ShowClientDialog(
            currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                    "El cliente regresó, necesito reactivar la cuenta",
                    "Ese cliente está loco",
                    "No entiendo qué quiere",
                    "No sé para qué vino"
                    },
                    "El cliente regresó, necesito reactivar la cuenta",
                    OnCorrectSupervisorAnswer2,
                    OnWrongSupervisorAnswer
                );
            });
    }

    private void OnCorrectSupervisorAnswer2()
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
                            cameraController.MoveToPosition("Actividad 4 Supervisor Contraseña", () =>
                            {
                                HandleChangePassword2();
                            });
                        });
                    });
                });
        });     

    }

    private void HandleChangePassword2()
    {
        UpdateInstructionOnce(13, () =>
        {
            supervisorPasswordPanel.SetActive(true);
            AnimatePasswordEntry(() =>
            {
                SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();
                movement.GoToExit(() => Debug.Log("Cliente se va"));
                supervisorPasswordPanel.SetActive(false);
                UpdateInstructionOnce(14, () =>
                {
                    SpawnReactivationTicket();
                });
            });
        });       
    }

    private void SpawnReactivationTicket()
    {
        cameraController.MoveToPosition("Actividad 4 Mirar Ticket", () =>
        {
            GameObject ticket = Instantiate(ticketPrefab2, ticketSFirst.position, ticketPrefab2.transform.rotation);
            ReactivateTicket ticketScript = ticket.GetComponent<ReactivateTicket>();
            ticketScript.Initialize(ticketSLast, ticketSFirst, RestoreSuspendedData);
        });
    }

    private void RestoreSuspendedData()
    {
        cameraController.MoveToPosition("Actividad 4 Restauracion", () =>
        {
            activityProductsText.text = savedProductText;
            activityTotalPriceText.text = savedTotalText;

            UpdateInstructionOnce(15, () =>
            {
                currentCustomer.GetComponent<CustomerMovement>().MoveToExit();
                    UpdateInstructionOnce(16, () =>
                    {
                        cameraController.MoveToPosition("Iniciando Juego", () =>
                        {
                            ActivityComplete();
                        });
                    });                 
            });
        });       
    }

    private void ActivityComplete()
    {
        scanner.ClearUI();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad 4 Success", () =>
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

    public void SaveData()
    {
        savedProductText = activityProductsText.text;
        savedTotalText = activityTotalPriceText.text;
    }

    private void AnimatePasswordEntry(Action onComplete)
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

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }


    private void OnWrongSupervisorAnswer()
    {
        SoundManager.Instance.PlaySound("error");
    }

    protected override void Initialize() { }

}