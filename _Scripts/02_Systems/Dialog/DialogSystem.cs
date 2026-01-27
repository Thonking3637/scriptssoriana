using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

[Serializable]
public class CustomerComment
{
    public string category;
    [TextArea] public string clientText;
    public string question;
    public List<string> options;
    public string correctAnswer;
}

public class DialogSystem : MonoBehaviour
{
    public static DialogSystem Instance;

    [Header("Client Dialog UI")]
    [SerializeField] private GameObject clientPanel;
    [SerializeField] private CanvasGroup clientCanvasGroup;
    [SerializeField] private TextMeshProUGUI clientNameText;
    [SerializeField] private TextMeshProUGUI clientDialogText;
    [SerializeField] private TypewriterEffect clientTypewriter;

    [Header("Question UI")]
    [SerializeField] private GameObject questionPanelContainer;
    [SerializeField] private CanvasGroup questionCanvasGroup;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private TypewriterEffect questionTypewriter;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private GameObject optionButtonPrefab;

    [Header("Sound")]
    [SerializeField] private LetterSoundPlayer letterSoundPlayer;

    [Header("Feedback UI")]
    [SerializeField] private CanvasGroup feedbackFlashCanvasGroup;
    [SerializeField] private Color correctColor = new Color(0f, 1f, 0f, 0.6f);
    [SerializeField] private Color incorrectColor = new Color(1f, 0f, 0f, 0.6f);
    [SerializeField] private Image feedbackImage;

    [SerializeField] public List<CustomerComment> customerComments = new();
    private Dictionary<string, int> commentIndexes = new();

    private InstructionsManager instructionsManager;
    private List<Button> activeButtons = new();
    private bool isActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        instructionsManager = InstructionsManager.Instance ?? FindObjectOfType<InstructionsManager>();

        clientCanvasGroup.alpha = 0;
        clientCanvasGroup.gameObject.SetActive(false);

        questionCanvasGroup.alpha = 0;
        questionCanvasGroup.gameObject.SetActive(false);

        feedbackFlashCanvasGroup.alpha = 0;
        feedbackFlashCanvasGroup.gameObject.SetActive(false);
    }


    public void ShowClientDialog(Client client, string dialog, Action onComplete)
    {
        if (client == null) ShowClientDialog("Cliente", dialog, onComplete);
        else ShowClientDialog(client.clientName, dialog, onComplete);
    }

    public void ShowClientDialog(Client client, string fallbackName, string dialog, Action onComplete)
    {
        string nameToUse = client != null ? client.clientName : fallbackName;
        ShowClientDialog(nameToUse, dialog, onComplete);
    }

    public void ShowClientDialog(string customName, string dialog, Action onComplete)
    {
        clientCanvasGroup.alpha = 0;
        clientCanvasGroup.transform.localScale = Vector3.one * 0.9f;
        clientCanvasGroup.gameObject.SetActive(true);

        clientCanvasGroup.DOFade(1f, 0.3f);
        clientCanvasGroup.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        instructionsManager?.HideInstructions();

        clientNameText.text = customName;

        questionPanelContainer.SetActive(false);
        questionCanvasGroup.alpha = 0;
        questionCanvasGroup.gameObject.SetActive(false);

        if (clientTypewriter != null)
        {
            clientTypewriter.targetText = clientDialogText;
            clientTypewriter.letterSoundPlayer = letterSoundPlayer;
            clientTypewriter.PlayText(dialog, () =>
            {
                DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
            });
        }
        else
        {
            clientDialogText.text = dialog;
            onComplete?.Invoke();
        }
    }

    public void ShowClientDialog(Client client, string dialog, AudioClip customAudio, Action onComplete)
    {
        string nameToUse = client != null ? client.clientName : "Cliente";
        ShowClientDialog(nameToUse, dialog, customAudio, onComplete);
    }

    public void ShowClientDialog(string customName, string dialog, AudioClip customAudio, Action onComplete)
    {
        clientCanvasGroup.alpha = 0;
        clientCanvasGroup.transform.localScale = Vector3.one * 0.9f;
        clientCanvasGroup.gameObject.SetActive(true);

        clientCanvasGroup.DOFade(1f, 0.3f);
        clientCanvasGroup.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        instructionsManager?.HideInstructions();
        clientNameText.text = customName;

        questionPanelContainer.SetActive(false);
        questionCanvasGroup.alpha = 0;
        questionCanvasGroup.gameObject.SetActive(false);

        if (customAudio != null)
        {
            StartCoroutine(PlayAudioWithTyping(dialog, customAudio, onComplete));
        }
        else if (clientTypewriter != null)
        {
            clientTypewriter.targetText = clientDialogText;
            clientTypewriter.letterSoundPlayer = letterSoundPlayer;
            clientTypewriter.PlayText(dialog, () =>
            {
                DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
            });
        }
        else
        {
            clientDialogText.text = dialog;
            onComplete?.Invoke();
        }
    }

    public void ShowClientDialogWithOptions(
        string question,
        List<string> options,
        string correctAnswer,
        Action onCorrect,
        Action onIncorrect,
        bool useTypingEffect = true)
    {
        if (isActive) return;
        isActive = true;

        instructionsManager?.HideInstructions();

        clientCanvasGroup.alpha = 0;
        clientCanvasGroup.gameObject.SetActive(false);

        questionPanelContainer.SetActive(true);
        questionCanvasGroup.alpha = 0;
        questionCanvasGroup.transform.localScale = Vector3.one * 0.9f;
        questionCanvasGroup.gameObject.SetActive(true);
        questionCanvasGroup.DOFade(1f, 0.3f);
        questionCanvasGroup.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);
        activeButtons.Clear();

        void ShowOptions()
        {
            List<string> randomized = new(options);
            Shuffle(randomized);

            foreach (string opt in randomized)
            {
                GameObject go = Instantiate(optionButtonPrefab, optionsContainer);
                go.SetActive(true);

                TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
                Button btn = go.GetComponent<Button>();
                txt.text = opt;
                btn.interactable = false;
                activeButtons.Add(btn);

                go.transform.localScale = Vector3.zero;
                go.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);

                btn.onClick.AddListener(() =>
                {
                    if (opt == correctAnswer)
                    {
                        SoundManager.Instance.PlaySound("success");
                        ShowFeedbackFlash(correctColor);
                        DisableAllButtons();
                        HideDialog(false);
                        onCorrect?.Invoke();
                    }
                    else
                    {
                        SoundManager.Instance.PlaySound("error");
                        ShowFeedbackFlash(incorrectColor);
                        btn.interactable = false;
                        onIncorrect?.Invoke();
                    }
                });
            }

            DOVirtual.DelayedCall(0.3f, () =>
            {
                foreach (var b in activeButtons)
                    b.interactable = true;
            });
        }

        if (useTypingEffect && questionTypewriter != null)
        {
            questionTypewriter.targetText = questionText;
            questionTypewriter.letterSoundPlayer = letterSoundPlayer;
            questionTypewriter.PlayText(question, ShowOptions);
        }
        else
        {
            questionText.text = question;
            ShowOptions();
        }
    }

    public void ShowClientDialogWithOptions(
    string question,
    List<string> options,
    string correctAnswer,
    AudioClip customAudio,
    Action onCorrect,
    Action onIncorrect)
    {
        bool useTypingEffect = customAudio == null;

        ShowClientDialogWithOptions(
            question,
            options,
            correctAnswer,
            onCorrect,
            onIncorrect,
            useTypingEffect
        );

        if (customAudio != null)
        {
            SoundManager.Instance.PlaySound(customAudio);
        }
    }

    public void HideDialog(bool showInstructions = true)
    {
        if (questionCanvasGroup != null)
        {
            questionCanvasGroup.DOFade(0f, 0.25f);
            questionCanvasGroup.transform.DOScale(0.9f, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
            {
                questionCanvasGroup.gameObject.SetActive(false);
                isActive = false;
                if (showInstructions) instructionsManager?.ShowInstructions();
            });
        }

        if (clientCanvasGroup != null)
        {
            clientCanvasGroup.DOFade(0f, 0.25f);
            clientCanvasGroup.transform.DOScale(0.9f, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
            {
                clientCanvasGroup.gameObject.SetActive(false);
                if (showInstructions) instructionsManager?.ShowInstructions();
            });
        }
    }

    private void ShowFeedbackFlash(Color color)
    {
        if (feedbackFlashCanvasGroup != null && feedbackImage != null)
        {
            feedbackImage.color = color;
            feedbackFlashCanvasGroup.gameObject.SetActive(true);
            feedbackFlashCanvasGroup.alpha = 0f;
            feedbackFlashCanvasGroup.DOFade(1f, 0.1f).OnComplete(() =>
            {
                feedbackFlashCanvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    feedbackFlashCanvasGroup.gameObject.SetActive(false);
                });
            });
        }
    }

    public CustomerComment GetNextComment(string category)
    {
        var list = customerComments.FindAll(c => c.category == category);
        if (list.Count == 0) return null;

        if (!commentIndexes.ContainsKey(category))
            commentIndexes[category] = 0;

        int index = commentIndexes[category];
        var result = list[index];

        commentIndexes[category] = (index + 1) % list.Count;
        return result;
    }

    private void DisableAllButtons()
    {
        foreach (var b in activeButtons)
            b.interactable = false;
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void ResetCategoryIndex(string category)
    {
        if (commentIndexes.ContainsKey(category))
        {
            commentIndexes[category] = 0;
        }
    }

    private IEnumerator PlayAudioWithTyping(string fullText, AudioClip clip, Action onComplete)
    {
        if (clientDialogText == null || string.IsNullOrEmpty(fullText) || clip == null)
        {
            clientDialogText.text = fullText;
            onComplete?.Invoke();
            yield break;
        }

        clientDialogText.text = "";

        float totalDuration = clip.length;
        float timePerChar = totalDuration / fullText.Length;

        SoundManager.Instance.PlaySound(clip);

        string visibleText = "";
        string fullTag = "";
        bool insideTag = false;

        foreach (char c in fullText)
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
                clientDialogText.text = visibleText;
                yield return new WaitForSeconds(timePerChar);
            }
        }

        DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
    }

    private void ShowCustomerCommentWithCategory(string category, Client client, Action onCorrect, Action onIncorrect)
    {
        var comment = DialogSystem.Instance.GetNextComment(category);
        if (comment == null)
        {
            Debug.LogWarning($"No se encontró ningún comentario en la categoría '{category}'");
            return;
        }

        DialogSystem.Instance.ShowClientDialog(
            client,
            comment.clientText,
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    comment.question,
                    comment.options,
                    comment.correctAnswer,
                    () =>
                    {
                        DialogSystem.Instance.HideDialog(false);
                        onCorrect?.Invoke();
                    },
                    () =>
                    {
                        onIncorrect?.Invoke();
                    }
                );
            });
    }

}
