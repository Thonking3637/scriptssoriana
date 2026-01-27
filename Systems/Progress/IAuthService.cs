using System;

public interface IAuthService
{
    bool TryLoadLastSession(out string employeeCode, out string storeId, out string roleId);

    void SignIn(
        string employeeCode,
        string password,
        string storeId,
        string roleId,
        Action onSuccess,
        Action<string> onError
    );

    void Register(
        string employeeCode,
        string password,
        string firstName,
        string lastName,
        string storeId,
        string roleId,
        Action onSuccess,
        Action<string> onError
    );

    void SignOut();
}