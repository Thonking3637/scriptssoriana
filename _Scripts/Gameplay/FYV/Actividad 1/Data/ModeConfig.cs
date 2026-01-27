using UnityEngine;

[CreateAssetMenu(fileName = "A1_ModeConfig", menuName = "Recibo/A1 Mode Config")]
public class ModeConfig : ScriptableObject
{
    [Header("Ayudas y feedback")]
    public bool showHighlights = true;
    public bool feedbackImmediate = true;
    public bool showPerBoxSummary = true;
    public bool showFinalSummaryOnly = false;

    [Header("Flujo")]
    public bool allowRepeatBox = true;
    public bool showInstructionsPanel = true;
}
