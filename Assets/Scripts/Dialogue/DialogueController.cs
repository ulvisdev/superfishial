using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    public GameObject dialoguePanel;
    public TMP_Text dialogueText, nameText;
    public Image portraitImage;
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;

    public DialogueVertexAnimator dialogueVertexAnimator;

    [Header("Text Effects")]
    public DialogueTextEffects dialogueTextEffects;

    // void Awake()
    // {
    //     dialogueVertexAnimator = new DialogueVertexAnimator(dialogueText);
    //     if (Instance == null) Instance = this;
    //     else Destroy(gameObject);
    // }

    void Awake()
    {
        dialogueVertexAnimator = new DialogueVertexAnimator(dialogueText);

        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialogueTextEffects == null)
            dialogueTextEffects = dialogueText.GetComponent<DialogueTextEffects>();
    }

    public void PrepareDialogueText(string text)
    {
        if (dialogueTextEffects != null)
            dialogueTextEffects.PrepareText(text);
        else
            dialogueText.text = text;
    }

    public int GetDialogueCharacterCount()
    {
        if (dialogueTextEffects == null)
            return 0;

        return dialogueTextEffects.GetCharacterCount();
    }

    public char GetDialogueCharacter(int index)
    {
        if (dialogueTextEffects == null)
            return ' ';

        return dialogueTextEffects.GetCharacter(index);
    }

    public void RevealDialogueCharacter(int index)
    {
        if (dialogueTextEffects != null)
            dialogueTextEffects.RevealCharacter(index);
    }

    public void ShowAllDialogueText()
    {
        if (dialogueTextEffects != null)
            dialogueTextEffects.ShowAll();
    }

    public void ClearDialogueText()
    {
        if (dialogueTextEffects != null)
            dialogueTextEffects.Clear();
        else
            dialogueText.text = "";
    }

    private Coroutine typeRoutine = null;
    public void PlayDialogue(string message, AudioClip voiceClip, float voicePitch = 1, bool randomPitch = false)
    {
        this.EnsureCoroutineStopped(ref typeRoutine);
        dialogueVertexAnimator.textAnimating = false;
        List<DialogueCommand> commands = DialogueUtility.ProcessInputString(message, out string totalTextMessage);
        typeRoutine = StartCoroutine(dialogueVertexAnimator.AnimateTextIn(commands, totalTextMessage, null, voiceClip, voicePitch, randomPitch));
    }

    public void ShowDialogueUI(bool show)
    {
        dialoguePanel.SetActive(show);
    }

    public void SetNPCInfo(string npcName, Sprite portrait)
    {
        nameText.text = npcName;
        portraitImage.sprite = portrait;
    }

    public void SetDialogueText(string text)
    {
        dialogueText.text = text;
    }

    public void ClearChoices()
    {
        foreach (Transform child in choiceContainer) Destroy(child.gameObject);
    }

    public GameObject CreateChoiceButton(string choiceText, UnityEngine.Events.UnityAction onClick)
    {
        GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceContainer);
        choiceButton.GetComponentInChildren<TMP_Text>().text = choiceText;
        choiceButton.GetComponent<Button>().onClick.AddListener(onClick);
        return choiceButton;
    }
}
