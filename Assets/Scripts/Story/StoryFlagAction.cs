using UnityEngine;

public class StoryFlagAction : MonoBehaviour
{
    [Header("Flags")]
    [SerializeField] private string[] flagsToSet;
    [SerializeField] private string[] flagsToClear;

    public void Apply()
    {
        StoryState state = StoryState.Instance;

        if (state == null || !state.IsReady)
            return;

        if (flagsToClear != null)
            foreach (string flag in flagsToClear)
                state.ClearFlag(flag);

        if (flagsToSet != null)
            foreach (string flag in flagsToSet)
                state.SetFlag(flag);
    }
}
