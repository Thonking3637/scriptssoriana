using System;
using UnityEngine;

public class SessionContext : MonoBehaviour
{
    public static SessionContext Instance { get; private set; }

    public static bool HasSession =>
        Instance != null && Instance.IsLoggedIn && !string.IsNullOrWhiteSpace(Instance.Uid);

    public bool IsLoggedIn { get; private set; }
    public string Uid { get; private set; }
    public string EmployeeCode { get; private set; }
    public string StoreId { get; private set; }
    public string RoleId { get; private set; }

    public event Action OnSessionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetLoggedIn(string uid, string employeeCode, string storeId, string roleId)
    {
        IsLoggedIn = true;
        Uid = uid ?? "";
        EmployeeCode = employeeCode ?? "";
        StoreId = storeId ?? "";
        RoleId = roleId ?? "";
        OnSessionChanged?.Invoke();
    }

    public void Clear()
    {
        IsLoggedIn = false;
        Uid = "";
        EmployeeCode = "";
        StoreId = "";
        RoleId = "";
        OnSessionChanged?.Invoke();
    }
}
