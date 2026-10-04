using System;
using UnityEngine;

[Serializable]
public class StoryCondition
{
    public string[] requiredFlags = Array.Empty<string>();
    public string[] forbiddenFlags = Array.Empty<string>();
    public Quest requiredActiveQuest;
    public Quest requiredReadyQuest;
    public Quest requiredHandedInQuest;

    public bool IsMet()
    {
        StoryState state = StoryState.Instance;

        if (state == null || !state.IsReady)
            return false;

        if (requiredFlags != null)
            foreach (string flag in requiredFlags)
                if (!state.HasFlag(flag))
                    return false;

        if (forbiddenFlags != null)
            foreach (string flag in forbiddenFlags)
                if (state.HasFlag(flag))
                    return false;

        QuestController quests = QuestController.Instance;

        if (requiredActiveQuest != null && (quests == null || !quests.IsQuestActive(requiredActiveQuest.questID)))
            return false;

        if (requiredReadyQuest != null && (quests == null || !quests.IsQuestCompleted(requiredReadyQuest.questID)))
            return false;

        if (requiredHandedInQuest != null && (quests == null || !quests.IsQuestHandedIn(requiredHandedInQuest.questID)))
            return false;

        return true;
    }
}
