using System;
using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// IntroManager mejorado
/// 
/// FEATURES:
/// - Skip: Tocar pantalla o presionar tecla salta la intro
/// - Sonido typewriter: Sonido opcional por cada N letras
/// - Fade In + Out: Fade in al inicio, fade out al final
/// - Pausas naturales: Pausa más larga en puntos, comas, etc.
/// - Rich text: Soporta tags como <color>, <b>, etc.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class IntroManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("UI")]
    public CanvasGroup fadeCanvas;
    public TextMeshProUGUI introText;

    [Header("Timing")]
    [Tooltip("Duración del fade in/out")]
    public float fadeDuration = 1.5f;

    [Tooltip("Tiempo que se muestra el mensaje completo antes del fade out")]
    public float messageDuration = 2f;

    [Tooltip("Velocidad base del typewriter (segundos por caracter)")]
    public float typingSpeed = 0.05f;

    [Header("Pausas Naturales")]
    [Tooltip("Pausa extra después de comas")]
    public float commaDelay = 0.15f;

    [Tooltip("Pausa extra después de puntos")]
    public float periodDelay = 0.3f;

    [Tooltip("Pausa extra después de signos de interrogación/exclamación")]
    public float exclamationDelay = 0.25f;

    [Header("Skip")]
    [Tooltip("Permitir saltar la intro")]
    public bool allowSkip = true;

    [Tooltip("Tecla para saltar (PC)")]
    public KeyCode skipKey = KeyCode.Space;

    [Tooltip("También saltar con click/touch")]
    public bool skipOnTouch = true;

    [Header("Audio")]
    [Tooltip("Sonido de typewriter (opcional)")]
    public AudioClip typeSound;

    [Tooltip("Volumen del sonido")]
    [Range(0f, 1f)]
    public float typeSoundVolume = 0.5f;

    [Tooltip("Reproducir sonido cada N caracteres (1 = cada letra, 2 = cada 2 letras)")]
    [Range(1, 5)]
    public int playSoundEveryNChars = 2;

    [Header("Fade In (Opcional)")]
    [Tooltip("Hacer fade in antes de mostrar el texto")]
    public bool useFadeIn = false;

    [Tooltip("Duración del fade in")]
    public float fadeInDuration = 0.5f;

    // ═══════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ═══════════════════════════════════════════════════════════════════════════

    private Action _onIntroComplete;
    private bool _isPlaying = false;
    private bool _skipRequested = false;
    private bool _isTyping = false;
    private string _fullMessage = "";
    private AudioSource _audioSource;
    private int _charCounter = 0;

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Configurar canvas
        if (fadeCanvas)
        {
            var canvas = fadeCanvas.GetComponentInParent<Canvas>();
            if (canvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
            }
            fadeCanvas.gameObject.SetActive(true);
            fadeCanvas.alpha = 1f;
            fadeCanvas.blocksRaycasts = true;
            fadeCanvas.interactable = false;
        }

        if (introText)
            introText.text = "";

        // Crear AudioSource si hay sonido configurado
        if (typeSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.clip = typeSound;
            _audioSource.volume = typeSoundVolume;
        }
    }

    private void Update()
    {
        if (!_isPlaying || !allowSkip) return;

        // Detectar skip por tecla
        if (Input.GetKeyDown(skipKey))
        {
            RequestSkip();
        }

        // Detectar skip por touch/click
        if (skipOnTouch && (Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            RequestSkip();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Muestra la intro con el mensaje especificado
    /// </summary>
    public void ShowIntro(string message, Action onComplete)
    {
        if (!fadeCanvas)
        {
            onComplete?.Invoke();
            return;
        }

        _fullMessage = message;
        _onIntroComplete = onComplete;
        _skipRequested = false;
        _isPlaying = true;

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.alpha = 1f;
        fadeCanvas.blocksRaycasts = true;

        if (introText)
            introText.text = "";

        StopAllCoroutines();
        StartCoroutine(PlayIntroSequence(message));
    }

    /// <summary>
    /// Salta la intro inmediatamente
    /// </summary>
    public void Skip()
    {
        RequestSkip();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SECUENCIA PRINCIPAL
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator PlayIntroSequence(string message)
    {
        // Frame de espera inicial
        yield return null;

        // Fade In (opcional)
        if (useFadeIn && fadeInDuration > 0)
        {
            fadeCanvas.alpha = 0f;
            yield return StartCoroutine(FadeCanvasRealtime(1f, fadeInDuration));
        }

        // Typewriter
        _isTyping = true;
        yield return StartCoroutine(TypeTextRealtime(message));
        _isTyping = false;

        // Si no se saltó, esperar antes del fade out
        if (!_skipRequested)
        {
            float waited = 0f;
            while (waited < messageDuration && !_skipRequested)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Fade Out
        yield return StartCoroutine(FadeCanvasRealtime(0f, _skipRequested ? fadeDuration * 0.3f : fadeDuration));

        // Finalizar
        _isPlaying = false;
        fadeCanvas.gameObject.SetActive(false);
        _onIntroComplete?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TYPEWRITER
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator TypeTextRealtime(string message)
    {
        if (!introText) yield break;

        introText.text = "";
        _charCounter = 0;

        // Procesar el mensaje (manejar rich text)
        int i = 0;
        while (i < message.Length)
        {
            // Si se pidió skip, mostrar todo y salir
            if (_skipRequested)
            {
                introText.text = message;
                yield break;
            }

            char c = message[i];

            // Detectar y saltar tags de rich text completos
            if (c == '<')
            {
                int closeIndex = message.IndexOf('>', i);
                if (closeIndex != -1)
                {
                    // Agregar el tag completo sin delay
                    string tag = message.Substring(i, closeIndex - i + 1);
                    introText.text += tag;
                    i = closeIndex + 1;
                    continue;
                }
            }

            // Agregar caracter
            introText.text += c;
            i++;

            // Reproducir sonido
            if (!char.IsWhiteSpace(c))
            {
                if (_charCounter == 0 || _charCounter >= playSoundEveryNChars)
                {
                    PlayTypeSound();
                    _charCounter = 0;
                }
                _charCounter++;
            }

            // Calcular delay
            float delay = GetDelayForChar(c);
            if (delay > 0)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
        }
    }

    /// <summary>
    /// Calcula el delay después de un caracter (pausas naturales)
    /// </summary>
    private float GetDelayForChar(char c)
    {
        // Pausas naturales según puntuación
        switch (c)
        {
            case '.':
            case '。': // Punto japonés
                return typingSpeed + periodDelay;

            case ',':
            case '、': // Coma japonesa
            case ';':
            case ':':
                return typingSpeed + commaDelay;

            case '!':
            case '?':
            case '¡':
            case '¿':
                return typingSpeed + exclamationDelay;

            case ' ':
                return typingSpeed * 0.5f; // Espacios más rápidos

            case '\n':
                return typingSpeed + periodDelay; // Salto de línea = pausa

            default:
                return typingSpeed;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDIO
    // ═══════════════════════════════════════════════════════════════════════════

    private void PlayTypeSound()
    {
        if (_audioSource != null && typeSound != null)
        {
            // Pequeña variación de pitch para que no suene repetitivo
            _audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            _audioSource.PlayOneShot(typeSound, typeSoundVolume);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FADE
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator FadeCanvasRealtime(float targetAlpha, float duration)
    {
        if (!fadeCanvas || duration <= 0f)
        {
            if (fadeCanvas) fadeCanvas.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = fadeCanvas.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Skip acelera el fade
            if (_skipRequested && targetAlpha == 0f)
            {
                duration = Mathf.Min(duration, 0.3f);
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease out para fade más suave
            t = 1f - Mathf.Pow(1f - t, 2f);

            fadeCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvas.alpha = targetAlpha;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SKIP
    // ═══════════════════════════════════════════════════════════════════════════

    private void RequestSkip()
    {
        if (!_isPlaying || _skipRequested) return;

        _skipRequested = true;

        // Si está escribiendo, mostrar todo el texto inmediatamente
        if (_isTyping && introText != null)
        {
            introText.text = _fullMessage;
        }

        Debug.Log("[IntroManager] Skip solicitado");
    }
}