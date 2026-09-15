using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Quest3ObstacleOption
{
    public GameObject prefab;

    [Min(0f)]
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
    [SerializeField] private int minimumObstaclesPerWave = 1;
    [SerializeField] private int maximumObstaclesPerWave = 3;
    [SerializeField] private int safeLaneClearance = 0;
    [SerializeField] private float verticalPadding = 50f;

    [Header("Spacing")]
    [SerializeField] private float minimumWaveSpacing = 300f;
    [SerializeField] private float maximumWaveSpacing = 450f;
    [SerializeField] private float spawnPadding = 60f;

    [Header("Speed")]
    [SerializeField] private float startingObstacleSpeed = 200f;
    [SerializeField] private float maximumObstacleSpeed = 400f;
    [SerializeField] private float acceleration = 8f;

    [Header("Reset")]
    [SerializeField] private float resetDelay = 0.35f;

    private readonly List<RectTransform> activeObstacles = new List<RectTransform>();
    private readonly HashSet<RectTransform> obstaclesThatPushedSkateboard = new HashSet<RectTransform>();

    private Coroutine spawnRoutine;

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
                Debug.Log("OBSTACLE HIT SKATEBOARD");
                obstaclesThatPushedSkateboard.Add(obstacle);
                skateboard.PushFromObstacle(obstacle);
            }

            if (RectsOverlap(miniPlayer.GetHitbox(), obstacle))
            {
                StartCoroutine(ResetRun());
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
        ClearObstacles();

        currentObstacleSpeed = startingObstacleSpeed;
        safeLane = laneCount / 2;

        running = true;
        resetting = false;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopMinigame()
    {
        running = false;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = null;

        ClearObstacles();
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSecondsRealtime(0.75f);

        while (running)
        {
            SpawnWave();

            float spacing = UnityEngine.Random.Range(minimumWaveSpacing, maximumWaveSpacing);
            float delay = spacing / Mathf.Max(currentObstacleSpeed, 1f);

            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private void SpawnWave()
    {
        int actualLaneCount = Mathf.Max(2, laneCount);

        safeLane = Mathf.Clamp(safeLane + UnityEngine.Random.Range(-1, 2), 0, actualLaneCount - 1);
        List<int> availableLanes = new List<int>();

        for (int lane = 0; lane < actualLaneCount; lane++)
            if (Mathf.Abs(lane - safeLane) > safeLaneClearance)
                availableLanes.Add(lane);

        int obstacleCount = UnityEngine.Random.Range(minimumObstaclesPerWave, maximumObstaclesPerWave + 1);
        obstacleCount = Mathf.Min(obstacleCount, availableLanes.Count);

        for (int i = 0; i < obstacleCount; i++)
        {
            int laneListIndex = UnityEngine.Random.Range(0, availableLanes.Count);
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
        float totalWeight = 0f;

        foreach (Quest3ObstacleOption option in obstacleOptions)
            if (option.prefab != null)
                totalWeight += Mathf.Max(0f, option.weight);

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);

        foreach (Quest3ObstacleOption option in obstacleOptions)
        {
            if (option.prefab == null)
                continue;

            roll -= Mathf.Max(0f, option.weight);

            if (roll <= 0f)
                return option.prefab;
        }

        return obstacleOptions[obstacleOptions.Length - 1].prefab;
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
        safeLane = laneCount / 2;

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

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private bool RectsOverlap(RectTransform first, RectTransform second)
    {
        return GetWorldRect(first).Overlaps(GetWorldRect(second));
    }

    private Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
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