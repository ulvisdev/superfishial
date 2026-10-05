using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NPC : MonoBehaviour, iInteractable
{
    public NewNPCDialogue dialogueData;
    public PlayerMovement playerMovement;

    [Header("Interaction")]
    public StoryCondition interactionCondition = new();

    [Header("Events")]
    public UnityEvent onDialogueFinished;

    public static NPC ActiveNPC { get; private set; }
    public bool IsDialogueActive => isDialogueActive;

    private DialogueController dialogueUI;
    private PlayerFreeze freeze;
    private int dialogueIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private bool showingChoices;
    private bool movementWasEnabled;
    private bool pauseWasSet;
    private bool handInOnEnd;
    private bool completing;
    private string completedFlag;
    private readonly HashSet<int> completedLines = new();

    public bool CanInteract()
    {
        if (isDialogueActive)
            return true;

        return dialogueData != null && ActiveNPC == null && !PauseController.IsGamePaused && (interactionCondition == null || interactionCondition.IsMet());
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        if (isDialogueActive)
            NextLine();
        else
            BeginDialogue();
    }

    public void BeginDialogue()
    {
        if (dialogueData == null || ActiveNPC != null || StoryState.Instance == null || !StoryState.Instance.IsReady)
            return;

        if (interactionCondition != null && !interactionCondition.IsMet())
            return;

        dialogueUI = DialogueController.Instance;

        if (dialogueUI == null || dialogueData.dialogueLine == null || dialogueData.dialogueLine.Length == 0)
            return;

        SelectStart();

        if (!ValidIndex(dialogueIndex))
        {
            Debug.LogError("NPC dialogue start index is outside its dialogue lines.", this);
            return;
        }

        ActiveNPC = this;
        isDialogueActive = true;
        showingChoices = false;
        completedLines.Clear();
        pauseWasSet = PauseController.IsGamePaused;
        freeze = PlayerFreeze.Instance;

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        movementWasEnabled = playerMovement != null && playerMovement.IsMovementEnabled;

        if (freeze != null)
            freeze.FreezePlayer();
        else if (playerMovement != null)
            playerMovement.SetMovementEnabled(false);

        PauseController.SetPause(true);
        dialogueUI.ClearChoices();
        dialogueUI.ShowDialogueUI(true);
        DisplayCurrentLine();
    }

    private void SelectStart()
    {
        dialogueIndex = 0;
        completedFlag = null;
        handInOnEnd = false;

        if (dialogueData.startRules != null)
            foreach (DialogueStartRule rule in dialogueData.startRules)
            {
                if (rule == null || (rule.condition != null && !rule.condition.IsMet()))
                    continue;

                dialogueIndex = rule.startIndex;
                completedFlag = rule.completedFlag;
                handInOnEnd = rule.handInOnEnd;
                return;
            }

        Quest quest = dialogueData.quest;
        QuestController controller = QuestController.Instance;

        if (quest == null || controller == null)
            return;

        if (controller.IsQuestHandedIn(quest.questID))
            dialogueIndex = dialogueData.questHandedInIndex >= 0 ? dialogueData.questHandedInIndex : dialogueData.questCompletedIndex;
        else if (controller.IsQuestCompleted(quest.questID))
        {
            dialogueIndex = dialogueData.questCompletedIndex;
            handInOnEnd = dialogueData.handInOnCompletedDialogueEnd;
        }
        else if (controller.IsQuestActive(quest.questID))
            dialogueIndex = dialogueData.questInProgressIndex;
    }

    private void NextLine()
    {
        if (!isDialogueActive || showingChoices)
            return;

        if (isTyping)
        {
            StopAllCoroutines();
            dialogueUI.ShowAllDialogueText();
            isTyping = false;
            return;
        }

        CompleteLine();

        if (!isDialogueActive)
            return;

        if (At(dialogueData.endsDialogue, dialogueIndex, false))
        {
            EndDialogue();
            return;
        }

        if (dialogueData.choices != null)
            foreach (DialogueChoice choices in dialogueData.choices)
                if (choices != null && choices.dialogueIndex == dialogueIndex && DisplayChoices(choices))
                    return;

        dialogueIndex++;
        DisplayCurrentLine();
    }

    private void CompleteLine()
    {
        if (!completedLines.Add(dialogueIndex) || dialogueData.lineActions == null)
            return;

        foreach (DialogueLineAction action in dialogueData.lineActions)
            if (action != null && action.dialogueIndex == dialogueIndex)
                StoryState.Instance.SetFlag(action.flagToSet);
    }

    private bool DisplayChoices(DialogueChoice choices)
    {
        dialogueUI.ClearChoices();
        int count = 0;

        if (choices.choices == null)
            return false;

        for (int i = 0; i < choices.choices.Length; i++)
        {
            DialogueChoiceRule rule = GetRule(choices, i);
            bool givesQuest = At(choices.givesQuest, i, false);

            if (rule != null && rule.condition != null && !rule.condition.IsMet())
                continue;

            if (givesQuest && (QuestController.Instance == null || !QuestController.Instance.CanAcceptQuest(dialogueData.quest)))
                continue;

            if (choices.nextDialogueIndexes == null || i >= choices.nextDialogueIndexes.Length)
                continue;

            int choiceIndex = i;
            dialogueUI.CreateChoiceButton(choices.choices[i], () => ChooseOption(choices, choiceIndex));
            count++;
        }

        showingChoices = count > 0;
        return showingChoices;
    }

    private DialogueChoiceRule GetRule(DialogueChoice choices, int index)
    {
        if (choices.storyRules == null)
            return null;

        return Array.Find(choices.storyRules, rule => rule != null && rule.choiceIndex == index);
    }

    private void ChooseOption(DialogueChoice choices, int index)
    {
        if (!isDialogueActive || !showingChoices)
            return;

        DialogueChoiceRule rule = GetRule(choices, index);

        if (rule != null && rule.condition != null && !rule.condition.IsMet())
        {
            RefreshChoices(choices);
            return;
        }

        if (At(choices.givesQuest, index, false) && !QuestController.Instance.TryAcceptQuest(dialogueData.quest))
        {
            RefreshChoices(choices);
            return;
        }

        if (rule != null && rule.handInQuest && (dialogueData.quest == null || !QuestController.Instance.TryHandInQuest(dialogueData.quest.questID)))
        {
            RefreshChoices(choices);
            return;
        }

        if (rule != null && rule.handInQuest)
            handInOnEnd = false;

        showingChoices = false;
        dialogueUI.ClearChoices();

        if (rule != null)
            StoryState.Instance.SetFlag(rule.flagToSet);

        if (!isDialogueActive)
            return;

        dialogueIndex = choices.nextDialogueIndexes[index];
        DisplayCurrentLine();
    }

    private void RefreshChoices(DialogueChoice choices)
    {
        if (DisplayChoices(choices))
            return;

        dialogueIndex++;
        DisplayCurrentLine();
    }

    private bool ValidIndex(int index)
    {
        return index >= 0 && index < dialogueData.dialogueLine.Length;
    }

    private void DisplayCurrentLine()
    {
        if (!isDialogueActive)
            return;

        if (!ValidIndex(dialogueIndex))
        {
            EndDialogue();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    private IEnumerator TypeLine()
    {
        isTyping = true;
        dialogueUI.SetNPCInfo(At(dialogueData.npcName, dialogueIndex, ""), At<Sprite>(dialogueData.npcPortrait, dialogueIndex, null));
        dialogueUI.PrepareDialogueText(dialogueData.dialogueLine[dialogueIndex]);
        int count = dialogueUI.GetDialogueCharacterCount();
        AudioClip voice = At<AudioClip>(dialogueData.voiceSound, dialogueIndex, null);
        float pitch = At(dialogueData.voicePitch, dialogueIndex, 1f);
        bool randomPitch = At(dialogueData.RandomPitch, dialogueIndex, false);
        bool repeating = At(dialogueData.repeatingVoice, dialogueIndex, false);

        if (!repeating && voice != null)
            SoundEffectManager.PlayVoice(voice, pitch, randomPitch);

        for (int i = 0; i < count; i++)
        {
            dialogueUI.RevealDialogueCharacter(i);

            if (repeating && voice != null && !char.IsWhiteSpace(dialogueUI.GetDialogueCharacter(i)))
                SoundEffectManager.PlayVoice(voice, pitch, randomPitch);

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, At(dialogueData.typingSpeed, dialogueIndex, 0.03f)));
        }

        dialogueUI.ShowAllDialogueText();
        isTyping = false;

        if (At(dialogueData.autoProgressLine, dialogueIndex, false))
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, At(dialogueData.autoProgressDelay, dialogueIndex, 0.5f)));
            NextLine();
        }
    }

    private static T At<T>(T[] values, int index, T fallback)
    {
        return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
    }

    public void EndDialogue()
    {
        if (!isDialogueActive || completing)
            return;

        completing = true;
        CompleteLine();
        bool success = !handInOnEnd || (dialogueData.quest != null && QuestController.Instance.TryHandInQuest(dialogueData.quest.questID));
        CloseDialogue();

        if (success)
        {
            StoryState.Instance.SetFlag(completedFlag);
            onDialogueFinished?.Invoke();
        }

        completing = false;
    }

    public void CancelDialogue()
    {
        CloseDialogue();
    }

    private void CloseDialogue()
    {
        if (!isDialogueActive)
            return;

        StopAllCoroutines();
        isDialogueActive = false;
        isTyping = false;
        showingChoices = false;
        ActiveNPC = null;

        if (dialogueUI != null)
        {
            dialogueUI.ClearChoices();
            dialogueUI.ClearDialogueText();
            dialogueUI.ShowDialogueUI(false);
        }

        PauseController.SetPause(pauseWasSet);

        if (freeze != null)
            freeze.UnfreezePlayer();
        else if (playerMovement != null)
            playerMovement.SetMovementEnabled(movementWasEnabled);
    }

    private void OnDisable()
    {
        CloseDialogue();
    }
}
