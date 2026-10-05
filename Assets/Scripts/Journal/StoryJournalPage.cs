using System;
using UnityEngine;

[CreateAssetMenu(fileName = "JournalPage", menuName = "Story/Journal Page")]
public class StoryJournalPage : ScriptableObject
{
    [Header("Availability")]
    public StoryCondition unlockCondition = new();

    [Header("Artwork")]
    public Sprite defaultSprite;
    public JournalArtworkRule[] artworkRules = Array.Empty<JournalArtworkRule>();

    [Header("Quest")]
    public Quest quest;

    public Sprite GetArtwork()
    {
        foreach (JournalArtworkRule rule in artworkRules ?? Array.Empty<JournalArtworkRule>())
            if (rule != null && rule.sprite != null && (rule.condition == null || rule.condition.IsMet()))
                return rule.sprite;

        return defaultSprite;
    }
}

[Serializable]
public class JournalArtworkRule
{
    public StoryCondition condition = new();
    public Sprite sprite;
}
