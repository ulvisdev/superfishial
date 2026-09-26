using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPC : MonoBehaviour, iInteractable
{
    public NewNPCDialogue dialogueData;
    private DialogueController dialogueUI;
    public PlayerMovement playerMovement;
    private int dialogueIndex;
    private bool isTyping, isDialogueActive;

    private enum QuestState { NotStarted, InProgress, Completed }
    private QuestState questState = QuestState.NotStarted;

    void Start()
    {
        dialogueUI = DialogueController.Instance;
        isDialogueActive = false;
    }
    public bool CanInteract()
    {
        return dialogueData != null && (isDialogueActive || !PauseController.IsGamePaused);
    }

    public void Interact()
    {
        if (dialogueData == null || (PauseController.IsGamePaused && !isDialogueActive))
            return;

        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        //Sync with quest data
        SyncQuestState();

        //Set dialogue line based on questState
        if (questState == QuestState.NotStarted)
        {
            dialogueIndex = 0;
        }
        else if (questState == QuestState.InProgress)
        {
            dialogueIndex = dialogueData.questInProgressIndex;
        }
        else if (questState == QuestState.Completed)
        {
            dialogueIndex = dialogueData.questCompletedIndex;
        }

        isDialogueActive = true;

        // Disable player movement
        playerMovement.SetMovementEnabled(false);

        dialogueUI.ShowDialogueUI(true);
        PauseController.SetPause(true);

        DisplayCurrentLine();
    }

    private void SyncQuestState()
    {
        if (dialogueData.quest == null) return;

        string questID = dialogueData.quest.questID;

        if (QuestController.Instance.IsQuestCompleted(questID) || QuestController.Instance.IsQuestHandedIn(questID))
        {
            questState = QuestState.Completed;
        }
        else if (QuestController.Instance.IsQuestActive(questID))
        {
            questState = QuestState.InProgress;
        }
        else
        {
            questState = QuestState.NotStarted;
        }
    }

    void NextLine()
    {
        // if (isTyping)
        // {
        //     // Skip typing animation and show the full line
        //     StopAllCoroutines();
        //     DialogueController.Instance.dialogueVertexAnimator.SkipToEndOfCurrentMessage();
        //     isTyping = false;
        // }

        // --- NEW --- //
        if (isTyping)
        {
            StopAllCoroutines();
            dialogueUI.ShowAllDialogueText();
            isTyping = false;
            return;
        }
        // --- --- --- //

        //Clear Choices
        dialogueUI.ClearChoices();

        //Check endDialogueLines
        if (dialogueData.endsDialogue[dialogueIndex])
        {
            EndDialogue();
            return;
        }

        //Check if choices & display
        foreach (DialogueChoice dialogueChoice in dialogueData.choices)
        {
            if (dialogueChoice.dialogueIndex == dialogueIndex)
            {
                DisplayChoices(dialogueChoice);
                return;
            }
        }

        if (++dialogueIndex < dialogueData.dialogueLine.Length)
        {
            // If another line, type next line
            DisplayCurrentLine();
        }
        else
        {
            EndDialogue();
        }
    }

    // IEnumerator TypeLine()
    // {
    //     isTyping = true;
    //     dialogueUI.SetDialogueText("");

    //     dialogueUI.SetNPCInfo(dialogueData.npcName[dialogueIndex], dialogueData.npcPortrait[dialogueIndex]);

    //     if (!dialogueData.repeatingVoice[dialogueIndex])
    //     {
    //         SoundEffectManager.PlayVoice(dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex], dialogueData.RandomPitch[dialogueIndex]);
    //         DialogueController.Instance.PlayDialogue(dialogueData.dialogueLine[dialogueIndex], null);
    //     }
    //     else
    //     {
    //         DialogueController.Instance.PlayDialogue(dialogueData.dialogueLine[dialogueIndex], dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex], dialogueData.RandomPitch[dialogueIndex]);
    //     }

    //     foreach (char letter in dialogueData.dialogueLine[dialogueIndex])
    //     {
    //         dialogueUI.SetDialogueText(dialogueUI.dialogueText.text += letter);
    //         if (dialogueData.repeatingVoice[dialogueIndex])
    //         {
    //             if (dialogueData.RandomPitch[dialogueIndex])
    //             {
    //                 SoundEffectManager.PlayVoice(dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex], true);
    //             }
    //             else
    //             {
    //                 SoundEffectManager.PlayVoice(dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex]);
    //             }
    //         }
    //         yield return new WaitForSeconds(dialogueData.typingSpeed[dialogueIndex]);
    //     }

    //     isTyping = false;

    //     if (dialogueData.autoProgressLine[dialogueIndex])
    //     {
    //         yield return new WaitForSeconds(dialogueData.autoProgressDelay[dialogueIndex]);
    //         NextLine();
    //     }
    // }

    // --- NEW --- //
    IEnumerator TypeLine()
    {
        isTyping = true;

        dialogueUI.SetNPCInfo(dialogueData.npcName[dialogueIndex], dialogueData.npcPortrait[dialogueIndex]);
        string currentLine = dialogueData.dialogueLine[dialogueIndex];

        dialogueUI.PrepareDialogueText(currentLine);

        int characterCount = dialogueUI.GetDialogueCharacterCount();

        if (!dialogueData.repeatingVoice[dialogueIndex])
            SoundEffectManager.PlayVoice(dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex], dialogueData.RandomPitch[dialogueIndex]);

        for (int i = 0; i < characterCount; i++)
        {
            dialogueUI.RevealDialogueCharacter(i);
            char character = dialogueUI.GetDialogueCharacter(i);

            if (dialogueData.repeatingVoice[dialogueIndex] && !char.IsWhiteSpace(character))
                SoundEffectManager.PlayVoice(dialogueData.voiceSound[dialogueIndex], dialogueData.voicePitch[dialogueIndex], dialogueData.RandomPitch[dialogueIndex]);

            yield return new WaitForSecondsRealtime(dialogueData.typingSpeed[dialogueIndex]);
        }

        isTyping = false;

        if (dialogueData.autoProgressLine[dialogueIndex])
        {
            yield return new WaitForSecondsRealtime(dialogueData.autoProgressDelay[dialogueIndex]);
            NextLine();
        }
    }
    // --- --- --- //

    void DisplayChoices(DialogueChoice choice)
    {
        for (int i = 0; i < choice.choices.Length; i++)
        {
            int nextIndex = choice.nextDialogueIndexes[i];
            bool givesQuest = choice.givesQuest[i];
            dialogueUI.CreateChoiceButton(choice.choices[i], () => ChooseOption(nextIndex, givesQuest));
        }
    }

    void ChooseOption(int nextIndex, bool givesQuest)
    {
        if (givesQuest)
        {
            QuestController.Instance.AcceptQuest(dialogueData.quest);
            questState = QuestState.InProgress;
        }
        dialogueIndex = nextIndex;
        dialogueUI.ClearChoices();
        DisplayCurrentLine();
    }

    void DisplayCurrentLine()
    {
        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    public void EndDialogue()
    {
        if (questState == QuestState.Completed && !QuestController.Instance.IsQuestHandedIn(dialogueData.quest.questID))
        {
            HandleQuestCompletion(dialogueData.quest);
        }

        StopAllCoroutines();
        isDialogueActive = false;

        // dialogueUI.SetDialogueText("");

        // --- NEW --- //
        dialogueUI.ClearDialogueText();
        // --- --- --- //
        
        dialogueUI.ShowDialogueUI(false);
        PauseController.SetPause(false);

        // Re-enable player movement
        playerMovement.SetMovementEnabled(true);
    }

    void HandleQuestCompletion(Quest quest)
    {
        RewardsController.Instance.GiveQuestReward(quest);
        QuestController.Instance.HandInQuest(quest.questID);
    }
}
