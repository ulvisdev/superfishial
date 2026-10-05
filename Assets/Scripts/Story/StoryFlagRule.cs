using UnityEngine;

public class StoryFlagRule : MonoBehaviour
{
    [Header("Condition")]
    [SerializeField] private StoryCondition condition = new();

    [Header("Result")]
    [SerializeField] private string flagToSet;

    private void OnEnable()
    {
        StoryState.Changed += Refresh;
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        StoryState.Changed -= Refresh;
    }

    public void Refresh()
    {
        StoryState state = StoryState.Instance;

        if (state == null || !state.IsReady || string.IsNullOrWhiteSpace(flagToSet))
            return;

        if (state.HasFlag(flagToSet))
            return;

        if (condition != null && !condition.IsMet())
            return;

        state.SetFlag(flagToSet);
    }
}