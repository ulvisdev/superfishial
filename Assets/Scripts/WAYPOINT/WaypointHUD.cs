using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class WaypointHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform screenArea;
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private GameObject onScreenIcon;
    [SerializeField] private RectTransform offScreenArrow;
    [SerializeField] private TMP_Text distanceText;

    [Header("Display")]
    [SerializeField] private float screenMarginPixels = 8f;
    [SerializeField] private bool hideWhenPaused = true;

    [Header("Obstructions")]
    [SerializeField] private bool hideBehindObstacles;
    [SerializeField] private LayerMask obstructionLayers;

    private readonly List<RectTransform> markers = new();
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform iconTemplate;
    private bool configured;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvas == null || screenArea == null || markerRoot == null || onScreenIcon == null)
        {
            Debug.LogError("Assign Canvas, Screen Area, Marker Root and On Screen Icon.", this);
            enabled = false;
            return;
        }

        iconTemplate = onScreenIcon.GetComponent<RectTransform>();

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || markerRoot.parent != screenArea || transform.IsChildOf(markerRoot) || iconTemplate == null || iconTemplate.parent != markerRoot)
        {
            Debug.LogError("Use an Overlay Canvas, Marker Root under Screen Area, and an Icon directly under Marker Root. Keep this controller outside Marker Root.", this);
            enabled = false;
            return;
        }

        if (offScreenArrow != null)
            offScreenArrow.gameObject.SetActive(false);

        if (distanceText != null)
            distanceText.gameObject.SetActive(false);

        foreach (UnityEngine.UI.Graphic graphic in onScreenIcon.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;

        markers.Add(iconTemplate);
        onScreenIcon.SetActive(false);
        markerRoot.gameObject.SetActive(false);
        configured = true;
    }

    private void LateUpdate()
    {
        if (!configured)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null || !worldCamera.isActiveAndEnabled || StoryCutsceneController.IsPlaying || RoomTravelController.IsTravelling || (hideWhenPaused && PauseController.IsGamePaused) || StoryState.Instance == null || !StoryState.Instance.IsReady)
        {
            HideMarkers();
            return;
        }

        int used = 0;
        markerRoot.gameObject.SetActive(true);

        foreach (WaypointTarget target in WaypointTarget.Targets)
        {
            if (target == null || !target.isActiveAndEnabled || !target.Visible || !target.ShowOnScreen)
                continue;

            Vector3 point = worldCamera.WorldToScreenPoint(target.Position);

            if (point.z < worldCamera.nearClipPlane || point.z > worldCamera.farClipPlane || !InsideScreen(point))
                continue;

            if (hideBehindObstacles && Physics.Linecast(worldCamera.transform.position, target.Position, obstructionLayers, QueryTriggerInteraction.Ignore))
                continue;

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(screenArea, point, null, out Vector3 position))
                continue;

            RectTransform marker = GetMarker(used);
            marker.position = position;
            marker.localRotation = Quaternion.identity;
            marker.gameObject.SetActive(true);
            marker.GetWorldCorners(corners);
            bool fits = true;

            foreach (Vector3 corner in corners)
                if (!InsideScreen(RectTransformUtility.WorldToScreenPoint(null, corner)))
                {
                    fits = false;
                    break;
                }

            if (!fits)
            {
                marker.gameObject.SetActive(false);
                continue;
            }

            used++;
        }

        for (int i = used; i < markers.Count; i++)
            markers[i].gameObject.SetActive(false);

        markerRoot.gameObject.SetActive(used > 0);
    }

    private bool InsideScreen(Vector2 point)
    {
        Rect rect = worldCamera.pixelRect;
        float margin = Mathf.Max(0f, screenMarginPixels);

        if (point.x < rect.xMin + margin || point.x > rect.xMax - margin || point.y < rect.yMin + margin || point.y > rect.yMax - margin)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(screenArea, point, null, out Vector2 local) && screenArea.rect.Contains(local);
    }

    private RectTransform GetMarker(int index)
    {
        if (index < markers.Count)
            return markers[index];

        RectTransform marker = Instantiate(iconTemplate, markerRoot);
        marker.name = "ObjectiveMarker";
        markers.Add(marker);
        return marker;
    }

    private void HideMarkers()
    {
        if (markerRoot != null && !transform.IsChildOf(markerRoot))
            markerRoot.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        HideMarkers();
    }

    private void OnDestroy()
    {
        for (int i = 1; i < markers.Count; i++)
            if (markers[i] != null)
                Destroy(markers[i].gameObject);
    }
}
