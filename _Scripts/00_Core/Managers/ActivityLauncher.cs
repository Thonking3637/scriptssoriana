using UnityEngine;

public class ActivityLauncher : MonoBehaviour
{
    public static ActivityLauncher Instance;

    public int activityIndexToStart = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Para mantener entre escenas
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
