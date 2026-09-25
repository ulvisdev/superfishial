using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class WaypointHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform player;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform screenArea;
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private GameObject onScreenIcon;
    [SerializeField] private RectTransform offScreenArrow;
    [SerializeField] private TMP_Text distanceText;

    [Header("Display")]
    [SerializeField] private Vector2 edgePadding = new Vector2(90f, 65f);
    [SerializeField, Min(0.001f)] private float metersPerUnit = 1f;
    [SerializeField] private float arrowAngleOffset = -90f;
    [SerializeField, Min(0f)] private float textGapPixels = 20f;
    [SerializeField, Min(0f)] private float screenMarginPixels = 8f;
    [SerializeField] private bool showLabel = true;
    [SerializeField] private bool hideWhenPaused = true;

    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform iconRect;
    private RectTransform textRect;

    private void Awake()
    {

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null || player == null || canvas == null || screenArea == null || markerRoot == null || onScreenIcon == null || offScreenArrow == null || distanceText == null)
        {
            Debug.LogError("Assign all Waypoint HUD references.", this);
            enabled = false;
            return;
        }

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || markerRoot.parent != screenArea || transform.IsChildOf(markerRoot))
        {
            Debug.LogError("Use an Overlay Canvas, put Marker Root directly under Screen Area, and keep Waypoint HUD outside Marker Root.", this);
            enabled = false;
            return;
        }
        
        iconRect = onScreenIcon.GetComponent<RectTransform>();
        textRect = distanceText.rectTransform;

        if (iconRect == null || iconRect.parent != markerRoot || offScreenArrow.parent != markerRoot || textRect.parent != markerRoot)
        {
            Debug.LogError("Icon, arrow and distance text must be direct UI children of Marker Root.", this);
            enabled = false;
            return;
        }

        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.localRotation = Quaternion.identity;
        distanceText.alignment = TextAlignmentOptions.Top;
        distanceText.margin = Vector4.zero;
        markerRoot.anchorMin = screenArea.pivot;
        markerRoot.anchorMax = screenArea.pivot;
        markerRoot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        WaypointTarget target = GetTarget();

        if (target == null || worldCamera == null || player == null || (hideWhenPaused && PauseController.IsGamePaused))
        {
            markerRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(target.Position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(screenArea, screenPoint, null, out Vector2 localPoint);
        Rect rect = screenArea.rect;
        Vector2 center = rect.center;
        Vector2 halfSize = new Vector2(Mathf.Max(1f, rect.width * 0.5f - edgePadding.x), Mathf.Max(1f, rect.height * 0.5f - edgePadding.y));
        Vector2 direction = localPoint - center;
        bool onScreen = screenPoint.z > 0f && Mathf.Abs(direction.x) <= halfSize.x && Mathf.Abs(direction.y) <= halfSize.y;

        if (onScreen && !target.ShowOnScreen)
        {
            markerRoot.gameObject.SetActive(false);
            return;
        }

        if (!onScreen)
        {

            if (screenPoint.z <= 0f)
                direction = -direction;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.down;

            float scale = Mathf.Max(Mathf.Abs(direction.x) / halfSize.x, Mathf.Abs(direction.y) / halfSize.y);
            localPoint = center + direction / scale;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + arrowAngleOffset;
            offScreenArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        markerRoot.gameObject.SetActive(true);
        markerRoot.anchoredPosition = localPoint;
        onScreenIcon.SetActive(onScreen);
        offScreenArrow.gameObject.SetActive(!onScreen);
        int meters = Mathf.CeilToInt(Vector3.Distance(player.position, target.transform.position) * metersPerUnit);
        distanceText.text = showLabel && !string.IsNullOrEmpty(target.Label) ? $"{target.Label} · {meters} m" : $"{meters} m";
        
        KeepMarkerOnScreen(onScreen ? iconRect : offScreenArrow);
        PositionText(onScreen ? iconRect : offScreenArrow);
    }

    private void PositionText(RectTransform visibleIcon)
    {
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, distanceText.preferredHeight);
        Rect iconBounds = GetScreenBounds(visibleIcon);
        Rect area = GetScreenBounds(screenArea);
        float bottom = Mathf.Max(0f, area.yMin) + screenMarginPixels;
        float top = Mathf.Min(Screen.height, area.yMax) - screenMarginPixels;
        bool above = iconBounds.center.y < (bottom + top) * 0.5f;
        textRect.pivot = new Vector2(0.5f, above ? 0f : 1f);
        float textY = above ? iconBounds.yMax + textGapPixels : iconBounds.yMin - textGapPixels;
        Vector2 textPosition = new Vector2(iconBounds.center.x, textY);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(markerRoot, textPosition, null, out Vector3 position))
            textRect.position = position;

        Rect bounds = GetScreenBounds(textRect);
        float left = Mathf.Max(0f, area.xMin) + screenMarginPixels;
        float right = Mathf.Min(Screen.width, area.xMax) - screenMarginPixels;
        textPosition.x += FitOffset(bounds.xMin, bounds.xMax, left, right);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(markerRoot, textPosition, null, out position))
            textRect.position = position;
            
    }

    private void KeepMarkerOnScreen(RectTransform visibleIcon)
    {
        Rect iconBounds = GetScreenBounds(visibleIcon);
        Rect area = GetScreenBounds(screenArea);
        float left = Mathf.Max(0f, area.xMin) + screenMarginPixels;
        float right = Mathf.Min(Screen.width, area.xMax) - screenMarginPixels;
        float bottom = Mathf.Max(0f, area.yMin) + screenMarginPixels;
        float top = Mathf.Min(Screen.height, area.yMax) - screenMarginPixels;
        float minX = iconBounds.xMin;
        float maxX = iconBounds.xMax;
        float minY = iconBounds.yMin;
        float maxY = iconBounds.yMax;
        Vector2 shift = new Vector2(FitOffset(minX, maxX, left, right), FitOffset(minY, maxY, bottom, top));
        Vector2 rootScreen = RectTransformUtility.WorldToScreenPoint(null, markerRoot.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(screenArea, rootScreen + shift, null, out Vector2 local))
            markerRoot.anchoredPosition = local;

    }

    private float FitOffset(float min, float max, float lower, float upper)
    {
        if (max - min > upper - lower)
            return (lower + upper - min - max) * 0.5f;

        return Mathf.Clamp(0f, lower - min, upper - max);
    }

    private Rect GetScreenBounds(RectTransform rectTransform)
    {
        rectTransform.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = min;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private WaypointTarget GetTarget()
    {
        WaypointTarget best = null;

        for (int i = WaypointTarget.Targets.Count - 1; i >= 0; i--)
        {
            WaypointTarget candidate = WaypointTarget.Targets[i];

            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.Visible)
                continue;

            if (best == null || candidate.Priority > best.Priority)
                best = candidate;
        }

        return best;
    }

    private void OnDisable()
    {
        if (markerRoot != null && markerRoot.gameObject != gameObject)
            markerRoot.gameObject.SetActive(false);
    }
}
