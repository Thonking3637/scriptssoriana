using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CustomNavigationManager : MonoBehaviour
{
    public EventSystem eventSystem;
    private TMP_InputField activeInputField;

    public void SetActiveInputField(TMP_InputField inputField)
    {
        if (inputField != null)
        {
            activeInputField = inputField;
            eventSystem.SetSelectedGameObject(inputField.gameObject);
            inputField.ActivateInputField();
        }
        else
        {
            Debug.LogWarning("Se intent� activar un InputField nulo.");
        }
    }

    public TMP_InputField GetActiveInputField()
    {
        return activeInputField;
    }
}
