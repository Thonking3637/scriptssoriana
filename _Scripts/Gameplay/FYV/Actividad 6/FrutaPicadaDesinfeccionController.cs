using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
using TMPro;   // <- para Image / Text

/// <summary>
/// Zona de DESINFECCIÓN en jaba.
/// Flujo: Jaba → Agua → Desinfectante → Papaya limpia → Timer.
/// </summary>
public class FrutaPicadaDesinfeccionController : MonoBehaviour
{
    [Header("Refs")]
    public FrutaPicadaActivity activity;
    public PapayaVisualState papayaVisual;

    [Header("Objetos de jaba/agua")]
    public GameObject jabaVisual;
    public GameObject aguaEnJaba;
    public GameObject aguaConDesinfectante;

    [Header("Timer visual")]
    [Tooltip("Objeto que se enciende mientras dura el reposo (puede ser un panel, ícono, etc.).")]
    public GameObject timerVisual;
    [Tooltip("Duración del reposo en segundos (ej. 5 = 5 segundos).")]
    public float segundosReposo = 5f;

    [Tooltip("Si se asigna, se usará su fillAmount (0→1) como barra o círculo de progreso.")]
    public Image timerFillImage;
    [Tooltip("Texto opcional para mostrar cuenta regresiva.")]
    public TextMeshProUGUI timerText;

    [Header("Instrucciones (solo Tutorial)")]
    [Tooltip("Se dispara al colocar la JABA.")]
    public int I_Jaba_OK = -1;
    [Tooltip("Se dispara al aplicar AGUA en la jaba.")]
    public int I_Agua_OK = -1;
    [Tooltip("Se dispara al aplicar DESINFECTANTE.")]
    public int I_Desinfect_OK = -1;
    [Tooltip("Se dispara al colocar la PAPAYA en la jaba.")]
    public int I_Papaya_OK = -1;
    [Tooltip("Se dispara al INICIAR el TIMER de reposo.")]
    public int I_TimerStart_OK = -1;
    [Tooltip("Opcional: instrucción al COMPLETAR la zona de desinfección.")]
    public int I_ZonaCompletada_OK = -1;

    [Header("Debug")]
    public bool zonaActiva;

    public event Action OnZonaDesinfeccionCompletada;

    private bool pasoJaba;
    private bool pasoAgua;
    private bool pasoDesinfectante;
    private bool pasoPapaya; 
    private bool timerEnCurso;

    private Coroutine timerCR;

    public void ResetZona()
    {
        pasoJaba = pasoAgua = pasoDesinfectante = pasoPapaya = false;
        timerEnCurso = false;

        if (jabaVisual) jabaVisual.SetActive(false);
        if (aguaEnJaba) aguaEnJaba.SetActive(false);
        if (aguaConDesinfectante) aguaConDesinfectante.SetActive(false);
        if (timerVisual) timerVisual.SetActive(false);

        if (timerFillImage) timerFillImage.fillAmount = 0f;
        if (timerText) timerText.text = string.Empty;

        if (papayaVisual) papayaVisual.HideAll();

        if (timerCR != null)
        {
            StopCoroutine(timerCR);
            timerCR = null;
        }
    }

    public void SetZonaActiva(bool active)
    {
        zonaActiva = active;
        
        if (active)
        {
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Desinfeccion_Jaba);
        }
        else
        {
            FrutaUIButtonHighlight.ClearAll();
        }
    }

    void PlaySuccess()
    {
        SoundManager.Instance.PlaySound("success");
    }

    public void PlayError()
    {
        activity?.ReportError(1);
        SoundManager.Instance?.PlaySound("error");
    }

    // ─────────────────────────────────────────────
    // PASO 1: JABA
    // ─────────────────────────────────────────────
    public void TryColocarJaba()
    {
        if (!zonaActiva) return;

        if (!pasoJaba && !pasoAgua && !pasoDesinfectante && !pasoPapaya)
        {
            pasoJaba = true;
            if (jabaVisual) jabaVisual.SetActive(true);
            PlaySuccess();
            
            if (activity != null)
                activity.PlayInstructionFromButton(I_Jaba_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Desinfeccion_Agua);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 2: AGUA EN JABA
    // ─────────────────────────────────────────────
    public void TryAplicarAguaEnJaba()
    {
        if (!zonaActiva) return;

        if (pasoJaba && !pasoAgua && !pasoDesinfectante && !pasoPapaya)
        {
            pasoAgua = true;
            if (aguaEnJaba) aguaEnJaba.SetActive(true);
            if (aguaConDesinfectante) aguaConDesinfectante.SetActive(false);
            PlaySuccess();

            // Instrucción: Agua en jaba
            if (activity != null)
                activity.PlayInstructionFromButton(I_Agua_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Desinfeccion_Desinfectante);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 3: DESINFECTANTE
    // ─────────────────────────────────────────────
    public void TryAplicarDesinfectanteEnJaba()
    {
        if (!zonaActiva) return;

        if (pasoJaba && pasoAgua && !pasoDesinfectante && !pasoPapaya)
        {
            pasoDesinfectante = true;
            if (aguaEnJaba) aguaEnJaba.SetActive(false);
            if (aguaConDesinfectante) aguaConDesinfectante.SetActive(true);
            PlaySuccess();

            // Instrucción: Desinfectante
            if (activity != null)
                activity.PlayInstructionFromButton(I_Desinfect_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Desinfeccion_Papaya);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 4: PAPAYA EN JABA
    // ─────────────────────────────────────────────
    public void TryColocarPapayaEnJaba()
    {
        if (!zonaActiva) return;

        if (pasoJaba && pasoAgua && pasoDesinfectante && !pasoPapaya)
        {
            pasoPapaya = true;
            if (papayaVisual) papayaVisual.ShowEnJaba();
            PlaySuccess();

            // Instrucción: Papaya en jaba
            if (activity != null)
                activity.PlayInstructionFromButton(I_Papaya_OK, onlyInTutorial: true);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Desinfeccion_Timer);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 5: INICIAR TIMER
    // ─────────────────────────────────────────────
    public void TryIniciarTimer()
    {
        if (!zonaActiva) return;

        if (pasoJaba && pasoAgua && pasoDesinfectante && pasoPapaya && !timerEnCurso)
        {
            timerEnCurso = true;

            if (timerVisual) timerVisual.SetActive(true);
            if (timerFillImage) timerFillImage.fillAmount = 0f;
            if (timerText) timerText.text = segundosReposo.ToString("0");

            if (timerCR != null) StopCoroutine(timerCR);
            timerCR = StartCoroutine(TimerRoutine());

            PlaySuccess();

            // Instrucción: Inicio de reposo
            if (activity != null)
                activity.PlayInstructionFromButton(I_TimerStart_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.ClearAll();
        }
        else
        {
            PlayError();
        }
    }

    IEnumerator TimerRoutine()
    {
        float elapsed = 0f;

        while (elapsed < segundosReposo)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / segundosReposo);

            // Actualizar relleno si existe
            if (timerFillImage)
            {
                timerFillImage.fillAmount = t;
            }

            // Actualizar texto (segundos restantes) si existe
            if (timerText)
            {
                float restante = Mathf.Max(0f, segundosReposo - elapsed);
                timerText.text = Mathf.CeilToInt(restante).ToString("0");
            }

            yield return null;
        }

        timerEnCurso = false;

        if (timerVisual) timerVisual.SetActive(false);
        if (timerFillImage) timerFillImage.fillAmount = 1f;
        if (timerText) timerText.text = "0";

        CompletarZona();
    }

    void CompletarZona()
    {
        zonaActiva = false;

        // Instrucción opcional al completar la zona
        if (activity != null)
            activity.PlayInstructionFromButton(I_ZonaCompletada_OK, onlyInTutorial: true);

        OnZonaDesinfeccionCompletada?.Invoke();
    }
}
