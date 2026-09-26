using UnityEngine;

public class QuestObjectiveCompletion : MonoBehaviour
{
    [SerializeField] private Quest quest;
    [SerializeField] private string objectiveID;

    [Header("Optional Reach Location Trigger")]
    [SerializeField] private bool completeOnPlayerEnter;
    [SerializeField] private string playerTag = "Player";

    public void CompleteObjective()
    {
        if (quest == null || QuestController.Instance == null) return;
        QuestController.Instance.CompleteObjective(quest.questID, objectiveID);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!completeOnPlayerEnter) return;
        GameObject enteringObject = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (enteringObject.CompareTag(playerTag)) CompleteObjective();
    }
}
