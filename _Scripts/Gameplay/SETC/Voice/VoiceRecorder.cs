using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

#if !UNITY_WEBGL
public class VoiceRecorder : MonoBehaviour
{
    public AudioSource playbackSource;
    public Button recordButton;
    public Button playButton;
    public Button continueButton;
    public TextMeshProUGUI textoARepetirText;

    public float maxRecordingTime = 8f;

    public TextMeshProUGUI timerText;
    private float recordingRemainingTime;

    private AudioClip recordedClip;
    private string micName;
    private bool isRecording = false;

    public System.Action OnRecordingFinished;
    public bool IsRecording => isRecording;

    private void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No se detectaron micrófonos.");
            recordButton.interactable = false;
            return;
        }

        micName = Microphone.devices[0];

        recordButton.onClick.AddListener(StartRecording);
        playButton.onClick.AddListener(PlayRecording);
        continueButton.onClick.AddListener(FinalizeRecording);

        playButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
    }

    public void SetTextoARepetir(string texto)
    {
        if (textoARepetirText != null)
            textoARepetirText.text = texto;

        int wordCount = texto.Split(new char[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        maxRecordingTime = Mathf.Max(2f, wordCount * 0.5f);
        recordingRemainingTime = maxRecordingTime;

        if (timerText != null)
            timerText.text = recordingRemainingTime.ToString("F1") + "s";
    }

    public void StartRecording()
    {
#if !UNITY_WEBGL
        isRecording = true;
        recordButton.interactable = false;

        recordedClip = Microphone.Start(micName, false, Mathf.CeilToInt(maxRecordingTime), 44100);
        StartCoroutine(RecordingCountdownCoroutine());
#else
    Debug.LogWarning("🎤 Grabación no soportada en WebGL.");
#endif
    }

    public void PlayRecording()
    {
        if (recordedClip == null)
        {
            Debug.LogWarning("No hay audio grabado.");
            return;
        }

        playbackSource.clip = recordedClip;
        playbackSource.Play();
    }

    private void FinalizeRecording()
    {
        recordButton.interactable = true;
        playButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);

        OnRecordingFinished?.Invoke();
    }

    public void ResetRecorder()
    {
        if (timerText != null)
            timerText.text = "";

        recordedClip = null;
        recordButton.interactable = true;
        playButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
    }

    private IEnumerator RecordingCountdownCoroutine()
    {
        recordingRemainingTime = maxRecordingTime;

        while (recordingRemainingTime > 0f)
        {
            if (timerText != null)
                timerText.text = "Tiempo de Grabación " + recordingRemainingTime.ToString("F1") + "s";

            recordingRemainingTime -= Time.deltaTime;
            yield return null;
        }

        if (Microphone.IsRecording(micName))
        {
            Microphone.End(micName);
            isRecording = false;

            playButton.gameObject.SetActive(true);
            continueButton.gameObject.SetActive(true);

            if (timerText != null)
                timerText.text = "0.0s";
        }
    }
}
#endif