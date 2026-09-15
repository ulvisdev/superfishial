using UnityEngine;

public class Quest3CurrentScroller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform currentA;
    [SerializeField] private RectTransform currentB;

    [Header("Scrolling")]
    [SerializeField] private float startingScrollSpeed = 120f;
    [SerializeField] private float maximumScrollSpeed = 260f;
    [SerializeField] private float acceleration = 4f;

    private float currentScrollSpeed;

    private float tileWidth;
    private bool scrolling = false;

    public void BeginMinigame()
    {
        tileWidth = currentA.rect.width;
        currentScrollSpeed = startingScrollSpeed;

        currentA.anchoredPosition = Vector2.zero;
        currentB.anchoredPosition = new Vector2(tileWidth, 0f);

        scrolling = true;
    }

    void Update()
    {
        if (!scrolling)
            return;

        currentScrollSpeed = Mathf.MoveTowards(currentScrollSpeed, maximumScrollSpeed, acceleration * Time.unscaledDeltaTime);
        float movement = currentScrollSpeed * Time.unscaledDeltaTime;

        currentA.anchoredPosition += Vector2.left * movement;
        currentB.anchoredPosition += Vector2.left * movement;

        if (currentA.anchoredPosition.x <= -tileWidth)
            currentA.anchoredPosition += Vector2.right * tileWidth * 2f;

        if (currentB.anchoredPosition.x <= -tileWidth)
            currentB.anchoredPosition += Vector2.right * tileWidth * 2f;
    }

    public void ResetSpeed()
    {
        currentScrollSpeed = startingScrollSpeed;
    }

    public void StopMinigame()
    {
        scrolling = false;
    }

    public void PauseMinigame()
    {
        scrolling = false;
    }

    public void ResumeMinigame()
    {
        scrolling = true;
    }
}