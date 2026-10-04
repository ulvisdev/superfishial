using System.Collections;
using UnityEngine;

public static class MinigameTime
{
    public static float DeltaTime => PauseController.IsGamePaused ? 0f : Time.unscaledDeltaTime;

    public static IEnumerator Wait(float duration)
    {
        float remaining = Mathf.Max(0f, duration);

        while (remaining > 0f || PauseController.IsGamePaused)
        {
            yield return null;
            remaining -= DeltaTime;
        }
    }
}