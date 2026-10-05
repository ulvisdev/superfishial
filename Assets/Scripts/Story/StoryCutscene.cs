using UnityEngine;

public enum StoryCutscenePresentation
{
    Journal,
    Frames
}

public enum StoryCutsceneTransition
{
    Fade,
    Bubbles
}

[CreateAssetMenu(fileName = "StoryCutscene", menuName = "Story/Cutscene")]
public class StoryCutscene : ScriptableObject
{
    [Header("Story")]
    public StoryCondition condition = new();
    public string completedFlag;

    [Header("Presentation")]
    public StoryCutscenePresentation presentation;
    public StoryCutsceneTransition transition;
    public Sprite background;
    public Sprite journalHands;
    public Sprite completionBanner;
    public string completionText = "QUEST COMPLETE";
    public bool waitForInput = true;

    [Header("Journal")]
    public Vector2 journalStart = Vector2.zero;
    public Vector2 journalEnd = new(0f, 1200f);
    public float journalHoldSeconds = 1.5f;
    public float journalMoveSeconds = 1f;

    [Header("Banner")]
    public Vector2 bannerStart = new(0f, 700f);
    public Vector2 bannerEnd = new(0f, 350f);
    public float bannerMoveSeconds = 0.6f;
    public float minimumHoldSeconds = 0.5f;

    [Header("Frame Animation")]
    public Sprite[] frames;
    public float framesPerSecond = 12f;

    [Header("Audio")]
    public AudioClip music;
    public bool loopMusic = true;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    public AudioClip journalSound;
    public AudioClip bannerSound;
}
