using UnityEngine;

public class LetterSoundPlayer : MonoBehaviour
{
    [Tooltip("Clips de sonido para A-Z (0-25), Ñ (26), y silencio (27)")]
    public AudioClip[] letterSounds = new AudioClip[28]; // A-Z (0–25), Ñ (26), SILENCIO (27)

    [Range(0f, 1f)]
    public float volume = 0.3f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayCharSound(char c)
    {
        char upper = char.ToUpper(c);
        int index;

        if (upper == 'Ñ')
        {
            index = 26;
        }
        else if (upper >= 'A' && upper <= 'Z')
        {
            index = upper - 'A';
        }
        else
        {
            index = 27;
        }

        if (index >= 0 && index < letterSounds.Length && letterSounds[index] != null)
        {
            audioSource.pitch = Random.Range(1.2f, 1.4f);
            audioSource.PlayOneShot(letterSounds[index], volume);
        }
    }
}
