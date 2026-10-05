using UnityEngine;

public class StoryDepthVisibility : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DepthSystem depthSystem;

    [Header("Story")]
    [SerializeField] private string treatmentFlag = "treatment_complete";
    [SerializeField] private string exteriorRoomID = "worldspawn";

    [Header("Visibility")]
    [SerializeField] private float upgradeStartDepth = 50f;
    [SerializeField] private float upgradeFullDepth = 65f;
    [SerializeField] private float treatedVisibilityMultiplier = 1.5f;

    private static readonly int VisibilityID = Shader.PropertyToID("_SFStoryDepthVisibility");

    private void OnEnable()
    {
        Shader.SetGlobalFloat(VisibilityID, 1f);
    }

    private void LateUpdate()
    {
        float multiplier = 1f;
        StoryState state = StoryState.Instance;
        RoomTravelController travel = RoomTravelController.Instance;

        if (depthSystem != null && state != null && state.IsReady && state.HasFlag(treatmentFlag) && travel != null && travel.CurrentRoomID == exteriorRoomID)
        {
            float progress = Mathf.InverseLerp(upgradeStartDepth, Mathf.Max(upgradeStartDepth + 0.01f, upgradeFullDepth), depthSystem.CurrentDepth);
            multiplier = Mathf.Lerp(1f, Mathf.Max(1f, treatedVisibilityMultiplier), Mathf.SmoothStep(0f, 1f, progress));
        }

        Shader.SetGlobalFloat(VisibilityID, multiplier);
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(VisibilityID, 1f);
    }

    private void OnValidate()
    {
        upgradeStartDepth = Mathf.Max(0f, upgradeStartDepth);
        upgradeFullDepth = Mathf.Max(upgradeStartDepth + 0.01f, upgradeFullDepth);
        treatedVisibilityMultiplier = Mathf.Max(1f, treatedVisibilityMultiplier);
    }
}
