using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using static SoundManager;

public class VolumeSlider : MonoBehaviour, IBeginDragHandler, IPointerUpHandler
{
    public AudioChannel channel;
    public Slider slider;

    [Header("UI")]
    public TextMeshProUGUI txt_volume;

    void Reset()
    {
        if (!slider) slider = GetComponent<Slider>();
        if (slider) { slider.wholeNumbers = false; slider.minValue = 0f; slider.maxValue = 1f; }

        if (!txt_volume)
        {
            var t = transform.Find("txt_volume");
            if (t) txt_volume = t.GetComponent<TextMeshProUGUI>();
        }
    }

    void Start()
    {
        if (!slider) slider = GetComponent<Slider>();

        // Inicializa el valor del slider según el canal
        switch (channel)
        {
            case AudioChannel.Master: slider.value = SoundManager.Instance.GetMasterVolume(); break;
            case AudioChannel.Music: slider.value = SoundManager.Instance.GetMusicVolume(); break;
            case AudioChannel.SFX: slider.value = SoundManager.Instance.GetSFXVolume(); break;
            case AudioChannel.Instruction: slider.value = SoundManager.Instance.GetInstrVolume(); break;
        }

        // Refresca el texto inicial
        UpdateLabel(slider.value);

        // Mientras arrastras: aplica volumen (sin sample) y actualiza label
        slider.onValueChanged.AddListener(v =>
        {
            UpdateLabel(v);
            Apply(v, playSample: false);
        });
    }

    // Al empezar a arrastrar, corta cualquier preview activo
    public void OnBeginDrag(PointerEventData _)
    {
        if (channel == AudioChannel.SFX || channel == AudioChannel.Instruction)
            SoundManager.Instance.StopPreviewSamples();
    }

    public void OnPointerUp(PointerEventData _)
    {
        bool wantSample = (channel == AudioChannel.SFX || channel == AudioChannel.Instruction);
        Apply(slider.value, playSample: wantSample);
    }

    void Apply(float v, bool playSample)
    {
        switch (channel)
        {
            case AudioChannel.Master:
                SoundManager.Instance.SetMasterVolume(v, playSample: false);
                break;
            case AudioChannel.Music:
                SoundManager.Instance.SetMusicVolume(v, playSample: false);
                break;
            case AudioChannel.SFX:
                SoundManager.Instance.SetSFXVolume(v, playSample);
                break;
            case AudioChannel.Instruction:
                SoundManager.Instance.SetInstrVolume(v, playSample);
                break;
        }
    }

    void UpdateLabel(float v)
    {
        if (!txt_volume) return;
        int pct = Mathf.RoundToInt(Mathf.Clamp01(v) * 100f);
        txt_volume.text = $"{pct}%";
    }
}
