using UnityEngine;

public class MapUnlockView : MonoBehaviour
{
    [SerializeField] private string unlockedFlag = "map_unlocked";
    [SerializeField] private GameObject mapImage;
    [SerializeField] private GameObject lockedMessage;

    private void OnEnable()
    {
        StoryState.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        StoryState.Changed -= Refresh;
    }

    private void Refresh()
    {
        bool unlocked = StoryState.Instance != null && StoryState.Instance.IsReady && StoryState.Instance.HasFlag(unlockedFlag);

        if (mapImage != null)
            mapImage.SetActive(unlocked);

        if (lockedMessage != null)
            lockedMessage.SetActive(!unlocked);
    }
}
