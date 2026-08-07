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
    public bool[] endsDialogue; // Mark where dialogue ends

    [Header("Dialogue Audio")]
    public AudioClip[] voiceSound;
    public bool[] repeatingVoice;
    public PitchType[] pitchType;
    public float[] voicePitch;

    [Header("Dialogue Quest")]
    public int questInProgressIndex; // Said when quest in progress.
    public int questCompletedIndex; // Said when quest completed.
    public Quest quest; // Quest NPC gives

}

public enum PitchType { Static, Random }

[System.Serializable]
public class DialogueChoice
{
    public int dialogueIndex; //Dialogue line where choices appear
    public string[] choices; //Player response options
    public int[] nextDialogueIndexes; //Where choice leads
    public bool[] givesQuest; //If choice gives quest
}