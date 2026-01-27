using System;
using UnityEngine;

public class DummyAuthService : MonoBehaviour, IAuthService
{
    public static DummyAuthService Instance { get; private set; }

    private const string PP_UID = "pp_uid";
    private const string PP_EMPLOYEE = "pp_employee";
    private const string PP_STORE = "pp_store";
    private const string PP_ROLE = "pp_role";
    private const string PP_PASS_PREFIX = "pp_pass_";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TryLoadLastSession(out string employeeCode, out string storeId, out string roleId)
    {
        employeeCode = PlayerPrefs.GetString(PP_EMPLOYEE, "");
        storeId = PlayerPrefs.GetString(PP_STORE, "");
        roleId = PlayerPrefs.GetString(PP_ROLE, "");

        if (string.IsNullOrWhiteSpace(employeeCode))
            return false;

        // Si por algún motivo el store global está vacío, intenta recuperar el store del perfil por-empleado
        if (string.IsNullOrWhiteSpace(storeId))
        {
            string key = employeeCode.ToLowerInvariant();
            storeId = PlayerPrefs.GetString(PP_STORE + "_" + key, "");
            roleId = PlayerPrefs.GetString(PP_ROLE + "_" + key, roleId);

            // Re-hidrata la “sesión actual” (solo para que quede consistente)
            if (!string.IsNullOrWhiteSpace(storeId))
            {
                PlayerPrefs.SetString(PP_STORE, storeId);
                PlayerPrefs.SetString(PP_ROLE, roleId);
                PlayerPrefs.Save();
            }
        }

        // ✅ Sesión “usable” solo por tener employeeCode (login ya no pide dropdowns)
        return true;
    }
    public void Register(
    string employeeCode,
    string password,
    string firstName,
    string lastName,
    string storeId,
    string roleId,
    Action onSuccess,
    Action<string> onError)
    {
        employeeCode = employeeCode?.Trim() ?? "";
        storeId = storeId?.Trim() ?? "";
        roleId = roleId?.Trim() ?? "";
        password = password ?? "";

        if (string.IsNullOrWhiteSpace(employeeCode)) { onError?.Invoke("EmployeeCode vacío."); return; }
        if (string.IsNullOrWhiteSpace(storeId)) { onError?.Invoke("Selecciona una tienda válida."); return; }
        if (password.Length < 6) { onError?.Invoke("Contraseña mínima: 6 caracteres."); return; }

        string key = employeeCode.ToLowerInvariant();
        string passKey = PP_PASS_PREFIX + key;

        if (!string.IsNullOrWhiteSpace(PlayerPrefs.GetString(passKey, "")))
        {
            onError?.Invoke("Ese código ya está registrado. Inicia sesión.");
            return;
        }

        // Guardar password por empleado
        PlayerPrefs.SetString(passKey, password);

        // Guardar store/role por empleado (🔥 lo que te faltaba)
        PlayerPrefs.SetString(PP_STORE + "_" + key, storeId);
        PlayerPrefs.SetString(PP_ROLE + "_" + key, roleId);

        // UID estable por empleado
        string uid = $"dummy_{key}";

        // “Sesión actual”
        PlayerPrefs.SetString(PP_UID, uid);
        PlayerPrefs.SetString(PP_EMPLOYEE, employeeCode);
        PlayerPrefs.SetString(PP_STORE, storeId);
        PlayerPrefs.SetString(PP_ROLE, roleId);
        PlayerPrefs.Save();

        SessionContext.Instance.SetLoggedIn(uid, employeeCode, storeId, roleId);

        Debug.Log($"[DummyAuth] Registered uid={uid} emp={employeeCode} store={storeId} role={roleId}");
        onSuccess?.Invoke();
    }

    public void SignIn(
     string employeeCode,
     string password,
     string storeId,
     string roleId,
     Action onSuccess,
     Action<string> onError)
    {
        employeeCode = employeeCode?.Trim() ?? "";
        password = password ?? "";

        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            onError?.Invoke("EmployeeCode vacío.");
            return;
        }

        string key = employeeCode.ToLowerInvariant();

        // Validar registro + password
        string passKey = PP_PASS_PREFIX + key;
        string savedPass = PlayerPrefs.GetString(passKey, "");

        if (string.IsNullOrWhiteSpace(savedPass))
        {
            onError?.Invoke("Usuario no registrado. Ve a Registro.");
            return;
        }

        if (savedPass != password)
        {
            onError?.Invoke("Contraseña incorrecta.");
            return;
        }

        // Recuperar store/role del perfil guardado en registro
        string storedStore = PlayerPrefs.GetString(PP_STORE + "_" + key, "");
        string storedRole = PlayerPrefs.GetString(PP_ROLE + "_" + key, "");

        if (string.IsNullOrWhiteSpace(storedStore))
        {
            onError?.Invoke("Perfil incompleto (sin tienda). Re-registra al usuario.");
            return;
        }

        string uid = $"dummy_{key}";

        // “Sesión actual”
        PlayerPrefs.SetString(PP_UID, uid);
        PlayerPrefs.SetString(PP_EMPLOYEE, employeeCode);
        PlayerPrefs.SetString(PP_STORE, storedStore);
        PlayerPrefs.SetString(PP_ROLE, storedRole);
        PlayerPrefs.Save();

        SessionContext.Instance.SetLoggedIn(uid, employeeCode, storedStore, storedRole);

        Debug.Log($"[DummyAuth] Logged in uid={uid} emp={employeeCode} store={storedStore} role={storedRole}");
        onSuccess?.Invoke();
    }

    public void SignOut()
    {
        SessionContext.Instance.Clear();
        Debug.Log("[DummyAuth] Signed out");
    }
}
