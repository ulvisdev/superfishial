using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestWaypointManager : MonoBehaviour
{
    [Serializable]
    public class Destination
    {
        public string objectiveID;
        public WaypointTarget waypoint;
        public string objectiveText = "Complete the objective";
    }

    [Serializable]
    public class QuestStep
    {
        public Quest quest;
        public WaypointTarget npcWaypoint;
        public string talkText = "Talk to the quest giver";
        public string returnText = "Return to the quest giver";
        public List<Destination> destinations = new List<Destination>();
    }

    [Header("Playtest Order")]
    [SerializeField] private List<QuestStep> questSequence = new List<QuestStep>();

    [Header("Optional Objective Text")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private string finishedText = "Playtest complete!";
    [SerializeField] private bool hideTextWhenPaused = true;

    private WaypointTarget currentWaypoint;
    private string currentText = "";
    private float nextRefresh;

    private void Start()
    {
        HideManagedWaypoints();
        ValidateSequence();
        RefreshWaypoint();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            RefreshWaypoint();
        }

        if (objectiveText != null) objectiveText.text = hideTextWhenPaused && PauseController.IsGamePaused ? "" : currentText;
    }

    public void RefreshWaypoint()
    {
        QuestController controller = QuestController.Instance;
        if (controller == null) return;

        foreach (QuestStep step in questSequence)
        {
            if (step == null || step.quest == null) continue;
            string questID = step.quest.questID;
            if (controller.IsQuestHandedIn(questID)) continue;

            if (!controller.IsQuestActive(questID))
            {
                SetTarget(step.npcWaypoint, step.talkText);
                return;
            }

            if (controller.IsQuestCompleted(questID))
            {
                SetTarget(step.npcWaypoint, step.returnText);
                return;
            }

            QuestProgress progress = controller.activateQuests.Find(q => q.QuestID == questID);
            foreach (Destination destination in step.destinations)
            {
                if (destination == null) continue;
                QuestObjective objective = progress.objectives.Find(o => o.objectiveID == destination.objectiveID);
                if (objective == null || objective.IsCompleted) continue;
                SetTarget(destination.waypoint, destination.objectiveText);
                return;
            }

            SetTarget(null, "Complete the remaining objectives: " + step.quest.questName);
            return;
        }

        SetTarget(null, finishedText);
    }

    private void SetTarget(WaypointTarget waypoint, string text)
    {
        if (currentWaypoint != waypoint)
        {
            if (currentWaypoint != null) currentWaypoint.Hide();
            currentWaypoint = waypoint;
            if (currentWaypoint != null) currentWaypoint.Show();
        }
        currentText = text;
    }

    private void HideManagedWaypoints()
    {
        foreach (QuestStep step in questSequence)
        {
            if (step == null) continue;
            if (step.npcWaypoint != null) step.npcWaypoint.Hide();
            foreach (Destination destination in step.destinations)
                if (destination != null && destination.waypoint != null) destination.waypoint.Hide();
        }
        currentWaypoint = null;
    }

    private void ValidateSequence()
    {
        foreach (QuestStep step in questSequence)
        {
            if (step == null || step.quest == null)
            {
                Debug.LogWarning("Quest waypoint sequence has an empty quest entry.", this);
                continue;
            }
            if (step.npcWaypoint == null) Debug.LogWarning("Assign an NPC waypoint for " + step.quest.questName, this);
            foreach (Destination destination in step.destinations)
            {
                if (destination == null) continue;
                if (destination.waypoint == null) Debug.LogWarning("Assign a destination waypoint for " + step.quest.questName, this);
                if (step.quest.objectives == null || !step.quest.objectives.Exists(o => o.objectiveID == destination.objectiveID)) Debug.LogWarning("Unknown objective ID '" + destination.objectiveID + "' in " + step.quest.questName, this);
            }
        }
    }

    private void OnDisable()
    {
        HideManagedWaypoints();
        currentText = "";
        if (objectiveText != null) objectiveText.text = "";
    }
}
