using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class CashRegisterActivationActivity : ActivityBase
{
    [Header("UI Elements")]
    public GameObject loginPanel;
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;

    [Header("Botones de Animación")]
    public List<Button> commandButtons;
    public List<Button> loginButtons;
    public List<Button> passwordButtons;

    [Header("Interacción con Botón")]
    public GameObject interactionButton;

    [Header("Configuración de Material")]
    public MeshRenderer objectToChangeMaterial;
    public Material newMaterial; 
    public int materialIndex = 3;

    [Header("Panel Success")]
    public Button continueButton;


    [Header("Brillo (URP)")]
    [Range(0, 1f)] public float shinySmoothness = 0.9f; 
    [Range(0, 1f)] public float shinyMetallic = 0.0f;  
    [Min(0f)] public float emissionIntensity = 2.5f;
    public float tweenDuration = 0.5f;


    protected override void Initialize() { }

    protected override void Start()
    {
        interactionButton.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        InitializeCommands();

        cameraController.MoveToPosition("Iniciando Vista Monitor", () =>
        {
            UpdateInstructionOnce(0, () =>
            {
                cameraController.MoveToPosition("Vista Botonera", () =>
                {
                    UpdateInstructionOnce(1,() =>
                    {
                        interactionButton.SetActive(true);
                    });
                });
            });
        });

    }

    public void OnInteractionComplete()
    {
        SoundManager.Instance.PlaySound("bubble");

        cameraController.MoveToPosition("Vista Poste", () =>
        {
            ChangeColor(() => {
                SoundManager.Instance.PlaySound("success");
                UpdateInstructionOnce(2, () =>
                {
                    cameraController.MoveToPosition("Actividad Login Botones",() =>
                    {
                        UpdateInstructionOnce(3, () =>
                        {
                            AnimateButtonsSequentiallyWithActivation(commandButtons);
                        });                        
                    });
                });
            });
        });
    }

    public void ChangeColor(System.Action onComplete = null)
    {
        if (!objectToChangeMaterial || !newMaterial)
        {
            Debug.LogError("Falta asignar objectToChangeMaterial o newMaterial.");
            return;
        }

        var mats = objectToChangeMaterial.materials;
        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            Debug.LogError($"Índice fuera de rango. El objeto tiene {mats.Length} materiales.");
            return;
        }

        var inst = new Material(newMaterial);

        int ID_BaseMap = Shader.PropertyToID("_BaseMap");
        int ID_MainTex = Shader.PropertyToID("_MainTex");
        var old = mats[materialIndex];

        if (inst.HasProperty(ID_BaseMap) && !inst.GetTexture(ID_BaseMap) && old && old.HasProperty(ID_BaseMap))
            inst.SetTexture(ID_BaseMap, old.GetTexture(ID_BaseMap));
        else if (inst.HasProperty(ID_MainTex) && !inst.GetTexture(ID_MainTex) && old && old.HasProperty(ID_MainTex))
            inst.SetTexture(ID_MainTex, old.GetTexture(ID_MainTex));

        mats[materialIndex] = inst;
        objectToChangeMaterial.materials = mats;

        SoundManager.Instance?.PlaySound("flip");
        onComplete?.Invoke();
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        CommandManager.CommandAction successCommand = new CommandManager.CommandAction
        {
            command = "CONTROL+ALT+SHIFT+DELETE",
            customAction = ActivateLoginPanel,
            panelToActivate = loginPanel,
            requiredActivity = "Day1_AltaCaja",
            commandButtons = commandButtons
        };
        commandManager.commandList.Add(successCommand);
    }
    private void ActivateLoginPanel()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Iniciar Sesion", () =>
        {
            UpdateInstructionOnce(4, () =>
            {
                usernameField.text = "";
                commandManager.navigationManager.SetActiveInputField(usernameField);
                usernameField.DeactivateInputField();
                usernameField.ActivateInputField();

                AnimateButtonsSequentiallyWithActivation(loginButtons, () => 
                {
                    LoginHandle();
                });
            });      
        });
    }
    public void LoginHandle()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(5, () =>
        {
            commandManager.navigationManager.SetActiveInputField(passwordField);
            passwordField.DeactivateInputField();
            passwordField.ActivateInputField();
            AnimateButtonsSequentiallyWithActivation(passwordButtons, () =>
            {
                PasswordHandle();
            });
        });       
    }

    public void PasswordHandle()
    {
        SoundManager.Instance.PlaySound("success");
        ActivityComplete();
    }
    
    public void ActivityComplete()
    {
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad Cash Successfull", () =>
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
}
