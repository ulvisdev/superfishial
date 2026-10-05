using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewNPCDialogue2", menuName = "NPC Dialogue2")]
public class NewNPCDialogue : ScriptableObject
{
    [Header("Dialogue Context")]
    public string DialogueContext;

    [Header("NPC Appearance")]
    public string[] npcName;
    public Sprite[] npcPortrait;

    [Header("NPC Dialogue Info")]
    public string[] dialogueLine;
    public bool[] autoProgressLine;
    public float[] autoProgressDelay;
    public float[] typingSpeed;
    public DialogueChoice[] choices;
    public bool[] endsDialogue;

    [Header("Dialogue Audio")]
    public AudioClip[] voiceSound;
    public bool[] repeatingVoice;
    public bool[] RandomPitch;
    public float[] voicePitch;

    [Header("Dialogue Quest")]
    public int questInProgressIndex;
    public int questCompletedIndex;
    public Quest quest;

    [Header("Story")]
    public DialogueStartRule[] startRules;
    public DialogueLineAction[] lineActions;
    public bool handInOnCompletedDialogueEnd = true;
    public int questHandedInIndex = -1;

}

[System.Serializable]
public class DialogueChoice
{
    public int dialogueIndex;
    public string[] choices;
    public int[] nextDialogueIndexes;
    public bool[] givesQuest;
    public DialogueChoiceRule[] storyRules;
}
[System.Serializable]
public class DialogueStartRule
{
    public StoryCondition condition = new();
    public int startIndex;
    public string completedFlag;
    public bool handInOnEnd;
}

[System.Serializable]
public class DialogueChoiceRule
{
    public int choiceIndex;
    public StoryCondition condition = new();
    public string flagToSet;
    public bool handInQuest;
}

[System.Serializable]
public class DialogueLineAction
{
    public int dialogueIndex;
    public string flagToSet;
}
