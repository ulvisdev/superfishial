using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomTravelController : MonoBehaviour
{
    public static RoomTravelController Instance { get; private set; }
    public static bool IsTravelling => Instance != null && Instance.travelling;
    public string CurrentRoomID { get; private set; }

    [SerializeField] private PlayerMovement player;
    [SerializeField] private PlayerFreeze playerFreeze;
    [SerializeField] private ScreenTransition transition;
    [SerializeField] private StoryRoom defaultRoom;
    [SerializeField] private StoryRoom[] rooms;

    private bool travelling;
    private bool previousPause;
    private bool ownsFreeze;
    private readonly Dictionary<string, StoryRoom> catalog = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (rooms != null)
            foreach (StoryRoom room in rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.roomID) || room.roomCamera == null || room.arrival == null || !catalog.TryAdd(room.roomID, room))
                {
                    Debug.LogError("Rooms need unique IDs, an arrival point and a camera. Check the Rooms list.", this);
                    catalog.Clear();
                    return;
                }
            }

        if (defaultRoom == null || !catalog.ContainsKey(defaultRoom.roomID))
        {
            Debug.LogError("Assign Default Room and include it in Rooms.", this);
            return;
        }

        SelectRoom(defaultRoom.roomID);
    }

    public bool HasRoom(string id)
    {
        return catalog.ContainsKey(string.IsNullOrEmpty(id) && defaultRoom != null ? defaultRoom.roomID : id ?? "");
    }

    public void SelectRoom(string id)
    {
        if (string.IsNullOrEmpty(id) && defaultRoom != null)
            id = defaultRoom.roomID;

        if (!catalog.TryGetValue(id ?? "", out StoryRoom selected))
            return;

        CurrentRoomID = selected.roomID;

        foreach (StoryRoom room in catalog.Values)
            if (room.roomCamera != selected.roomCamera)
                room.roomCamera.enabled = false;

        selected.roomCamera.enabled = true;
        selected.roomCamera.PreviousStateIsValid = false;
    }

    public bool Place(string id, Transform arrivalOverride = null)
    {
        if (!catalog.TryGetValue(id ?? "", out StoryRoom room) || player == null)
            return false;

        Transform destination = arrivalOverride != null ? arrivalOverride : room.arrival;
        player.TeleportTo(destination.position);
        SelectRoom(id);
        InteractionDetector detector = player.GetComponentInChildren<InteractionDetector>();

        if (detector != null)
            detector.ClearNearby();

        return true;
    }

    public bool CanTravel()
    {
        return isActiveAndEnabled && !travelling && !StoryCutsceneController.IsPlaying && transition != null && transition.IsConfigured && player != null && playerFreeze != null && !playerFreeze.IsFrozen && !PauseController.IsGamePaused && NPC.ActiveNPC == null && StoryState.Instance != null && StoryState.Instance.IsReady && SaveController.Instance != null && SaveController.Instance.IsReady && !SaveController.Instance.IsLoading;
    }

    public bool Travel(string id, Transform arrivalOverride, bool bubbles)
    {
        if (!CanTravel() || !catalog.ContainsKey(id ?? ""))
            return false;

        StartCoroutine(TravelRoutine(id, arrivalOverride, bubbles));
        return true;
    }

    private IEnumerator TravelRoutine(string id, Transform arrivalOverride, bool bubbles)
    {
        travelling = true;
        previousPause = PauseController.IsGamePaused;
        playerFreeze.FreezePlayer();
        ownsFreeze = true;
        PauseController.SetPause(true);
        yield return transition.Cover(bubbles);
        Place(id, arrivalOverride);
        yield return null;
        yield return null;
        yield return transition.Reveal(bubbles, ResumeMovement);
        Release();
        SaveController.Instance.SaveGame();
    }

    private void Release()
    {
        if (!travelling)
            return;

        transition.Clear();
        PauseController.SetPause(previousPause);

        if (ownsFreeze && playerFreeze != null)
            playerFreeze.UnfreezePlayer();

        ownsFreeze = false;
        travelling = false;
    }

    private void ResumeMovement()
    {
        if (!ownsFreeze)
            return;

        PauseController.SetPause(previousPause);

        if (playerFreeze != null)
            playerFreeze.UnfreezePlayer();

        ownsFreeze = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        Release();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
