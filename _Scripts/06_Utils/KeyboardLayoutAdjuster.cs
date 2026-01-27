using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Ajusta la posición del panel cuando aparece el teclado virtual,
/// enfocando el InputField actualmente seleccionado.
/// </summary>
public class KeyboardLayoutAdjuster : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El panel que se moverá cuando aparezca el teclado")]
    [SerializeField] private RectTransform panelToAdjust;

    [Tooltip("Canvas padre (se busca automáticamente si no se asigna)")]
    [SerializeField] private Canvas parentCanvas;

    [Header("Configuración")]
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float animationSpeed = 12f;

    [Tooltip("Espacio extra sobre el InputField activo")]
    [SerializeField] private float inputFieldPadding = 30f;

    [Tooltip("Límite máximo de desplazamiento (0 = sin límite)")]
    [SerializeField] private float maxOffset = 300f;

    // Estado interno
    private float _originalY;
    private float _targetY;
    private bool _isAdjusting;
    private Coroutine _checkCoroutine;
    private int _lastKeyboardHeight;
    private TMP_InputField _currentInput;
    private RectTransform _canvasRect;

    private void Awake()
    {
        if (panelToAdjust == null)
            panelToAdjust = GetComponent<RectTransform>();

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null)
            _canvasRect = parentCanvas.GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _originalY = panelToAdjust.anchoredPosition.y;
        _targetY = _originalY;
        _lastKeyboardHeight = 0;
        _currentInput = null;

        _checkCoroutine = StartCoroutine(CheckKeyboardRoutine());
    }

    private void OnDisable()
    {
        if (_checkCoroutine != null)
        {
            StopCoroutine(_checkCoroutine);
            _checkCoroutine = null;
        }

        ResetPosition();
    }

    private void Update()
    {
        if (!_isAdjusting) return;

        var pos = panelToAdjust.anchoredPosition;
        pos.y = Mathf.Lerp(pos.y, _targetY, Time.unscaledDeltaTime * animationSpeed);
        panelToAdjust.anchoredPosition = pos;

        if (Mathf.Abs(pos.y - _targetY) < 0.5f)
        {
            pos.y = _targetY;
            panelToAdjust.anchoredPosition = pos;
            _isAdjusting = false;
        }
    }

    private IEnumerator CheckKeyboardRoutine()
    {
        var wait = new WaitForSecondsRealtime(checkInterval);

        while (true)
        {
            int keyboardHeight = GetKeyboardHeight();
            TMP_InputField activeInput = GetActiveInputField();

            // Detectar cambios
            bool keyboardChanged = keyboardHeight != _lastKeyboardHeight;
            bool inputChanged = activeInput != _currentInput;

            if (keyboardChanged || inputChanged)
            {
                _lastKeyboardHeight = keyboardHeight;
                _currentInput = activeInput;
                AdjustForKeyboard(keyboardHeight, activeInput);
            }

            yield return wait;
        }
    }

    private void AdjustForKeyboard(int keyboardHeight, TMP_InputField activeInput)
    {
        // Si no hay teclado, volver a posición original
        if (keyboardHeight <= 100)
        {
            _targetY = _originalY;
            _isAdjusting = true;
            return;
        }

        // Si no hay input activo, usar ajuste genérico
        if (activeInput == null)
        {
            float canvasScale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
            float genericOffset = (keyboardHeight / canvasScale) * 0.3f;

            if (maxOffset > 0)
                genericOffset = Mathf.Min(genericOffset, maxOffset);

            _targetY = _originalY + genericOffset;
            _isAdjusting = true;
            return;
        }

        // Calcular posición del InputField en pantalla
        RectTransform inputRect = activeInput.GetComponent<RectTransform>();

        // Obtener posición del InputField en coordenadas de pantalla
        Vector3[] corners = new Vector3[4];
        inputRect.GetWorldCorners(corners);

        // Corner[0] = bottom-left, Corner[1] = top-left
        float inputBottomScreen = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;

        // Área visible (pantalla - teclado)
        float visibleAreaTop = Screen.height;
        float visibleAreaBottom = keyboardHeight;

        // ¿El input está cubierto por el teclado?
        float safeMargin = inputFieldPadding * (parentCanvas != null ? parentCanvas.scaleFactor : 1f);
        float inputBottomWithMargin = inputBottomScreen - safeMargin;

        if (inputBottomWithMargin < visibleAreaBottom)
        {
            // Necesitamos subir el panel
            float pixelsToMove = visibleAreaBottom - inputBottomWithMargin;
            float canvasScale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
            float offset = pixelsToMove / canvasScale;

            // Aplicar límite máximo
            if (maxOffset > 0)
                offset = Mathf.Min(offset, maxOffset);

            _targetY = _originalY + offset;
        }
        else
        {
            // El input ya es visible, volver a posición original
            _targetY = _originalY;
        }

        _isAdjusting = true;

#if UNITY_EDITOR
        Debug.Log($"[Keyboard] Input: {activeInput.name}, ScreenY: {inputBottomScreen}, KeyboardTop: {visibleAreaBottom}, Offset: {_targetY - _originalY}");
#endif
    }

    private TMP_InputField GetActiveInputField()
    {
        // Obtener el objeto seleccionado actualmente
        GameObject selected = EventSystem.current?.currentSelectedGameObject;

        if (selected == null)
            return null;

        // Verificar si es un TMP_InputField
        return selected.GetComponent<TMP_InputField>();
    }

    private void ResetPosition()
    {
        _targetY = _originalY;

        if (panelToAdjust != null)
        {
            var pos = panelToAdjust.anchoredPosition;
            pos.y = _originalY;
            panelToAdjust.anchoredPosition = pos;
        }

        _isAdjusting = false;
        _lastKeyboardHeight = 0;
        _currentInput = null;
    }

    private int GetKeyboardHeight()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return GetKeyboardHeightAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
        return (int)TouchScreenKeyboard.area.height;
#else
        // En Editor: mantener K para simular teclado
        if (Input.GetKey(KeyCode.K))
            return 600;
        return 0;
#endif
    }

#if UNITY_ANDROID
    private int GetKeyboardHeightAndroid()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return 0;

                var window = activity.Call<AndroidJavaObject>("getWindow");
                if (window == null) return 0;

                var decorView = window.Call<AndroidJavaObject>("getDecorView");
                if (decorView == null) return 0;

                var rootView = decorView.Call<AndroidJavaObject>("getRootView");
                if (rootView == null) return 0;

                using (var rect = new AndroidJavaObject("android.graphics.Rect"))
                {
                    rootView.Call("getWindowVisibleDisplayFrame", rect);

                    int screenHeight = Screen.height;
                    int visibleHeight = rect.Call<int>("height");
                    int keyboardHeight = screenHeight - visibleHeight;

                    // Filtrar barra de navegación
                    if (keyboardHeight > screenHeight * 0.15f)
                        return keyboardHeight;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Keyboard] Error: {ex.Message}");
        }

        return 0;
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Simular Teclado Activo")]
    private void SimulateKeyboard()
    {
        AdjustForKeyboard(600, null);
    }

    [ContextMenu("Resetear Posición")]
    private void ResetPositionMenu()
    {
        ResetPosition();
    }
#endif
}