// IntroPuertasSequence.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class IntroPuertasSequence : MonoBehaviour
{
    [Header("Cámara / Jugador")]
    public Transform cameraTransform;

    [Header("Waypoints (A, B, C, D, E...)")]
    public List<Transform> puntos = new List<Transform>();
    [Tooltip("Velocidad de caminata en m/s")]
    public float velocidad = 1.2f;
    public Ease easeCaminar = Ease.Linear;

    [System.Serializable]
    public class AperturaProgramada
    {
        [Tooltip("Puerta a abrir (par de hojas)")]
        public SlidingDoor puerta;
        [Tooltip("Abrir ANTES de caminar del punto i al i+1")]
        public int indiceSegmento;
        [Tooltip("Pausa antes de caminar (por puerta)")]
        public float delayAntesDeCaminar = 0.25f;
    }

    [Header("Puertas programadas")]
    public List<AperturaProgramada> aperturas = new List<AperturaProgramada>();

    [Tooltip("Si está activo, las puertas con el mismo segmento se abren una tras otra en el orden de la lista.")]
    public bool abrirPuertasSecuencial = true;

    [Header("UI / Final")]
    public GameObject menuPanel;
    public GameObject extraPanel; // opcional
    public GameObject loginPanel;
    public Button botonSkip;

    [Header("UI Switcher (Opcional pero recomendado)")]
    public UISwitcher uiSwitcher;

    [Header("Sonidos (opcional)")]
    public string loopPasos = "pasos";
    public string sfxPuerta = "puerta_auto";

    [Header("Repetición")]
    public bool recordarSiYaSeVio = true;
    public string prefsKeyIntroVisto = "IntroPuertas_VistoUnaVez";

    [Header("Skip / Fade")]
    public bool usarFadeAlSaltar = true;
    [Tooltip("CanvasGroup con Image negra full screen (alpha inicial 0)")]
    public CanvasGroup fadePanel;
    public float fadeInTime = 0.25f;
    public float fadeOutTime = 0.25f;

    [Header("Skip opciones")]
    [Tooltip("Abrir puertas instantáneamente al saltar para evitar clipping")]
    public bool abrirPuertasAlSaltar = true;

    bool introActiva;
    Coroutine seqRoutine;
    bool isSkipping;

    void Start()
    {
        if (uiSwitcher == null) uiSwitcher = FindObjectOfType<UISwitcher>(true);

        bool yaVisto = recordarSiYaSeVio && PlayerPrefs.GetInt(prefsKeyIntroVisto, 0) == 1;

        if (botonSkip)
        {
            botonSkip.gameObject.SetActive(yaVisto);
            botonSkip.onClick.RemoveAllListeners();
            botonSkip.onClick.AddListener(SaltarIntro);
        }

        // Estado UI inicial: apaga menú/extra (login lo mostrará FinalizarYMostrarLogin)
        if (menuPanel) menuPanel.SetActive(false);
        if (extraPanel) extraPanel.SetActive(false);
        if (loginPanel) loginPanel.SetActive(false);

        seqRoutine = StartCoroutine(PlaySequence(yaVisto));
    }

    IEnumerator PlaySequence(bool mostrarSkipDesdeInicio)
    {
        if (cameraTransform == null || puntos.Count < 2)
        {
            FinalizarYMostrarLogin();
            yield break;
        }

        introActiva = true;

        if (botonSkip && !mostrarSkipDesdeInicio)
        {
            botonSkip.gameObject.SetActive(false);
            yield return new WaitForSeconds(1.5f);
            botonSkip.gameObject.SetActive(true);
        }

        // Cerrar puertas al inicio (instantáneo)
        foreach (var ap in aperturas) ap.puerta?.Cerrar(true);

        for (int i = 0; i < puntos.Count - 1; i++)
        {
            // Abrir puertas configuradas para este tramo (índice i)
            var abrirAhora = aperturas.Where(a => a.indiceSegmento == i).ToList();
            if (abrirAhora.Count > 0)
            {
                if (abrirPuertasSecuencial)
                {
                    foreach (var ap in abrirAhora)
                    {
                        if (ap.puerta != null)
                        {
                            if (!string.IsNullOrEmpty(sfxPuerta) && SoundManager.Instance != null)
                                SoundManager.Instance.PlaySound(sfxPuerta);

                            ap.puerta.Abrir();

                            float wait = Mathf.Max(0f, ap.delayAntesDeCaminar) + Mathf.Max(0f, ap.puerta.duracion);
                            if (wait > 0f) yield return new WaitForSeconds(wait);
                        }
                    }
                }
                else
                {
                    foreach (var ap in abrirAhora)
                    {
                        if (ap.puerta != null)
                        {
                            if (!string.IsNullOrEmpty(sfxPuerta) && SoundManager.Instance != null)
                                SoundManager.Instance.PlaySound(sfxPuerta);
                            ap.puerta.Abrir();
                        }
                    }

                    float wait = 0f;
                    foreach (var ap in abrirAhora)
                        wait = Mathf.Max(wait, Mathf.Max(0f, ap.delayAntesDeCaminar) + (ap.puerta != null ? Mathf.Max(0f, ap.puerta.duracion) : 0f));

                    if (wait > 0f) yield return new WaitForSeconds(wait);
                }
            }

            // Caminar i -> i+1
            float dist = Vector3.Distance(puntos[i].position, puntos[i + 1].position);
            float dur = Mathf.Max(0.01f, dist / Mathf.Max(0.05f, velocidad));

            // orientar hacia el siguiente punto
            Quaternion rot = Quaternion.LookRotation((puntos[i + 1].position - cameraTransform.position).normalized, Vector3.up);
            cameraTransform.DORotateQuaternion(rot, 0.3f).SetEase(Ease.InOutSine);

            if (!string.IsNullOrEmpty(loopPasos) && SoundManager.Instance != null)
                SoundManager.Instance.PlayLoop(loopPasos, 0.7f);

            Tween t = cameraTransform.DOMove(puntos[i + 1].position, dur).SetEase(easeCaminar);
            yield return t.WaitForCompletion();

            if (!string.IsNullOrEmpty(loopPasos) && SoundManager.Instance != null)
                SoundManager.Instance.StopLoop(loopPasos);
        }

        introActiva = false;
        if (recordarSiYaSeVio) PlayerPrefs.SetInt(prefsKeyIntroVisto, 1);

        if (botonSkip) botonSkip.gameObject.SetActive(false);
        FinalizarYMostrarLogin();
    }

    private void FinalizarYMostrarLogin()
    {
        // Si hay UISwitcher, úsalo para evitar estados inválidos entre paneles
        if (uiSwitcher != null)
        {
            bool logged = (SessionContext.Instance != null && SessionContext.Instance.IsLoggedIn);

            if (logged)
            {
                uiSwitcher.SwitchTo(menuPanel);
                if (extraPanel) extraPanel.SetActive(false); // no lo usas
                return;
            }

            uiSwitcher.SwitchTo(loginPanel);
            if (extraPanel) extraPanel.SetActive(false);
        }
        else
        {
            // Fallback: tu lógica original con SetActive
            if (menuPanel) menuPanel.SetActive(false);
            if (extraPanel) extraPanel.SetActive(false);
            if (loginPanel) loginPanel.SetActive(false);

            if (SessionContext.Instance != null && SessionContext.Instance.IsLoggedIn)
            {
                MostrarMenuFinal();
                return;
            }

            if (loginPanel) loginPanel.SetActive(true);
        }

        if (recordarSiYaSeVio)
            PlayerPrefs.SetInt(prefsKeyIntroVisto, 1);
    }

    // Si tu UI tiene un botón “Continuar” en login/registro que te manda al menú:
    public void OnUserAcceptedName()
    {
        if (uiSwitcher != null)
        {
            uiSwitcher.SwitchTo(menuPanel);
            if (extraPanel) extraPanel.SetActive(false);
        }
        else
        {
            if (loginPanel) loginPanel.SetActive(false);
            MostrarMenuFinal();
        }
    }

    private void MostrarMenuFinal()
    {
        if (menuPanel) menuPanel.SetActive(true);

        // extraPanel es opcional; si no lo usas, déjalo null y listo
        if (extraPanel)
        {
            extraPanel.SetActive(true);
            // aquí tu animación si aplica
        }
    }

    public void SaltarIntro()
    {
        if (!introActiva || isSkipping) return;
        isSkipping = true;

        if (botonSkip) botonSkip.interactable = false;

        if (seqRoutine != null)
        {
            StopCoroutine(seqRoutine);
            seqRoutine = null;
        }

        StartCoroutine(SkipWithFade());
    }

    IEnumerator SkipWithFade()
    {
        if (botonSkip) botonSkip.gameObject.SetActive(false);

        // FADE IN (a negro)
        if (usarFadeAlSaltar && fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            fadePanel.DOKill();
            yield return fadePanel.DOFade(1f, fadeInTime).WaitForCompletion();
        }

        // Detener tweens en curso y audio de pasos
        DOTween.Kill(cameraTransform);
        if (!string.IsNullOrEmpty(loopPasos) && SoundManager.Instance != null)
            SoundManager.Instance.StopLoop(loopPasos);

        // Abrir puertas al instante (opcional)
        if (abrirPuertasAlSaltar)
            foreach (var ap in aperturas) ap.puerta?.Abrir(true);

        // Teletransportar al último punto
        if (puntos.Count > 0)
        {
            var last = puntos[puntos.Count - 1];
            cameraTransform.position = last.position;
            cameraTransform.rotation = last.rotation;
        }

        introActiva = false;
        if (recordarSiYaSeVio) PlayerPrefs.SetInt(prefsKeyIntroVisto, 1);

        // Mostrar UI final antes del fade-out
        FinalizarYMostrarLogin();

        // FADE OUT
        if (usarFadeAlSaltar && fadePanel != null)
        {
            fadePanel.DOKill();
            yield return fadePanel.DOFade(0f, fadeOutTime).WaitForCompletion();
        }

        if (botonSkip) botonSkip.interactable = true;
        isSkipping = false;
    }
}
