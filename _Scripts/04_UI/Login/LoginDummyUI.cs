using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginDummyUI v2 - Con mejoras de UX:
/// - Validación visual de campos (highlight en rojo cuando falta llenar)
/// - Prevención de múltiples clicks durante operaciones async
/// - Feedback visual durante carga (botón deshabilitado + texto)
/// - Reset automático de highlights al escribir
/// </summary>
public class LoginDummyUI : MonoBehaviour
{
    private const string Placeholder = "Selecciona...";

    [Header("Login UI (Code + Password)")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField inputEmployeeCode;
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private Button btnLogin;
    [SerializeField] private Button btnGoRegister;

    [Header("Register UI (One-time)")]
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private TMP_InputField regEmployeeCode;
    [SerializeField] private TMP_InputField regPassword;
    [SerializeField] private TMP_InputField regPasswordConfirm;
    [SerializeField] private TMP_InputField regFirstName;
    [SerializeField] private TMP_InputField regLastName;

    [SerializeField] private TMP_Dropdown regDropdownStore;
    [SerializeField] private TMP_Dropdown regDropdownRoleOptional;

    [SerializeField] private Button btnRegister;
    [SerializeField] private Button btnGoLogin;

    [Header("After Login UI")]
    [SerializeField] private GameObject afterLoginRoot;

    [Header("Feedback")]
    [SerializeField] private TMP_Text errorText;

    [Header("Data")]
    [SerializeField] private StoreCatalog storeCatalog;

    [Header("Options")]
    [SerializeField] private bool requireNameOnRegister = true;

    [Header("UX - Visual Feedback")]
    [Tooltip("Color para resaltar campos con error")]
    [SerializeField] private Color errorHighlightColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("Duración del shake en segundos")]
    [SerializeField] private float shakeDuration = 0.3f;

    [Tooltip("Intensidad del shake en píxeles")]
    [SerializeField] private float shakeIntensity = 8f;

    [Header("Loading State")]
    [SerializeField] private string loadingText = "Procesando...";
    [SerializeField] private GameObject loadingIndicator; // Opcional: spinner o similar

    // Auth
    private IAuthService authProvider;
    private IAuthService Auth
    {
        get
        {
            if (authProvider == null)
            {
                authProvider = FirebaseAuthService.Instance ?? (IAuthService)DummyAuthService.Instance;

                if (authProvider == null)
                {
                    Debug.LogError("[LoginDummyUI] No AuthService found!");
                }
            }
            return authProvider;
        }
    }

    // Store mapping (Register dropdown)
    private readonly List<string> _storeIds = new();

    // Estado de operación en progreso (previene doble click)
    private bool _isProcessing;

    // Cache de colores originales para restaurar después del highlight
    private readonly Dictionary<Graphic, Color> _originalColors = new();

    // Cache de textos originales de botones
    private string _originalLoginText;
    private string _originalRegisterText;

    private void Awake()
    {
        SetupButtonListeners();
        SetupInputFieldListeners();
        CacheButtonTexts();
    }

    private void SetupButtonListeners()
    {
        if (btnLogin)
        {
            btnLogin.onClick.RemoveAllListeners();
            btnLogin.onClick.AddListener(OnLoginClicked);
        }
        if (btnGoRegister)
        {
            btnGoRegister.onClick.RemoveAllListeners();
            btnGoRegister.onClick.AddListener(ShowRegister);
        }
        if (btnGoLogin)
        {
            btnGoLogin.onClick.RemoveAllListeners();
            btnGoLogin.onClick.AddListener(ShowLogin);
        }
        if (btnRegister)
        {
            btnRegister.onClick.RemoveAllListeners();
            btnRegister.onClick.AddListener(OnRegisterClicked);
        }
    }

    /// <summary>
    /// Configura listeners para limpiar highlights cuando el usuario escribe
    /// </summary>
    private void SetupInputFieldListeners()
    {
        // Login fields
        if (inputEmployeeCode)
            inputEmployeeCode.onValueChanged.AddListener(_ => ClearHighlight(inputEmployeeCode));
        if (inputPassword)
            inputPassword.onValueChanged.AddListener(_ => ClearHighlight(inputPassword));

        // Register fields
        if (regEmployeeCode)
            regEmployeeCode.onValueChanged.AddListener(_ => ClearHighlight(regEmployeeCode));
        if (regPassword)
            regPassword.onValueChanged.AddListener(_ => ClearHighlight(regPassword));
        if (regPasswordConfirm)
            regPasswordConfirm.onValueChanged.AddListener(_ => ClearHighlight(regPasswordConfirm));
        if (regFirstName)
            regFirstName.onValueChanged.AddListener(_ => ClearHighlight(regFirstName));
        if (regLastName)
            regLastName.onValueChanged.AddListener(_ => ClearHighlight(regLastName));

        // Dropdowns
        if (regDropdownStore)
            regDropdownStore.onValueChanged.AddListener(_ => ClearDropdownHighlight(regDropdownStore));
    }

    private void CacheButtonTexts()
    {
        if (btnLogin)
        {
            var txt = btnLogin.GetComponentInChildren<TMP_Text>();
            if (txt) _originalLoginText = txt.text;
        }
        if (btnRegister)
        {
            var txt = btnRegister.GetComponentInChildren<TMP_Text>();
            if (txt) _originalRegisterText = txt.text;
        }
    }

    private void Start()
    {
        BuildRegisterStoreDropdown();
        ShowLogin();

        // Autorellenar último employeeCode (si existe)
        if (Auth != null && Auth.TryLoadLastSession(out var emp, out _, out _))
        {
            if (inputEmployeeCode) inputEmployeeCode.text = emp;
        }

        ShowError("");
        SetLoadingIndicator(false);
    }

    // -------------------------
    // UI Flow
    // -------------------------

    private void ShowLogin()
    {
        if (loginPanel) loginPanel.SetActive(true);
        if (registerPanel) registerPanel.SetActive(false);
        if (afterLoginRoot) afterLoginRoot.SetActive(false);

        ClearAllHighlights();
        ShowError("");

        // Focus al primer campo (usar Invoke para evitar problema de coroutine en objeto inactivo)
        FocusFieldSafe(inputEmployeeCode);
    }

    private void ShowRegister()
    {
        if (loginPanel) loginPanel.SetActive(false);
        if (registerPanel) registerPanel.SetActive(true);
        if (afterLoginRoot) afterLoginRoot.SetActive(false);

        ClearAllHighlights();
        ShowError("");

        // Focus al primer campo
        if (requireNameOnRegister)
            FocusFieldSafe(regFirstName);
        else
            FocusFieldSafe(regEmployeeCode);
    }

    /// <summary>
    /// Hace focus a un InputField de forma segura, esperando un frame.
    /// Usa coroutine desde este MonoBehaviour (que siempre está activo).
    /// </summary>
    private void FocusFieldSafe(TMP_InputField field)
    {
        if (field == null) return;

        // Este script (LoginDummyUI) debe estar en un GameObject activo
        // Los paneles pueden estar inactivos, pero este MonoBehaviour no
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FocusInputFieldDelayed(field));
        }
    }

    private IEnumerator FocusInputFieldDelayed(TMP_InputField field)
    {
        // Esperar un frame para que el layout se actualice
        yield return null;

        // Verificar que el campo siga válido y visible
        if (field != null && field.gameObject.activeInHierarchy && field.interactable)
        {
            field.Select();
            field.ActivateInputField();
        }
    }

    private IEnumerator AfterLoginFlow()
    {
        // 1) Cargar remoto ANTES de mostrar el menú
        var ps = ProgressService.Instance ?? FindObjectOfType<ProgressService>();
        if (ps != null)
            yield return ps.LoadRemoteMedalsIntoCompletionService();

        // 2) Mostrar menú PRIMERO (para que los binders se activen)
        if (loginPanel) loginPanel.SetActive(false);
        if (registerPanel) registerPanel.SetActive(false);
        if (afterLoginRoot) afterLoginRoot.SetActive(true);

        // 3) Esperar un frame para que los paneles se activen completamente
        yield return null;

        // 4) Notificar cambio de progreso (esto dispara OnEnable en los binders activos)
        CompletionService.NotifyChanged();

        // 5) Esperar otro frame para layout
        yield return null;
        Canvas.ForceUpdateCanvases();

        // 6) Refresh manual solo a binders ACTIVOS (false = solo objetos activos)
        var binders = FindObjectsOfType<MenuCompletionBinder>(false);
        foreach (var binder in binders)
        {
            if (binder != null && binder.gameObject.activeInHierarchy)
            {
                binder.RefreshAll();
            }
        }

        // 7) Reset estado
        SetProcessing(false);
    }

    // -------------------------
    // Handlers
    // -------------------------

    private void OnLoginClicked()
    {
        // Prevenir doble click
        if (_isProcessing) return;

        ShowError("");
        ClearAllHighlights();

        if (Auth == null)
        {
            ShowError("AuthProvider no disponible.");
            return;
        }

        // Validación con highlight visual
        bool hasErrors = false;
        var errorFields = new List<TMP_InputField>();

        string emp = inputEmployeeCode ? inputEmployeeCode.text.Trim() : "";
        string pass = inputPassword ? inputPassword.text : "";

        if (string.IsNullOrWhiteSpace(emp))
        {
            errorFields.Add(inputEmployeeCode);
            hasErrors = true;
        }

        if (string.IsNullOrWhiteSpace(pass) || pass.Length < 6)
        {
            errorFields.Add(inputPassword);
            hasErrors = true;
        }

        if (hasErrors)
        {
            // Mostrar error específico
            if (errorFields.Contains(inputEmployeeCode) && errorFields.Contains(inputPassword))
                ShowError("Completa todos los campos.");
            else if (errorFields.Contains(inputEmployeeCode))
                ShowError("Ingresa tu código de empleado.");
            else
                ShowError("Ingresa tu contraseña (mín. 6 caracteres).");

            HighlightErrorFields(errorFields);
            return;
        }

        // Iniciar operación
        SetProcessing(true, btnLogin, _originalLoginText);

        Auth.SignIn(emp, pass, "", "",
            onSuccess: () =>
            {
                StartCoroutine(AfterLoginFlow());
            },
            onError: msg =>
            {
                SetProcessing(false, btnLogin, _originalLoginText);
                ShowError(string.IsNullOrWhiteSpace(msg) ? "Error de login." : msg);
            }
        );
    }

    private void OnRegisterClicked()
    {
        // Prevenir doble click
        if (_isProcessing) return;

        ShowError("");
        ClearAllHighlights();

        if (Auth == null)
        {
            ShowError("AuthProvider no disponible.");
            return;
        }

        // Recoger valores
        string emp = regEmployeeCode ? regEmployeeCode.text.Trim() : "";
        string pass = regPassword ? regPassword.text : "";
        string pass2 = regPasswordConfirm ? regPasswordConfirm.text : "";
        string first = regFirstName ? regFirstName.text.Trim() : "";
        string last = regLastName ? regLastName.text.Trim() : "";
        string storeId = GetRegisterSelectedStoreId();
        string roleId = GetRegisterSelectedRoleId();

        // Validación con highlight visual
        var errorInputs = new List<TMP_InputField>();
        var errorDropdowns = new List<TMP_Dropdown>();
        string firstError = null;

        // Validar en orden de aparición visual
        if (requireNameOnRegister)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                errorInputs.Add(regFirstName);
                firstError ??= "Ingresa tus nombres.";
            }
            if (string.IsNullOrWhiteSpace(last))
            {
                errorInputs.Add(regLastName);
                firstError ??= "Ingresa tus apellidos.";
            }
        }

        if (string.IsNullOrWhiteSpace(emp))
        {
            errorInputs.Add(regEmployeeCode);
            firstError ??= "Ingresa tu código de empleado.";
        }

        if (string.IsNullOrWhiteSpace(storeId))
        {
            errorDropdowns.Add(regDropdownStore);
            firstError ??= "Selecciona una tienda.";
        }

        if (pass.Length < 6)
        {
            errorInputs.Add(regPassword);
            firstError ??= "La contraseña debe tener al menos 6 caracteres.";
        }

        if (pass != pass2)
        {
            errorInputs.Add(regPasswordConfirm);
            firstError ??= "Las contraseñas no coinciden.";
        }

        // Si hay errores, mostrar y resaltar
        if (errorInputs.Count > 0 || errorDropdowns.Count > 0)
        {
            ShowError(firstError ?? "Completa todos los campos requeridos.");
            HighlightErrorFields(errorInputs);
            HighlightErrorDropdowns(errorDropdowns);
            return;
        }

        // Iniciar operación
        SetProcessing(true, btnRegister, _originalRegisterText);

        Auth.Register(emp, pass, first, last, storeId, roleId,
            onSuccess: () =>
            {
                RunAfterLoginFlow();
            },
            onError: msg =>
            {
                SetProcessing(false, btnRegister, _originalRegisterText);
                ShowError(string.IsNullOrWhiteSpace(msg) ? "Error de registro." : msg);
            }
        );
    }

    // -------------------------
    // Processing State
    // -------------------------

    private void SetProcessing(bool processing, Button button = null, string originalText = null)
    {
        _isProcessing = processing;

        // Actualizar botones
        if (btnLogin) btnLogin.interactable = !processing;
        if (btnRegister) btnRegister.interactable = !processing;
        if (btnGoRegister) btnGoRegister.interactable = !processing;
        if (btnGoLogin) btnGoLogin.interactable = !processing;

        // Cambiar texto del botón activo
        if (button != null)
        {
            var txt = button.GetComponentInChildren<TMP_Text>();
            if (txt)
            {
                txt.text = processing ? loadingText : (originalText ?? txt.text);
            }
        }

        // Indicador de carga
        SetLoadingIndicator(processing);
    }

    private void SetLoadingIndicator(bool show)
    {
        if (loadingIndicator)
            loadingIndicator.SetActive(show);
    }

    // -------------------------
    // Visual Feedback - Highlights
    // -------------------------

    private void HighlightErrorFields(List<TMP_InputField> fields)
    {
        foreach (var field in fields)
        {
            if (field == null) continue;
            HighlightInputField(field);
        }

        // Shake el primero para llamar la atención
        if (fields.Count > 0 && fields[0] != null)
            StartCoroutine(ShakeTransform(fields[0].transform));
    }

    private void HighlightErrorDropdowns(List<TMP_Dropdown> dropdowns)
    {
        foreach (var dropdown in dropdowns)
        {
            if (dropdown == null) continue;
            HighlightDropdown(dropdown);
        }

        // Shake el primero
        if (dropdowns.Count > 0 && dropdowns[0] != null)
            StartCoroutine(ShakeTransform(dropdowns[0].transform));
    }

    private void HighlightInputField(TMP_InputField field)
    {
        if (field == null) return;

        // Buscar el Image de fondo (generalmente es el targetGraphic o un hijo)
        var targetGraphic = field.targetGraphic as Image;
        if (targetGraphic == null)
            targetGraphic = field.GetComponentInChildren<Image>();

        if (targetGraphic != null)
        {
            // Guardar color original si no lo tenemos
            if (!_originalColors.ContainsKey(targetGraphic))
                _originalColors[targetGraphic] = targetGraphic.color;

            targetGraphic.color = errorHighlightColor;
        }

        // También cambiar el color del borde si usa ColorBlock
        var colors = field.colors;
        colors.normalColor = errorHighlightColor;
        colors.highlightedColor = errorHighlightColor;
        field.colors = colors;
    }

    private void HighlightDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        var targetGraphic = dropdown.targetGraphic as Image;
        if (targetGraphic == null)
            targetGraphic = dropdown.GetComponentInChildren<Image>();

        if (targetGraphic != null)
        {
            if (!_originalColors.ContainsKey(targetGraphic))
                _originalColors[targetGraphic] = targetGraphic.color;

            targetGraphic.color = errorHighlightColor;
        }
    }

    private void ClearHighlight(TMP_InputField field)
    {
        if (field == null) return;

        var targetGraphic = field.targetGraphic as Image;
        if (targetGraphic == null)
            targetGraphic = field.GetComponentInChildren<Image>();

        if (targetGraphic != null && _originalColors.TryGetValue(targetGraphic, out var original))
        {
            targetGraphic.color = original;
        }

        // Restaurar ColorBlock
        var colors = field.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f);
        field.colors = colors;
    }

    private void ClearDropdownHighlight(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        var targetGraphic = dropdown.targetGraphic as Image;
        if (targetGraphic == null)
            targetGraphic = dropdown.GetComponentInChildren<Image>();

        if (targetGraphic != null && _originalColors.TryGetValue(targetGraphic, out var original))
        {
            targetGraphic.color = original;
        }
    }

    private void ClearAllHighlights()
    {
        // Login
        ClearHighlight(inputEmployeeCode);
        ClearHighlight(inputPassword);

        // Register
        ClearHighlight(regEmployeeCode);
        ClearHighlight(regPassword);
        ClearHighlight(regPasswordConfirm);
        ClearHighlight(regFirstName);
        ClearHighlight(regLastName);
        ClearDropdownHighlight(regDropdownStore);
    }

    private IEnumerator ShakeTransform(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalPos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = originalPos.x + Random.Range(-shakeIntensity, shakeIntensity) * (1f - elapsed / shakeDuration);
            target.localPosition = new Vector3(x, originalPos.y, originalPos.z);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        target.localPosition = originalPos;
    }

    // -------------------------
    // Register dropdown helpers
    // -------------------------

    private void BuildRegisterStoreDropdown()
    {
        if (regDropdownStore == null || storeCatalog == null || storeCatalog.stores == null)
        {
            Debug.LogWarning("[LoginDummyUI] StoreCatalog/RegDropdownStore no asignado.");
            return;
        }

        regDropdownStore.ClearOptions();
        _storeIds.Clear();

        var options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(Placeholder) };
        _storeIds.Add("");

        foreach (var s in storeCatalog.stores)
        {
            if (!s.enabled) continue;
            options.Add(new TMP_Dropdown.OptionData(s.storeName));
            _storeIds.Add(s.storeId);
        }

        regDropdownStore.AddOptions(options);
        regDropdownStore.value = 0;
        regDropdownStore.RefreshShownValue();
    }

    private string GetRegisterSelectedStoreId()
    {
        if (regDropdownStore == null) return "";
        int i = regDropdownStore.value;
        if (i < 0 || i >= _storeIds.Count) return "";
        return _storeIds[i];
    }

    private string GetRegisterSelectedRoleId()
    {
        if (regDropdownRoleOptional == null || regDropdownRoleOptional.options == null || regDropdownRoleOptional.options.Count == 0)
            return "";

        string txt = regDropdownRoleOptional.options[regDropdownRoleOptional.value].text;
        return (txt == Placeholder) ? "" : txt;
    }

    // -------------------------
    // Feedback
    // -------------------------

    private void ShowError(string msg)
    {
        if (errorText == null) return;
        errorText.text = msg ?? "";
        errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(msg));
    }

    private void RunAfterLoginFlow()
    {
        // Runner que SIEMPRE está activo (SYSTEMS)
        var runner = (MonoBehaviour)(ProgressService.Instance ?? FindObjectOfType<ProgressService>());
        if (runner != null) runner.StartCoroutine(AfterLoginFlow());
        else StartCoroutine(AfterLoginFlow()); // fallback (si estás activo)
    }

    // -------------------------
    // Cleanup
    // -------------------------

    private void OnDestroy()
    {
        // Remover listeners para evitar memory leaks
        if (inputEmployeeCode) inputEmployeeCode.onValueChanged.RemoveAllListeners();
        if (inputPassword) inputPassword.onValueChanged.RemoveAllListeners();
        if (regEmployeeCode) regEmployeeCode.onValueChanged.RemoveAllListeners();
        if (regPassword) regPassword.onValueChanged.RemoveAllListeners();
        if (regPasswordConfirm) regPasswordConfirm.onValueChanged.RemoveAllListeners();
        if (regFirstName) regFirstName.onValueChanged.RemoveAllListeners();
        if (regLastName) regLastName.onValueChanged.RemoveAllListeners();
        if (regDropdownStore) regDropdownStore.onValueChanged.RemoveAllListeners();
    }
}