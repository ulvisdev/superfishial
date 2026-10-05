using UnityEngine;
using UnityEngine.Events;

public class StoryTrigger : MonoBehaviour
{
    [Header("Condition")]
    [SerializeField] private StoryCondition condition = new();
    [SerializeField] private string completedFlag;

    [Header("Action")]
    [SerializeField] private UnityEvent onTriggered;

    private bool running;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;

        TryTrigger();
    }

    public void TryTrigger()
    {
        StoryState state = StoryState.Instance;

        if (running || state == null || !state.IsReady || state.HasFlag(completedFlag))
            return;

        if (condition != null && !condition.IsMet())
            return;

        running = true;
        onTriggered?.Invoke();
    }

    public void Complete()
    {
        if (!running)
            return;

        StoryState.Instance.SetFlag(completedFlag);
        running = false;
    }

    public void Cancel()
    {
        running = false;
    }
}
