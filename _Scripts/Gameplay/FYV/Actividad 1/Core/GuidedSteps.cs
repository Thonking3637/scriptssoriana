using UnityEngine;

public enum GuidedAction { OpenBox, RotateItem, MarkDefect, PressEvaluate, ConfirmDecision }

[CreateAssetMenu(fileName = "A1_GuidedSteps", menuName = "Recibo/A1 Guided Steps")]
public class GuidedSteps : ScriptableObject
{
    [System.Serializable] public class Step { public string instructionKey; public GuidedAction action; public int repeatCount = 1; }
    public Step[] steps;
}
