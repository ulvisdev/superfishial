using UnityEngine;

public class StoryObjectState : MonoBehaviour
{
    [Header("Condition")]
    [SerializeField] private StoryCondition condition = new();

    [Header("Objects")]
    [SerializeField] private GameObject[] activeWhenMet;
    [SerializeField] private GameObject[] activeWhenUnmet;

    private void OnEnable()
    {
        StoryState.Changed += Refresh;
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (StoryState.Instance == null || !StoryState.Instance.IsReady)
            return;

        bool met = condition == null || condition.IsMet();
        Apply(activeWhenMet, met);
        Apply(activeWhenUnmet, !met);
    }

    private void Apply(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        foreach (GameObject target in targets)
        {
            if (target == null)
                continue;

            if (transform.IsChildOf(target.transform))
            {
                Debug.LogError("StoryObjectState must stay outside the objects it controls.", this);
                continue;
            }

            target.SetActive(active);
        }
    }

    private void OnDisable()
    {
        StoryState.Changed -= Refresh;
    }
}
