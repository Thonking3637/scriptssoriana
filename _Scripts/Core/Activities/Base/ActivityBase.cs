using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public abstract class ActivityBase: MonoBehaviour
{
    [Header("Configuración de Música")]
    public AudioClip activityMusicClip;

    public event Action OnActivityStart;
    public event Action OnActivityComplete;

    protected bool isComplete = false;

    [Header("Configuración de Instrucciones")]
    [TextArea(2, 5)]
    public string[] instrucciones;
    public List<AudioClip> instructionSounds;

    [Header("Componentes Globales")]
    protected InstructionsManager instructionsManager;
    protected SoundManager soundManager;
    protected SmoothCameraController cameraController;
    protected CommandManager commandManager;

    protected string[] productNames;

    [Header("Configuración de Actividades")]
    public string startCameraPosition;

    [Header("Timer de Actividad")]
    private float activityStartTime;
    private float elapsedTime;
    private bool isTimerRunning = false;

    [HideInInspector]
    public TextMeshProUGUI activityTimerText;
    [HideInInspector]
    public TextMeshProUGUI activityTimeText;

    private Coroutine timerCoroutine;

    private HashSet<int> executedInstructions = new HashSet<int>();
    private bool hasTimerStarted = false;

    [Header("Analytics (IDs estables)")]
    [SerializeField] private string moduleId;
    [SerializeField] private string activityId;

    private string _attemptId;
    private float _attemptStartRealtime;
    private bool _attemptOpen;

    protected virtual void Awake()
    {
        instructionsManager = InstructionsManager.Instance ?? FindObjectOfType<InstructionsManager>(true);
        soundManager = SoundManager.Instance ?? FindObjectOfType<SoundManager>(true);
        cameraController = FindObjectOfType<SmoothCameraController>(true);
        commandManager = FindObjectOfType<CommandManager>(true);
    }
    protected abstract void Initialize();

    protected virtual void Start()
    {
        if (instructionsManager == null)
            instructionsManager = InstructionsManager.Instance
                                  ?? FindObjectOfType<InstructionsManager>(true);
        if (soundManager == null)
            soundManager = SoundManager.Instance
                           ?? FindObjectOfType<SoundManager>(true);
    }

    public virtual void StartActivity()
    {
        if (isComplete) return;

        ResetRuntimeStateForStart();

        StartAttemptIfPossible();

        OnActivityStart?.Invoke();

        if (!string.IsNullOrEmpty(startCameraPosition))
        {
            if (cameraController != null)
                cameraController.InitializeCameraPosition(startCameraPosition);
        }

        if (commandManager != null)
            commandManager.ClearCommands();
    }

    private void ResetRuntimeStateForStart()
    {
        ResetInstructions();

        hasTimerStarted = false;
        isTimerRunning = false;
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        if (activityTimerText != null)
            activityTimerText.gameObject.SetActive(false);
    }

    public virtual void CompleteActivity()
    {
        if (isComplete) return;
        isComplete = true;

        EndAttemptIfOpen(completed: true, score: null, mistakes: null);

        ResetInstructions();
        ShowInstructionsPanel();
        OnActivityComplete?.Invoke();
    }

    protected void UpdateInstruction(int index, Action onComplete = null)
    {
        if (instrucciones == null || instrucciones.Length == 0)
        {
            Debug.LogError($"[ActivityBase] No hay instrucciones asignadas en {name}, index solicitado = {index}");
            onComplete?.Invoke();
            return;
        }

        if (index < 0 || index >= instrucciones.Length)
        {
            Debug.LogError($"[ActivityBase] Índice de instrucción fuera de rango en {name}. index = {index}, length = {instrucciones.Length}");
            onComplete?.Invoke();
            return;
        }

        if (instructionsManager != null)
        {
            instructionsManager.SetInstructions(
                new string[] { GetInstructionText(index) },
                instructionSounds != null && index < instructionSounds.Count ? new AudioClip[] { instructionSounds[index] } : new AudioClip[0]
            );
        }

        if (soundManager != null && instructionSounds != null && index < instructionSounds.Count)
        {
            soundManager.PlayInstructionSound(instructionSounds[index], onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    protected void UpdateInstructionOnce(int index, params Action[] actions)
    {
        if (executedInstructions.Contains(index))
        {
            foreach (var action in actions) action?.Invoke();
            return;
        }

        executedInstructions.Add(index);

        if (index == 0 && instructionsManager != null)
        {
            instructionsManager.ShowInstructions();
        }

        UpdateInstruction(index, () =>
        {
            foreach (var action in actions)
            {
                action?.Invoke();
            }

            if (instructionSounds != null &&
                instructionSounds.Count > 0 &&
                index == instructionSounds.Count - 1 &&
                instructionsManager != null)
            {
                instructionsManager.HideInstructions();
            }
        });
    }
    protected void ResetInstructions()
    {
        executedInstructions.Clear();
    }
    protected virtual string GetInstructionText(int index)
    {
        if (index >= 0 && index < instrucciones.Length)
        {
            return instrucciones[index];
        }
        return "";
    }

    protected void AnimateButtonsSequentially(List<Button> buttons, Action onComplete = null)
    {
        if (buttons == null || buttons.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        buttons[0].interactable = true;
        GrowButton(buttons, 0, onComplete);
    }

    protected void GrowButton(List<Button> buttons, int index, Action onComplete = null)
    {
        if (index >= buttons.Count)
        {
            onComplete?.Invoke();
            return;
        }

        buttons[index].transform.DOScale(Vector3.one * 3f, 0.3f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() =>
                {
                    Debug.Log("Se presionó el botón: " + buttons[index].name);
                    buttons[index].transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.InBack);
                    buttons[index].interactable = false;

                    if (index + 1 < buttons.Count)
                    {
                        buttons[index + 1].interactable = true;
                        GrowButton(buttons, index + 1, onComplete);
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }
                });
            });
    }

    protected void ResetButtons(List<Button> buttons)
    {
        foreach (var button in buttons)
        {
            button.transform.DOKill();
            button.transform.localScale = Vector3.one;
            button.interactable = false;
        }
    }
    protected void PlayInstructionsSequentially(int[] indices, Action onComplete = null)
    {
        if (indices == null || indices.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        PlayInstructionWithCallback(0, indices, onComplete);
    }

    private void PlayInstructionWithCallback(int currentIndex, int[] indices, Action onComplete)
    {
        if (currentIndex >= indices.Length)
        {
            onComplete?.Invoke();
            return;
        }

        int instructionIndex = indices[currentIndex];

        if (instructionsManager != null)
        {
            instructionsManager.SetInstructions(
                new string[] { GetInstructionText(instructionIndex) },
                instructionSounds != null && instructionIndex < instructionSounds.Count ? new AudioClip[] { instructionSounds[instructionIndex] } : new AudioClip[0]
            );
        }

        if (soundManager != null && instructionSounds != null && instructionIndex < instructionSounds.Count)
        {
            soundManager.PlayInstructionSound(instructionSounds[instructionIndex], () =>
            {
                PlayInstructionWithCallback(currentIndex + 1, indices, onComplete);
            });
        }
        else
        {
            PlayInstructionWithCallback(currentIndex + 1, indices, onComplete);
        }
    }
    protected void HideInstructionsPanel()
    {
        if (instructionsManager != null)
        {
            instructionsManager.HideInstructions();
        }
    }

    protected void ShowInstructionsPanel()
    {
        if (instructionsManager != null)
        {
            instructionsManager.ShowInstructions();
        }
    }
    protected void AnimateButtonsSequentiallyWithActivation(List<Button> buttons, Action onComplete = null)
    {
        if (buttons == null || buttons.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        foreach (var button in buttons)
        {
            button.gameObject.SetActive(false);
        }

        ActivateButtonWithSequence(buttons, 0, onComplete);
    }

    public void ActivateButtonWithSequence(List<Button> buttons, int index, Action onComplete = null)
    {
        if (index >= buttons.Count)
        {
            onComplete?.Invoke();
            return;
        }

        buttons[index].gameObject.SetActive(true);
        buttons[index].interactable = false; 

        buttons[index].transform.DOScale(Vector3.one * 3f, 0.3f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                buttons[index].interactable = true;
                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() =>
                {
                    buttons[index].transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.InBack);
                    buttons[index].interactable = false;
                    buttons[index].gameObject.SetActive(false);

                    if (index + 1 < buttons.Count)
                    {
                        ActivateButtonWithSequence(buttons, index + 1, onComplete);
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }
                });
            });
    }

    private void ActivateAndGrowButton(List<Button> buttons, int index)
    {
        if (index >= buttons.Count) return;

        buttons[index].gameObject.SetActive(true);

        buttons[index].transform.DOScale(Vector3.one * 3f, 0.1f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                buttons[index].interactable = true;

                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() =>
                {
                    Debug.Log("Se presionó el botón: " + buttons[index].name);

                    buttons[index].transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            buttons[index].gameObject.SetActive(false);

                            if (index + 1 < buttons.Count)
                            {
                                ActivateAndGrowButton(buttons, index + 1);
                            }
                        });

                    buttons[index].interactable = false;
                });
            });
    }
    public void StartActivityTimer()
    {
        if (hasTimerStarted)
        {
            return;
        }

        hasTimerStarted = true;

        activityStartTime = Time.time;
        isTimerRunning = true;

        if (activityTimerText != null)
        {
            activityTimerText.gameObject.SetActive(true);
        }

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        timerCoroutine = StartCoroutine(UpdateTimer());
    }
    public void StopActivityTimer()
    {
        if (isTimerRunning)
        {
            elapsedTime = Time.time - activityStartTime;
        }

        isTimerRunning = false;
        hasTimerStarted = false;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        if (activityTimerText != null)
            activityTimerText.gameObject.SetActive(false);

        if (activityTimeText != null)
            activityTimeText.text = $"Tiempo total: {elapsedTime:F2} s";
    }

    private IEnumerator UpdateTimer()
    {
        while (isTimerRunning)
        {
            float currentTime = Time.time - activityStartTime;
            if (activityTimerText != null)
            {
                activityTimerText.text = $"{currentTime:F2} s";
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    protected virtual void InitializeCommands() { }

    protected void ActivateCommandButtons(List<Button> buttons)
    {
        foreach (var button in buttons)
        {
            button.gameObject.SetActive(true);
        }
    }

    protected float GetTotalAmount(TextMeshProUGUI activityTotalPriceText)
    {
        if (activityTotalPriceText == null || string.IsNullOrWhiteSpace(activityTotalPriceText.text))
        {
            Debug.LogError("Error: El texto de activityTotalPriceText está vacío o es nulo.");
            return 0f;
        }

        string cleanText = activityTotalPriceText.text.Replace("$", "").Trim();

        if (float.TryParse(cleanText, out float total))
        {
            return total;
        }

        Debug.LogError($"Error al convertir el total. Contenido limpio: '{cleanText}'");
        return 0f;
    }

    protected GameObject SpawnNextProduct(int scannedCount, int productsToScan, List<GameObject> productPrefabs, Transform spawnPoint)
    {
        if (scannedCount >= productsToScan) return null;

        GameObject prefab = productPrefabs[scannedCount];
        string prefabName = prefab.name;

        GameObject pooledProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefabName);

        if (pooledProduct == null)
        {
            Debug.LogWarning($"No se pudo obtener el producto '{prefabName}' del pool.");
            return null;
        }

        pooledProduct.transform.position = spawnPoint.position;
        pooledProduct.transform.rotation = prefab.transform.rotation;
        pooledProduct.transform.SetParent(null);
        pooledProduct.SetActive(true);

        DragObject dragScript = pooledProduct.GetComponent<DragObject>();

        if (dragScript != null)
        {
            dragScript.SetOriginalPoolName(prefabName);
        }

        return pooledProduct;
    }

    protected void InstantiateTicket(GameObject ticketPrefab, Transform ticketSpawnPoint, Transform ticketTargetPoint, Action onTicketDelivered)
    {
        if (ticketPrefab == null || ticketSpawnPoint == null)
        {
            Debug.LogError("No se ha asignado el prefab del ticket o el punto de aparición.");
            return;
        }

        SoundManager.Instance.PlaySound("SE_Ticket");

        GameObject ticketInstance = Instantiate(ticketPrefab, ticketSpawnPoint.position, ticketPrefab.transform.rotation);
        Ticket ticketScript = ticketInstance.GetComponent<Ticket>();

        if (ticketScript != null)
        {
            ticketScript.Initialize(ticketTargetPoint);
            ticketScript.OnTicketDelivered += () => onTicketDelivered?.Invoke();
        }
        else
        {
            Debug.LogError("No se encontró el script Ticket en el prefab.");
        }
    }

    protected List<Button> GetButtonsForAmount(string amountString, List<Button> numberButtons)
    {
        List<Button> selectedButtons = new List<Button>();

        foreach (char digit in amountString)
        {
            int index = digit - '0';

            if (index >= 0 && index < numberButtons.Count)
            {
                selectedButtons.Add(numberButtons[index]);
            }
        }
        return selectedButtons;
    }
    protected void RegenerateProductValues()
    {
        List<GameObject> productPrefabs = ObjectPoolManager.Instance.GetAllUniquePrefabs(PoolTag.Producto);

        foreach (var prefab in productPrefabs)
        {
            Product productData = prefab.GetComponent<DragObject>()?.productData;
            if (productData != null)
            {
                productData.RegenerateValues();
            }
        }
    }
    protected GameObject GetPooledProduct(int index, Transform spawnPoint)
    {
        if (productNames == null || index >= productNames.Length) return null;

        string prefabName = productNames[index];
        GameObject pooled = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefabName);

        if (pooled != null)
        {
            pooled.transform.position = spawnPoint.position;
            pooled.transform.rotation = pooled.transform.rotation;
            pooled.transform.SetParent(null);
            pooled.SetActive(true);

            DragObject drag = pooled.GetComponent<DragObject>();
            drag?.SetOriginalPoolName(prefabName);
        }
        else
        {
            Debug.LogWarning($"No se pudo obtener el producto '{prefabName}' del pool.");
        }

        return pooled;
    }

    public void MostrarPanel(GameObject panel)
    {
        panel.SetActive(true);
        panel.transform.localScale = Vector3.zero;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;

        Sequence seq = DOTween.Sequence();
        seq.Append(panel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        if (cg != null) seq.Join(cg.DOFade(1f, 0.5f));
    }

    public void OcultarPanel(GameObject panel, Action onComplete = null)
    {
        var cg = panel.GetComponent<CanvasGroup>();
        Sequence seq = DOTween.Sequence();
        seq.Append(panel.transform.DOScale(0f, 0.4f).SetEase(Ease.InBack));
        if (cg != null) seq.Join(cg.DOFade(0f, 0.4f));
        seq.OnComplete(() => {
            panel.SetActive(false);
            onComplete?.Invoke();
        });
    }

    public string FormatearTipoBolsa(TipoBolsa tipo)
    {
        return System.Text.RegularExpressions.Regex
            .Replace(tipo.ToString(), "(\\B[A-Z])", " $1");
    }

    public void AnimateCanvas(GameObject go, float duration = 0.5f)
    {
        if (!go.activeSelf)
            go.SetActive(true);

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        go.transform.localScale = Vector3.one * 0.8f;

        cg.DOFade(1f, duration).SetEase(Ease.OutQuad);
        go.transform.DOScale(1f, duration).SetEase(Ease.OutBack);
    }

    public void HideCanvas(GameObject go, float duration = 0.3f)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            go.SetActive(false);
            return;
        }

        cg.DOFade(0f, duration).SetEase(Ease.InSine);
        go.transform.DOScale(0.8f, duration).SetEase(Ease.InBack).OnComplete(() =>
        {
            go.SetActive(false);
        });
    }

    private void StartAttemptIfPossible()
    {
        _attemptId = null;
        _attemptStartRealtime = Time.realtimeSinceStartup;
        _attemptOpen = true;

        if (ProgressService.Instance == null) return;
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(activityId)) return;

        ProgressService.Instance.StartAttempt(moduleId, activityId, id =>
        {
            _attemptId = id;
        });
    }

    protected void EndAttemptIfOpen(bool completed, int? score, int? mistakes)
    {
        if (!_attemptOpen) return;
        _attemptOpen = false;

        int durationSec = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - _attemptStartRealtime));

        if (ProgressService.Instance == null) return;
        if (string.IsNullOrWhiteSpace(_attemptId)) return;
        if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(activityId)) return;

        ProgressService.Instance.EndAttempt(_attemptId, moduleId, activityId, durationSec, completed, score, mistakes);
    }

    protected virtual void OnDisable()
    {
        if (!isComplete && _attemptOpen)
        {
            EndAttemptIfOpen(completed: false, score: null, mistakes: null);
        }
    }
}
