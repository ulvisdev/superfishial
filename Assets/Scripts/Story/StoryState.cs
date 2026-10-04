using System;
using System.Collections.Generic;
using UnityEngine;

public class StoryState : MonoBehaviour
{
    public static StoryState Instance { get; private set; }
    public static event Action Changed;

    [Header("Flags")]
    [SerializeField] private List<string> flags = new();

    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool HasFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && flags.Contains(flag);
    }

    public void SetFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || flags.Contains(flag))
            return;

        flags.Add(flag);
        NotifyChanged();
    }

    public void ClearFlag(string flag)
    {
        if (!flags.Remove(flag))
            return;

        NotifyChanged();
    }

    public List<string> GetFlags()
    {
        return new List<string>(flags);
    }

    public void RestoreFlags(List<string> savedFlags)
    {
        flags.Clear();

        if (savedFlags == null)
            return;

        foreach (string flag in savedFlags)
            if (!string.IsNullOrWhiteSpace(flag) && !flags.Contains(flag))
                flags.Add(flag);
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;

        if (ready)
            NotifyChanged();
    }

    public static void NotifyChanged()
    {
        if (Instance == null || !Instance.IsReady)
            return;

        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
