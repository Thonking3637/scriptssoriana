using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine.UI;

public class CommandManager : MonoBehaviour
{
    public GameManager gameManager;
    public CustomNavigationManager navigationManager;

    [Serializable]
    public struct CommandAction
    {
        public string command;
        public Action customAction;
        public GameObject panelToActivate;
        public string requiredActivity;
        public List<Button> commandButtons;
    }

    public List<CommandAction> commandList = new List<CommandAction>();

    private List<string> currentCommand = new List<string>();
    private float commandTimer = 0f;
    private bool commandActive = false;
    private CommandAction activeCommand;

    [Header("Settings")]
    public float commandTimeLimit = 1.0f;

    /*
    [Header("UI Debugging")]
    public TextMeshProUGUI debugText;
    */

    public event Action<string> OnCommandExecuted;
    public event Action OnEnterPressed;

    private void Update()
    {
        if (commandActive)
        {
            commandTimer += Time.deltaTime;
            if (commandTimer >= commandTimeLimit)
            {
                ResetCommand();
            }
        }
    }

    public void OnButtonPressed(string buttonName)
    {
        OnCommandExecuted?.Invoke(buttonName);

        // 🔹 Si el botón presionado es un comando registrado, ejecutarlo inmediatamente
        foreach (var command in commandList)
        {
            if (command.command == buttonName)
            {
                ExecuteCommand(command);
                return;
            }
        }

        if (buttonName == "ENTER")
        {
            HandleEnter();
            return;
        }

        // 🔹 Si no es un comando registrado, proceder con las acciones normales
        switch (buttonName)
        {
            case "BORRAR":
                HandleDelete(); // 🔹 Solo se ejecutará si "BORRAR" NO es un comando registrado
                break;
            default:
                if (IsSingleCharacter(buttonName))
                {
                    HandleTextInput(buttonName);
                }
                else
                {
                    OnCommandInput(buttonName);
                }
                break;
        }
    }
    /*
    private void OnCommandInput(string buttonName)
    {
        if (commandList.Count == 0)
        {
            Debug.LogWarning("No commands registered.");
            return;
        }

        if (!commandActive)
        {
            foreach (var command in commandList)
            {
                if (command.command.StartsWith(buttonName))
                {
                    commandActive = true;
                    commandTimer = 0f;
                    currentCommand.Clear();
                    currentCommand.Add(buttonName);
                    currentButtonIndex = 0;
                    activeCommand = command;

                    Debug.Log($"Command started: {buttonName}");
                    return;
                }
            }
        }
        else
        {
            currentCommand.Add(buttonName);
            commandTimer = 0f;

            string currentInput = string.Join("+", currentCommand);
            Debug.Log($"Current command input: {currentInput}");

            if (currentInput == activeCommand.command)
            {
                ExecuteCommand(activeCommand);
                return;
            }

            if (!IsCommandStillValid())
            {
                Debug.Log("Invalid command sequence. Resetting.");
                ResetCommand();
            }
        }
    }
    */
    private void OnCommandInput(string buttonName)
    {
        if (commandList.Count == 0)
        {
            Debug.LogWarning("No commands registered.");
            return;
        }

        if (!commandActive)
        {
            foreach (var command in commandList)
            {
                if (command.command == buttonName) // 🔹 Si el comando es de un solo botón, ejecutarlo inmediatamente
                {
                    ExecuteCommand(command);
                    return;
                }
                else if (command.command.StartsWith(buttonName))
                {
                    commandActive = true;
                    commandTimer = 0f;
                    currentCommand.Clear();
                    currentCommand.Add(buttonName);
                    activeCommand = command;

                    Debug.Log($"Command started: {buttonName}");
                    return;
                }
            }
        }
        else
        {
            currentCommand.Add(buttonName);
            commandTimer = 0f;

            string currentInput = string.Join("+", currentCommand);
            Debug.Log($"Current command input: {currentInput}");

            if (currentInput == activeCommand.command)
            {
                ExecuteCommand(activeCommand);
                return;
            }

            if (!IsCommandStillValid())
            {
                Debug.Log("Invalid command sequence. Resetting.");
                ResetCommand();
            }
        }
    }

    private bool IsCommandStillValid()
    {
        string currentInput = string.Join("+", currentCommand);
        foreach (var command in commandList)
        {
            if (command.command.StartsWith(currentInput))
            {
                return true;
            }
        }
        return false;
    }

    private void ExecuteCommand(CommandAction command)
    {
        if (gameManager != null && gameManager.GetCurrentActivityName() == command.requiredActivity)
        {
            command.customAction?.Invoke();

            if (command.panelToActivate != null)
            {
                command.panelToActivate.SetActive(true);
            }
        }
        else
        {
            Debug.Log($"Command {command.command} cannot be executed in activity: {gameManager.GetCurrentActivityName()}.");
        }

        ResetCommand();
    }

    private void ResetCommand()
    {
        commandActive = false;
        commandTimer = 0f;
        currentCommand.Clear();
        //UpdateDebugText("Command reset.");
    }

    public void OnButtonPressedWrapper(string buttonName)
    {
        OnButtonPressed(buttonName);
    }

    private void HandleEnter()
    {
        if (commandActive && activeCommand.command.Contains("ENTER"))
        {
            activeCommand.customAction?.Invoke();
        }
        else
        {
            OnEnterPressed?.Invoke();
        }
    }

    public void RegisterEnterAction(Action enterAction)
    {
        OnEnterPressed = enterAction;
    }

    private void HandleDelete()
    {
        if (navigationManager != null)
        {
            TMP_InputField activeInputField = navigationManager.GetActiveInputField();
            if (activeInputField != null && activeInputField.text.Length > 0)
            {
                activeInputField.text = activeInputField.text.Substring(0, activeInputField.text.Length - 1);
            }
        }
    }
    public void ClearCommands()
    {
        commandList.Clear();
        ResetCommand();
    }

    private bool IsSingleCharacter(string key)
    {
        return key.Length == 1 && char.IsLetterOrDigit(key[0]);
    }

    private void HandleTextInput(string key)
    {
        if (navigationManager != null)
        {
            TMP_InputField activeInputField = navigationManager.GetActiveInputField();
            if (activeInputField != null && IsSingleCharacter(key))
            {
                activeInputField.text += key;
            }
        }
    }
}
