using UnityEngine;
using System;

public class GuidedCoach : MonoBehaviour
{
    public GuidedSteps stepsAsset;
    int stepIdx, repeatCounter;
    public bool Active { get; private set; }

    // Eventos que debe escuchar desde la actividad
    public Action<string> OnSayInstruction; // usa tu InstructionsManager
    public Action<bool> OnAllowUI;          // habilitar/deshabilitar botones seg�n paso

    public void Begin()
    {
        Active = true; stepIdx = 0; repeatCounter = 0;
        EmitInstruction();
    }

    void EmitInstruction()
    {
        if (!Active) return;
        var step = stepsAsset.steps[stepIdx];
        OnSayInstruction?.Invoke(step.instructionKey);
        // Ej: OnAllowUI(false) para bloquear botones hasta la accion requerida
    }

    // Llama estos metodos desde la actividad cuando la accion suceda:
    public void ReportOpenBox() => TryAdvance(GuidedAction.OpenBox);
    public void ReportRotate() => TryAdvance(GuidedAction.RotateItem);
    public void ReportMarkDefect() => TryAdvance(GuidedAction.MarkDefect);
    public void ReportPressEvaluate() => TryAdvance(GuidedAction.PressEvaluate);
    public void ReportConfirm() => TryAdvance(GuidedAction.ConfirmDecision);

    void TryAdvance(GuidedAction done)
    {
        if (!Active) return;
        var step = stepsAsset.steps[stepIdx];
        if (step.action != done) return;

        repeatCounter++;
        if (repeatCounter < Mathf.Max(1, step.repeatCount)) return;

        // paso completado
        repeatCounter = 0;
        stepIdx++;
        if (stepIdx >= stepsAsset.steps.Length) { Active = false; return; }
        EmitInstruction();
    }
}
