using UnityEngine;

[System.Serializable]
public class UserProfile { public string nombre; }

public static class ProfileManager
{
    const string KEY = "user_profile_json";

    public static bool HasProfile() => PlayerPrefs.HasKey(KEY);

    public static void SaveName(string nombre)
    {
        var p = new UserProfile { nombre = (nombre ?? "").Trim() };
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }

    public static string GetName()
    {
        if (!HasProfile()) return "";
        var p = JsonUtility.FromJson<UserProfile>(PlayerPrefs.GetString(KEY));
        return p?.nombre ?? "";
    }
}
