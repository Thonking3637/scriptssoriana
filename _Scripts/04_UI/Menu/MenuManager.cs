using UnityEditor;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public void StartActivity(int index, string sceneName)
    {
        ActivityLauncher[] existing = FindObjectsOfType<ActivityLauncher>();
        foreach (ActivityLauncher launcher in existing)
        {
            Destroy(launcher.gameObject);
        }

        GameObject launcherObj = new GameObject("ActivityLauncher");
        DontDestroyOnLoad(launcherObj);
        var activity = launcherObj.AddComponent<ActivityLauncher>();
        activity.activityIndexToStart = index;

        LoadingScreen.LoadScene(sceneName);
    }

    public void LoadLevel(string levelName)
    {
        LoadingScreen.LoadScene(levelName);
    }

    public void ExitApplication()
    {
        Debug.Log("🔴 Saliendo de la aplicación...");

#if UNITY_EDITOR
        // Cierra el Play Mode en el Editor
        EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
        // Cierra la Activity en Android (lo saca de Recents en API 21+)
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                activity.Call("finishAndRemoveTask"); // API 21+
                // Alternativa si solo quieres "mandar al fondo":
                // activity.Call("moveTaskToBack", true);
            }
        }
        catch
        {
            Application.Quit(); // fallback
        }
#else
        // En otras plataformas
        Application.Quit();
#endif
    }
}
