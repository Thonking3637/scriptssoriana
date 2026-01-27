using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ButtonAutoSetup : MonoBehaviour
{
    public CommandManager commandManager;
    public List<Transform> buttonContainers = new List<Transform>(); // Lista de contenedores de botones
    public List<string> buttonNames = new List<string>(); // Lista de nombres personalizados

    public void SetupButtons()
    {
        if (commandManager == null)
        {
            Debug.LogError("CommandManager is not assigned.");
            return;
        }

        if (buttonContainers.Count == 0)
        {
            Debug.LogError("No button containers assigned.");
            return;
        }

        if (buttonNames.Count == 0)
        {
            Debug.LogError("No button names provided.");
            return;
        }

        int totalButtons = 0;
        int nameIndex = 0;

        foreach (Transform container in buttonContainers)
        {
            foreach (Transform buttonTransform in container)
            {
                Button button = buttonTransform.GetComponent<Button>();

                if (button != null && nameIndex < buttonNames.Count)
                {
                    string buttonName = buttonNames[nameIndex];
                    buttonTransform.gameObject.name = buttonName; // Renombrar el botón en la jerarquía
                    nameIndex++;
                    totalButtons++;
                }
            }
        }

        Debug.Log($"Renamed {totalButtons} buttons successfully. Now assigning OnClick events via Editor script.");
    }
}
