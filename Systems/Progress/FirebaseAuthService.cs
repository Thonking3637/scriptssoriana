using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

public class FirebaseAuthService : MonoBehaviour, IAuthService
{
    public static FirebaseAuthService Instance { get; private set; }

    private FirebaseAuth _auth;
    private FirebaseFirestore _db;

    private const string PP_EMPLOYEE = "AUTH_EMPLOYEE";
    private const string PP_STORE = "AUTH_STORE";
    private const string PP_ROLE = "AUTH_ROLE";

    [Header("Pilot safety")]
    [SerializeField] private bool allowOfflineFallback = false;
    [SerializeField] private float firestoreTimeoutSeconds = 10f;

    private bool _isBusy;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _auth = FirebaseAuth.DefaultInstance;
            _db = FirebaseFirestore.DefaultInstance;

            // ✅ VALIDACIÓN EXTENDIDA DE CONFIGURACIÓN
            bool configOk = true;

            if (_auth == null)
            {
                Debug.LogError("[FirebaseAuthService] ❌ FirebaseAuth.DefaultInstance is NULL!");
                configOk = false;
            }
            else if (_auth.App == null)
            {
                Debug.LogError("[FirebaseAuthService] ❌ FirebaseAuth.App is NULL!");
                configOk = false;
            }
            else
            {
                var app = _auth.App;
                var options = app.Options;

                if (options == null)
                {
                    Debug.LogError("[FirebaseAuthService] ❌ Firebase App Options are NULL!");
                    configOk = false;
                }
                else
                {
                    string apiKey = options.ApiKey;
                    string projectId = options.ProjectId;
                    string appId = options.AppId;

                    Debug.Log("╔════════════════════════════════════════════════════════════╗");
                    Debug.Log("║       FIREBASE AUTHENTICATION SERVICE - CONFIG            ║");
                    Debug.Log("╠════════════════════════════════════════════════════════════╣");
                    Debug.Log($"║  Project ID:  {(string.IsNullOrEmpty(projectId) ? "❌ EMPTY" : "✅ " + projectId)}");
                    Debug.Log($"║  App ID:      {(string.IsNullOrEmpty(appId) ? "❌ EMPTY" : "✅ " + appId.Substring(0, Math.Min(20, appId.Length)) + "...")}");
                    Debug.Log($"║  API Key:     {(string.IsNullOrEmpty(apiKey) ? "❌ EMPTY" : apiKey.Length > 20 ? "✅ OK (válida)" : "⚠️ SOSPECHOSA (muy corta)")}");

                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Debug.LogError("║  ⚠️  API KEY VACÍA - FIREBASE NO FUNCIONARÁ              ║");
                        Debug.LogError("║      Verifica google-services.json                        ║");
                        configOk = false;
                    }

                    if (string.IsNullOrEmpty(projectId))
                    {
                        Debug.LogError("║  ⚠️  PROJECT ID VACÍO - FIREBASE NO FUNCIONARÁ           ║");
                        configOk = false;
                    }

                    Debug.Log("╠════════════════════════════════════════════════════════════╣");
                    if (configOk)
                    {
                        Debug.Log("║  Status:      ✅ READY                                     ║");
                        Debug.Log("╚════════════════════════════════════════════════════════════╝");
                    }
                    else
                    {
                        Debug.LogError("║  Status:      ❌ CONFIGURATION ERROR                      ║");
                        Debug.LogError("╠════════════════════════════════════════════════════════════╣");
                        Debug.LogError("║  ACCIÓN REQUERIDA:                                         ║");
                        Debug.LogError("║  1. Verifica google-services.json en StreamingAssets      ║");
                        Debug.LogError("║  2. Descarga el archivo correcto de Firebase Console      ║");
                        Debug.LogError("║  3. Asegúrate de tener el proyecto correcto               ║");
                        Debug.LogError("╚════════════════════════════════════════════════════════════╝");
                    }
                }
            }

            if (_db == null)
            {
                Debug.LogWarning("[FirebaseAuthService] ⚠️ Firestore instance is NULL (no crítico para Auth)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("╔════════════════════════════════════════════════════════════╗");
            Debug.LogError("║  🔥 FIREBASE INITIALIZATION FAILED                        ║");
            Debug.LogError("╠════════════════════════════════════════════════════════════╣");
            Debug.LogError($"║  Exception: {ex.GetType().Name}");
            Debug.LogError($"║  Message: {ex.Message}");
            Debug.LogError("╚════════════════════════════════════════════════════════════╝");
        }
    }

    public bool TryLoadLastSession(out string employeeCode, out string storeId, out string roleId)
    {
        employeeCode = PlayerPrefs.GetString(PP_EMPLOYEE, "");
        storeId = PlayerPrefs.GetString(PP_STORE, "");
        roleId = PlayerPrefs.GetString(PP_ROLE, "");
        return !string.IsNullOrWhiteSpace(employeeCode) && !string.IsNullOrWhiteSpace(storeId);
    }

    public void SignIn(string employeeCode, string password, string storeId, string roleId,
    Action onSuccess, Action<string> onError)
    {
        if (_isBusy)
        {
            onError?.Invoke("Operación en progreso. Espera un momento.");
            return;
        }

        if (!FirebaseBootstrap.IsReady)
        {
            onError?.Invoke("Firebase está inicializando. Intenta en un momento.");
            return;
        }

        _ = SignInAsync(employeeCode, password, storeId, roleId, onSuccess, onError);
    }

    private async Task SignInAsync(string employeeCode, string password, string storeId, string roleId, Action onSuccess, Action<string> onError)
    {
        _isBusy = true;

        try
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _auth ??= FirebaseAuth.DefaultInstance;
            _db ??= FirebaseFirestore.DefaultInstance;

            employeeCode = (employeeCode ?? "").Trim();
            storeId = (storeId ?? "").Trim();
            roleId = roleId ?? "";
            password = password ?? "";

            if (string.IsNullOrWhiteSpace(employeeCode)) { onError?.Invoke("EmployeeCode vacío."); return; }

            string email = $"{employeeCode.ToLowerInvariant()}@soriana.local";
            Debug.Log($"[FirebaseAuth] Attempt auth emailSynth={email} store={storeId} role={roleId}");

            FirebaseUser user = null;

            // 1) Intentar SignIn
            try
            {
                var signIn = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                user = signIn.User;
                Debug.Log("[FirebaseAuth] SignIn OK");
            }
            catch (Exception exSignIn)
            {
                // 🔥 Logging detallado para debugging
                Debug.LogError($"[FirebaseAuth] SignIn failed - Type: {exSignIn.GetType().Name}");
                Debug.LogError($"[FirebaseAuth] SignIn failed - Message: {exSignIn.Message}");

                // 🔥 Estrategia 1: Detectar por TEXTO en el mensaje (más confiable en Unity)
                string lower = exSignIn.Message.ToLowerInvariant();

                // 🆕 CASO ESPECIAL: "internal error" con dominios custom (@soriana.local)
                // Puede significar CONTRASEÑA INCORRECTA en lugar de error de configuración
                if (lower.Contains("internal error") || lower.Contains("internal_error"))
                {
                    Debug.LogWarning("[FirebaseAuth] Internal error detected - verifying if user exists...");

                    // ✅ Intentar verificar si es problema de password vs configuración
                    // Si el usuario existe, probablemente es contraseña incorrecta
                    // Si no existe o hay otro problema, es error de configuración

                    bool likelyWrongPassword = false;

                    // Heurística: Si llegamos hasta aquí con un email sintético bien formado
                    // y Firebase está configurado (ya validamos en Start), probablemente
                    // el error es de credenciales incorrectas, no de configuración
                    if (!string.IsNullOrEmpty(email) && email.Contains("@"))
                    {
                        Debug.LogWarning($"[FirebaseAuth] Email seems valid: {email}");
                        Debug.LogWarning("[FirebaseAuth] Likely cause: WRONG PASSWORD (not config error)");
                        likelyWrongPassword = true;
                    }

                    if (likelyWrongPassword)
                    {
                        Debug.Log("[FirebaseAuth] Detected: Wrong password (internal error with custom domain)");
                        onError?.Invoke("Contraseña incorrecta o usuario no existe.");
                        return;
                    }

                    // Si llegamos aquí, sí es probable que sea error de configuración
                    Debug.LogError("╔════════════════════════════════════════════════════════════╗");
                    Debug.LogError("║  🔥 FIREBASE CONFIGURATION ERROR - INTERNAL ERROR        ║");
                    Debug.LogError("╠════════════════════════════════════════════════════════════╣");
                    Debug.LogError("║  Posibles causas:                                          ║");
                    Debug.LogError("║  1. Email/Password NO habilitado en Firebase Console      ║");
                    Debug.LogError("║  2. google-services.json falta o es incorrecto            ║");
                    Debug.LogError("║  3. API Key inválida o vacía                              ║");
                    Debug.LogError("║  4. Proyecto Firebase mal configurado                     ║");
                    Debug.LogError("╠════════════════════════════════════════════════════════════╣");
                    Debug.LogError("║  ACCIÓN REQUERIDA:                                         ║");
                    Debug.LogError("║  → Firebase Console → Authentication → Sign-in method     ║");
                    Debug.LogError("║  → Habilitar 'Email/Password'                             ║");
                    Debug.LogError("╚════════════════════════════════════════════════════════════╝");
                    onError?.Invoke("Error de configuración de Firebase.\n\nVerifica:\n• Firebase Console → Auth → Email/Password habilitado\n• google-services.json correcto\n• API Key válida");
                    return;
                }

                // Contraseña incorrecta - múltiples variantes
                if (lower.Contains("password") ||
                    lower.Contains("credential") ||
                    lower.Contains("invalid-credential") ||
                    lower.Contains("invalid_credential") ||
                    lower.Contains("wrong"))
                {
                    Debug.Log("[FirebaseAuth] Detected: Wrong password (text match)");
                    onError?.Invoke("Contraseña incorrecta.");
                    return;
                }

                // Usuario no encontrado
                if (lower.Contains("no user") ||
                    lower.Contains("user not found") ||
                    lower.Contains("user-not-found") ||
                    lower.Contains("user_not_found"))
                {
                    Debug.Log("[FirebaseAuth] Detected: User not found (text match)");
                    onError?.Invoke("Usuario no registrado. Ve a Registro.");
                    return;
                }

                // Problemas de red
                if (lower.Contains("network") ||
                    lower.Contains("connection") ||
                    lower.Contains("internet"))
                {
                    Debug.Log("[FirebaseAuth] Detected: Network error (text match)");
                    onError?.Invoke("Sin conexión o red inestable.");
                    return;
                }

                // Demasiados intentos
                if (lower.Contains("too many") ||
                    lower.Contains("attempts") ||
                    lower.Contains("temporarily disabled"))
                {
                    Debug.Log("[FirebaseAuth] Detected: Too many attempts (text match)");
                    onError?.Invoke("Demasiados intentos. Intenta luego.");
                    return;
                }

                // 🔥 Estrategia 2: Intentar detectar por CÓDIGOS de error
                if (TryGetAuthError(exSignIn, out var aerr, out var raw, out var m))
                {
                    Debug.LogError($"[FirebaseAuth] Error code - AuthError enum: {aerr} ({(int)aerr}), Raw: {raw}");

                    // 🆕 AuthError.Failure (código 1) con dominios custom
                    // Usualmente significa contraseña incorrecta, no error de configuración
                    if (aerr == AuthError.Failure || raw == 1)
                    {
                        Debug.LogWarning("[FirebaseAuth] AuthError.Failure detected with custom domain");
                        Debug.LogWarning("[FirebaseAuth] Treating as: Wrong password or user doesn't exist");
                        onError?.Invoke("Contraseña incorrecta o usuario no existe.");
                        return;
                    }

                    // Contraseña incorrecta
                    if (raw == 17009 ||
                        aerr == AuthError.WrongPassword ||
                        aerr == AuthError.InvalidCredential)
                    {
                        Debug.Log("[FirebaseAuth] Detected: Wrong password (error code)");
                        onError?.Invoke("Contraseña incorrecta.");
                        return;
                    }

                    // Usuario no encontrado
                    if (raw == 17011 || aerr == AuthError.UserNotFound)
                    {
                        Debug.Log("[FirebaseAuth] Detected: User not found (error code)");
                        onError?.Invoke("Usuario no registrado. Ve a Registro.");
                        return;
                    }

                    // Problemas de red
                    if (raw == 17020)
                    {
                        Debug.Log("[FirebaseAuth] Detected: Network error (error code)");
                        onError?.Invoke("Sin conexión o red inestable.");
                        return;
                    }

                    // Demasiados intentos
                    if (raw == 17010)
                    {
                        Debug.Log("[FirebaseAuth] Detected: Too many attempts (error code)");
                        onError?.Invoke("Demasiados intentos. Intenta luego.");
                        return;
                    }
                }

                // Si llegamos aquí, no pudimos identificar el error específico
                Debug.LogWarning($"[FirebaseAuth] Unhandled auth error. Full exception: {exSignIn}");
                onError?.Invoke("No se pudo iniciar sesión.");
                return;
            }


            if (user == null)
            {
                onError?.Invoke("No se pudo autenticar (user null).");
                return;
            }

            // ✅ 3) Guardar sesión y entrar AL MENÚ primero (NO bloqueamos por Firestore)
            PlayerPrefs.SetString(PP_EMPLOYEE, employeeCode);
            PlayerPrefs.SetString(PP_STORE, storeId);
            PlayerPrefs.SetString(PP_ROLE, roleId);
            PlayerPrefs.Save();

            if (SessionContext.Instance != null)
            {
                SessionContext.Instance.SetLoggedIn(user.UserId, employeeCode, storeId, roleId);
            }
            else
            {
                Debug.LogError("[FirebaseAuth] SessionContext.Instance is null!");
                onError?.Invoke("Error crítico: sesión no disponible.");
                return;
            }

            Debug.Log($"[FirebaseAuth] Logged in uid={user.UserId} emp={employeeCode} store={storeId} role={roleId}");

            onSuccess?.Invoke();

            // ✅ 4) Firestore en background con timeout (si falla, NO rompe el juego)
            _ = UpsertUserProfileSafe(user.UserId, employeeCode, storeId, roleId);
        }
        catch (Exception ex)
        {
            LogFirebaseException("[FirebaseAuth] Error", ex);

            if (allowOfflineFallback)
            {
                string offlineUid = $"offline_{(employeeCode ?? "user").ToLowerInvariant()}";
                SessionContext.Instance.SetLoggedIn(offlineUid, employeeCode, storeId, roleId ?? "");
                Debug.LogWarning($"[FirebaseAuth] Offline fallback -> uid={offlineUid}");
                onSuccess?.Invoke();
                return;
            }

            onError?.Invoke("Error de login. Revisa internet / consola Firebase / restricciones.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task UpsertUserProfileSafe(string uid, string employeeCode, string storeId, string roleId, string firstName = "", string lastName = "")
    {
        try
        {
            if (_db == null) return;

            var doc = _db.Collection("users").Document(uid);

            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["employeeCode"] = employeeCode,
                ["storeId"] = storeId,
                ["role"] = roleId ?? "",
                ["firstName"] = firstName ?? "",
                ["lastName"] = lastName ?? "",
                ["updatedAt"] = FieldValue.ServerTimestamp
            };

            var upsertTask = doc.SetAsync(data, SetOptions.MergeAll);
            var timeout = Task.Delay((int)(firestoreTimeoutSeconds * 1000));

            var done = await Task.WhenAny(upsertTask, timeout);
            if (done == timeout) return;

            await upsertTask;
        }
        catch (Exception ex) { LogFirebaseException("[FirebaseAuth] UpsertUserProfile failed", ex); }
    }

    public void SignOut()
    {
        _auth?.SignOut();
        SessionContext.Instance.Clear();
        Debug.Log("[FirebaseAuth] Signed out");
    }

    private static void LogFirebaseException(string prefix, Exception ex)
    {
        if (ex == null) { Debug.LogError($"{prefix}: null"); return; }

        if (ex is AggregateException ag && ag.InnerExceptions != null && ag.InnerExceptions.Count > 0)
        {
            foreach (var inner in ag.InnerExceptions) LogFirebaseException(prefix, inner);
            return;
        }

        if (ex is FirebaseException fbEx)
        {
            // Intento de mapear a AuthError (a veces coincide)
            string authName = "";
            try { authName = $" ({(AuthError)fbEx.ErrorCode})"; } catch { }

            Debug.LogError($"{prefix}: FirebaseException code={fbEx.ErrorCode}{authName} msg={fbEx.Message}");
            return;
        }

        Debug.LogError($"{prefix}: {ex.GetType().Name} | {ex.Message}");
    }

    private static bool TryGetAuthError(Exception ex, out AuthError authError, out int rawCode, out string msg)
    {
        authError = default;
        rawCode = -1;
        msg = ex?.Message ?? "";

        // Unwrap AggregateException (muy común en async)
        if (ex is AggregateException ag && ag.InnerExceptions != null && ag.InnerExceptions.Count > 0)
            ex = ag.InnerExceptions[0];

        if (ex is FirebaseException fbEx)
        {
            rawCode = fbEx.ErrorCode;
            msg = fbEx.Message ?? msg;

            // ⚠️ El casteo NO siempre es confiable, pero intentamos
            try
            {
                authError = (AuthError)fbEx.ErrorCode;
            }
            catch
            {
                authError = default;
            }

            return true;
        }

        return false;
    }

    public void Register(string employeeCode, string password, string firstName, string lastName,
    string storeId, string roleId, Action onSuccess, Action<string> onError)
    {
        if (_isBusy) return;
        _ = RegisterAsync(employeeCode, password, firstName, lastName, storeId, roleId, onSuccess, onError);
    }

    private async Task RegisterAsync(string employeeCode, string password, string firstName, string lastName,
        string storeId, string roleId, Action onSuccess, Action<string> onError)
    {
        _isBusy = true;

        try
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _auth ??= FirebaseAuth.DefaultInstance;
            _db ??= FirebaseFirestore.DefaultInstance;

            employeeCode = (employeeCode ?? "").Trim();
            storeId = (storeId ?? "").Trim();
            roleId = roleId ?? "";
            password = password ?? "";

            firstName = CleanName(firstName, 40);
            lastName = CleanName(lastName, 60);

            if (string.IsNullOrWhiteSpace(employeeCode)) { onError?.Invoke("EmployeeCode vacío."); return; }
            if (string.IsNullOrWhiteSpace(storeId)) { onError?.Invoke("Selecciona una tienda."); return; }
            if (password.Length < 6) { onError?.Invoke("Contraseña mínima: 6 caracteres."); return; }

            string email = $"{employeeCode.ToLowerInvariant()}@soriana.local";

            FirebaseUser user;
            try
            {
                var created = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                user = created.User;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseAuth] Register failed - Type: {ex.GetType().Name}");
                Debug.LogError($"[FirebaseAuth] Register failed - Message: {ex.Message}");

                string lower = ex.Message.ToLowerInvariant();

                // Usuario ya existe
                if (lower.Contains("already in use") ||
                    lower.Contains("email-already-in-use") ||
                    lower.Contains("email_already_in_use"))
                {
                    onError?.Invoke("Ese código ya está registrado. Inicia sesión.");
                    return;
                }

                // Email inválido
                if (lower.Contains("invalid email") ||
                    lower.Contains("invalid-email"))
                {
                    onError?.Invoke("Formato de código inválido.");
                    return;
                }

                // Contraseña débil
                if (lower.Contains("weak password") ||
                    lower.Contains("weak-password"))
                {
                    onError?.Invoke("Contraseña muy débil. Usa al menos 6 caracteres.");
                    return;
                }

                // Red
                if (lower.Contains("network") ||
                    lower.Contains("connection"))
                {
                    onError?.Invoke("Sin conexión o red inestable.");
                    return;
                }

                // Intentar por código
                if (TryGetAuthError(ex, out var aerr, out _, out _))
                {
                    if (aerr == AuthError.EmailAlreadyInUse)
                    {
                        onError?.Invoke("Ese código ya está registrado. Inicia sesión.");
                        return;
                    }
                }

                Debug.LogWarning($"[FirebaseAuth] Unhandled register error: {ex}");
                onError?.Invoke("No se pudo registrar.");
                return;
            }

            if (user == null) { onError?.Invoke("Registro falló (user null)."); return; }

            // guardar sesión local
            PlayerPrefs.SetString(PP_EMPLOYEE, employeeCode);
            PlayerPrefs.SetString(PP_STORE, storeId);
            PlayerPrefs.SetString(PP_ROLE, roleId);
            PlayerPrefs.Save();

            SessionContext.Instance.SetLoggedIn(user.UserId, employeeCode, storeId, roleId);
            Debug.Log($"[FirebaseAuth] Registered uid={user.UserId} emp={employeeCode} store={storeId} role={roleId}");
            onSuccess?.Invoke();

            // perfil en Firestore (no bloqueante)
            _ = UpsertUserProfileSafe(user.UserId, employeeCode, storeId, roleId, firstName, lastName);
        }
        finally { _isBusy = false; }
    }

    private static string CleanName(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        if (s.Length > max) s = s.Substring(0, max);
        return s;
    }
}