using System.Collections;
using UnityEngine;
using TMPro;
using System;

public class TypewriterEffect : MonoBehaviour
{
    public TextMeshProUGUI targetText;
    public float typingSpeed = 0.04f;
    public float volume = 0.3f;
    public LetterSoundPlayer letterSoundPlayer;
    public void PlayText(string fullText, Action onComplete = null)
    {
        StopAllCoroutines();
        StartCoroutine(TypeTextCoroutine(fullText, onComplete));
    }

    private IEnumerator TypeTextCoroutine(string text, Action onComplete)
    {
        if (targetText == null) yield break;

        targetText.text = "";

        string visibleText = "";     // lo que se va tipeando
        string fullTag = "";         // acumulador de etiqueta completa
        bool insideTag = false;

        foreach (char c in text)
        {
            if (c == '<') insideTag = true;

            if (insideTag)
            {
                fullTag += c;
                if (c == '>')
                {
                    visibleText += fullTag;
                    fullTag = "";
                    insideTag = false;
                }
            }
            else
            {
                visibleText += c;
                targetText.text = visibleText;

                if (letterSoundPlayer != null)
                    letterSoundPlayer.PlayCharSound(c);

                yield return new WaitForSeconds(typingSpeed);
            }
        }

        onComplete?.Invoke();
    }


}
