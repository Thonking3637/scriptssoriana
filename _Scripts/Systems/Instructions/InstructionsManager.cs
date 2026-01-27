using UnityEngine;
using TMPro;
using DG.Tweening;

public class InstructionsManager : MonoBehaviour
{
    public static InstructionsManager Instance;

    [Header("UI Element")]
    public TextMeshProUGUI instructionText;
    public CanvasGroup instructionPanel;

    private string[] instructions;
    private AudioClip[] instructionSounds;
    private int currentInstructionIndex = 0;
    private bool isVisible = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetInstructions(string[] newInstructions, AudioClip[] newSounds)
    {
        instructions = newInstructions;
        instructionSounds = newSounds;
        currentInstructionIndex = 0;

        if (instructions.Length > 0)
        {
            //Debug.Log($"📜 Nueva instrucción: {instructions[0]}");
            ShowInstructions();
            UpdateInstructionText();
        }
        else
        {
            instructionText.text = "";
        }
    }
    public void NextInstruction()
    {
        if (instructions == null || instructions.Length == 0) return;

        SoundManager.Instance.StopInstructionSound(); // Detiene sonido actual
        currentInstructionIndex++;

        if (currentInstructionIndex < instructions.Length)
        {
            UpdateInstructionText();
        }
        else
        {
            HideInstructions();
        }
    }

    private void UpdateInstructionText()
    {
        instructionText.text = instructions[currentInstructionIndex];

        // Reproduce el sonido correspondiente a la instrucción
        if (instructionSounds != null && currentInstructionIndex < instructionSounds.Length)
        {
            SoundManager.Instance.PlayInstructionSound(instructionSounds[currentInstructionIndex]);
        }
    }

    /// <summary>
    /// 🔹 Oculta el panel de instrucciones con animación.
    /// </summary>
    public void HideInstructions()
    {
        if (!isVisible) return;
        isVisible = false;
        instructionPanel.DOFade(0, 0.5f).OnComplete(() => instructionPanel.gameObject.SetActive(false));
    }

    /// <summary>
    /// 🔹 Muestra el panel de instrucciones con animación.
    /// </summary>
    public void ShowInstructions()
    {
        instructionPanel.gameObject.SetActive(true);
        instructionPanel.alpha = 0f;
        instructionPanel.DOFade(1, 0.5f);
        isVisible = true;
    }

}
