using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class ImageIntroManager : MonoBehaviour
{
    public Image introImage;       // Imagen a mostrar
    public CanvasGroup fadeCanvas; // CanvasGroup para el Fade
    public float displayTime = 2f; // Tiempo que la imagen estará visible
    public string nextScene = "MainMenu"; // Escena a cargar después

    private void Start()
    {
        StartIntroSequence();
    }

    private void StartIntroSequence()
    {
        // 📌 **Pantalla inicia en negro**
        fadeCanvas.alpha = 1;
        introImage.enabled = true;

        // 📌 **Fade In (1 segundo)**
        fadeCanvas.DOFade(0, 1f).OnComplete(() =>
        {
            // 📌 **Esperar 2 segundos con la imagen visible**
            Invoke(nameof(StartFadeOut), displayTime);
        });
    }

    private void StartFadeOut()
    {
        // 📌 **Fade Out (1 segundo) antes de cambiar de escena**
        fadeCanvas.DOFade(1, 1f).OnComplete(() =>
        {
            SceneManager.LoadScene(nextScene);
        });
    }
}
