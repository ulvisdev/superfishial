using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Quest3ObstacleOption
{
    public GameObject prefab;

    public float weight = 1f;
}

public class Quest3ObstacleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform playArea;
    [SerializeField] private RectTransform obstacleContainer;
    [SerializeField] private Quest3MiniPlayer miniPlayer;
    [SerializeField] private Quest3CurrentScroller currentScroller;
    [SerializeField] private Quest3MinigameSkateboard skateboard;

    [Header("Obstacle Types")]
    [SerializeField] private Quest3ObstacleOption[] obstacleOptions;

    [Header("Lanes")]
    [SerializeField] private int laneCount = 5;
    [SerializeField] private int minimumObstaclesPerWave = 2;
    [SerializeField] private int maximumObstaclesPerWave = 3;
    [SerializeField] private int safeLaneClearance = 0;
    [SerializeField] private float verticalPadding = 50f;

    [Header("Spacing")]
    [SerializeField] private float minimumWaveSpacing = 280f;
    [SerializeField] private float maximumWaveSpacing = 360f;
    [SerializeField] private float spawnPadding = 60f;

    [Header("Speed")]
    [SerializeField] private float startingObstacleSpeed = 180f;
    [SerializeField] private float maximumObstacleSpeed = 300f;
    [SerializeField] private float acceleration = 5f;

    [Header("Warmup")]
    [SerializeField] private float firstWaveDelay = 0.35f;
    [SerializeField] private int warmupWaves = 1;

    [Header("Reset")]
    [SerializeField] private float resetDelay = 0.45f;

    private readonly List<RectTransform> activeObstacles = new List<RectTransform>();
    private readonly HashSet<RectTransform> obstaclesThatPushedSkateboard = new HashSet<RectTransform>();

    private Coroutine spawnRoutine;
    private Coroutine resetRoutine;
    private readonly Vector3[] corners = new Vector3[4];
    private int wavesSpawned;

    private bool running = false;
    private bool resetting = false;

    private float currentObstacleSpeed;
    private int safeLane;

    void Update()
    {
        if (!running || resetting)
            return;

        currentObstacleSpeed = Mathf.MoveTowards(currentObstacleSpeed, maximumObstacleSpeed, acceleration * Time.unscaledDeltaTime);

        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            RectTransform obstacle = activeObstacles[i];

            if (obstacle == null)
            {
                activeObstacles.RemoveAt(i);
                continue;
            }

            obstacle.anchoredPosition += Vector2.left * currentObstacleSpeed * Time.unscaledDeltaTime;

            if (skateboard != null && skateboard.CanBePushed() && RectsOverlap(skateboard.GetHitbox(), obstacle) && !obstaclesThatPushedSkateboard.Contains(obstacle))
            {
                obstaclesThatPushedSkateboard.Add(obstacle);
                skateboard.PushFromObstacle(obstacle);
            }

            if (RectsOverlap(miniPlayer.GetHitbox(), obstacle))
            {
                resetRoutine = StartCoroutine(ResetRun());
                return;
            }

            if (obstacle.anchoredPosition.x < playArea.rect.xMin - obstacle.rect.width)
            {
                obstaclesThatPushedSkateboard.Remove(obstacle);
                Destroy(obstacle.gameObject);
                activeObstacles.RemoveAt(i);
            }
        }
    }

    public void BeginMinigame()
    {
        StopMinigame();
        wavesSpawned = 0;

        currentObstacleSpeed = startingObstacleSpeed;
        safeLane = UnityEngine.Random.value < 0.5f ? 0 : Mathf.Max(2, laneCount) - 1;

        running = true;
        resetting = false;

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopMinigame()
    {
        running = false;
        resetting = false;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = null;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = null;

        ClearObstacles();
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, firstWaveDelay));

        while (running)
        {
            SpawnWave();

            float spacing = UnityEngine.Random.Range(Mathf.Max(1f, minimumWaveSpacing), Mathf.Max(1f, Mathf.Max(minimumWaveSpacing, maximumWaveSpacing)));
            float distance = 0f;

            while (running && distance < spacing)
            {
                yield return null;
                distance += currentObstacleSpeed * Time.unscaledDeltaTime;
            }
        }
    }

    private void SpawnWave()
    {
        int actualLaneCount = Mathf.Max(2, laneCount);

        int laneStep = UnityEngine.Random.value < 0.5f ? -1 : 1;

        if (safeLane <= 0)
            laneStep = 1;
        else if (safeLane >= actualLaneCount - 1)
            laneStep = -1;

        safeLane = Mathf.Clamp(safeLane + laneStep, 0, actualLaneCount - 1);
        List<int> availableLanes = new List<int>();

        for (int lane = 0; lane < actualLaneCount; lane++)
            if (Mathf.Abs(lane - safeLane) > Mathf.Max(0, safeLaneClearance))
                availableLanes.Add(lane);

        int minimumCount = Mathf.Max(1, minimumObstaclesPerWave);
        int maximumCount = Mathf.Max(minimumCount, maximumObstaclesPerWave);
        int obstacleCount = wavesSpawned < warmupWaves ? 1 : UnityEngine.Random.Range(minimumCount, maximumCount + 1);
        wavesSpawned++;
        obstacleCount = Mathf.Min(obstacleCount, availableLanes.Count);

        for (int i = 0; i < obstacleCount; i++)
        {
            int centerLaneIndex = availableLanes.IndexOf(actualLaneCount / 2);
            int laneListIndex = i == 0 && centerLaneIndex >= 0 ? centerLaneIndex : UnityEngine.Random.Range(0, availableLanes.Count);
            int lane = availableLanes[laneListIndex];

            availableLanes.RemoveAt(laneListIndex);

            SpawnObstacleInLane(lane, actualLaneCount);
        }
    }

    private void SpawnObstacleInLane(int lane, int actualLaneCount)
    {
        GameObject prefab = GetRandomObstaclePrefab();

        if (prefab == null)
            return;

        GameObject obstacleObject = Instantiate(prefab, obstacleContainer);
        RectTransform obstacle = obstacleObject.GetComponent<RectTransform>();

        if (obstacle == null)
        {
            Destroy(obstacleObject);
            return;
        }

        float minimumY = playArea.rect.yMin + verticalPadding;
        float maximumY = playArea.rect.yMax - verticalPadding;
        float laneT = lane / (float)(actualLaneCount - 1);

        float x = playArea.rect.xMax + obstacle.rect.width * 0.5f + spawnPadding;
        float y = Mathf.Lerp(minimumY, maximumY, laneT);

        obstacle.anchoredPosition = new Vector2(x, y);

        activeObstacles.Add(obstacle);
    }

    private GameObject GetRandomObstaclePrefab()
    {
        if (obstacleOptions == null || obstacleOptions.Length == 0)
            return null;

        float totalWeight = 0f;

        foreach (Quest3ObstacleOption option in obstacleOptions)
            if (option != null && option.prefab != null)
                totalWeight += Mathf.Max(0f, option.weight);

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);

        foreach (Quest3ObstacleOption option in obstacleOptions)
        {
            if (option == null || option.prefab == null || option.weight <= 0f)
                continue;

            roll -= Mathf.Max(0f, option.weight);

            if (roll <= 0f)
                return option.prefab;
        }

        return null;
    }

    private IEnumerator ResetRun()
    {
        if (resetting)
            yield break;

        resetting = true;
        running = false;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = null;

        miniPlayer.PauseMinigame();

        if (skateboard != null)
            skateboard.PauseForReset();

        if (currentScroller != null)
            currentScroller.PauseMinigame();

        yield return new WaitForSecondsRealtime(resetDelay);

        ClearObstacles();

        currentObstacleSpeed = startingObstacleSpeed;
        safeLane = UnityEngine.Random.value < 0.5f ? 0 : Mathf.Max(2, laneCount) - 1;

        wavesSpawned = 0;
        miniPlayer.ResetPosition();

        if (skateboard != null)
            skateboard.ResetTarget();

        if (currentScroller != null)
        {
            currentScroller.ResetSpeed();
            currentScroller.ResumeMinigame();
        }

        miniPlayer.ResumeMinigame();

        running = true;
        resetting = false;

        resetRoutine = null;
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private bool RectsOverlap(RectTransform first, RectTransform second)
    {
        return first != null && second != null && GetWorldRect(first).Overlaps(GetWorldRect(second));
    }

    private Rect GetWorldRect(RectTransform rectTransform)
    {
        rectTransform.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[0].x;
        float minY = corners[0].y;
        float maxY = corners[0].y;

        for (int i = 1; i < 4; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private void ClearObstacles()
    {
        foreach (RectTransform obstacle in activeObstacles)
            if (obstacle != null)
                Destroy(obstacle.gameObject);

        activeObstacles.Clear();
        obstaclesThatPushedSkateboard.Clear();
    }
}
