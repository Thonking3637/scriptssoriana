using UnityEngine;
using TMPro;

public class InputFieldSelector: MonoBehaviour
{
    public GameObject monitor;
    public CommandManager commandManager;

    private void Start()
    {
        commandManager = FindObjectOfType<CommandManager>();

        if (monitor != null)
        {
            foreach (Transform child in monitor.transform)
            {
                TMP_InputField[] inputFields = child.GetComponentsInChildren<TMP_InputField>();

                foreach (TMP_InputField inputField in inputFields)
                {
                    inputField.onSelect.AddListener((eventData) => OnInputFieldSelected(inputField));
                }
            }
        }
        else
        {
            Debug.LogError("Monitor no asignado.");
        }
    }

    public void OnInputFieldSelected(TMP_InputField inputField)
    {
        if (inputField == null)
        {
            Debug.LogError("InputField es nulo.");
            return;
        }

        if (commandManager != null)
        {
            commandManager.navigationManager.SetActiveInputField(inputField);
        }
        else
        {
            Debug.LogError("CommandManager no encontrado.");
        }
    }
}
