using UnityEngine;

public class RoomIntroDialogue : MonoBehaviour
{
    [SerializeField] private string roomID;
    [SerializeField] private NPC npc;
    [SerializeField] private StoryCondition condition = new();
    [SerializeField] private string completedFlag;

    private bool started;
    private float nextAttempt;

    private void OnEnable()
    {
        if (npc != null)
            (npc.onDialogueFinished ??= new UnityEngine.Events.UnityEvent()).AddListener(Complete);
    }

    private void Update()
    {
        if (started)
        {
            if (npc == null || !npc.IsDialogueActive)
                started = false;

            return;
        }

        if (Time.unscaledTime < nextAttempt || StoryState.Instance == null || !StoryState.Instance.IsReady || string.IsNullOrWhiteSpace(completedFlag) || StoryState.Instance.HasFlag(completedFlag))
            return;

        if (RoomTravelController.Instance == null || RoomTravelController.Instance.CurrentRoomID != roomID || RoomTravelController.IsTravelling || StoryCutsceneController.IsPlaying || PauseController.IsGamePaused || NPC.ActiveNPC != null || (PlayerFreeze.Instance != null && PlayerFreeze.Instance.IsFrozen))
            return;

        if (npc == null || !npc.isActiveAndEnabled || (condition != null && !condition.IsMet()))
            return;

        nextAttempt = Time.unscaledTime + 1f;
        npc.BeginDialogue();
        started = npc.IsDialogueActive;
    }

    private void Complete()
    {
        if (!started || StoryState.Instance == null)
            return;

        started = false;
        StoryState.Instance.SetFlag(completedFlag);
        SaveController.Instance.RequestSave();
    }

    private void OnDisable()
    {
        if (npc != null)
            npc.onDialogueFinished?.RemoveListener(Complete);

        started = false;
    }
}
