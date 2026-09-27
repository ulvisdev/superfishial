using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class PlayerRevealController : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public Transform player;
    public LayerMask obstructionLayers;

    [Min(0.01f)] public float fadeDuration = 0.3f;
    float revealAmount;

    [Header("Circle")]
    public Vector3 worldOffset = Vector3.zero;
    [Range(0.01f, 0.5f)] public float radius = 0.12f;
    [Range(0.001f, 0.2f)] public float edgeSoftness = 0.04f;
    [Range(0f, 1f)] public float centerOpacity = 0.5f;
    [Min(0f)] public float depthMargin = 0.2f;
    [Min(0.001f)] public float depthSoftness = 0.2f;

    static readonly int EnabledId = Shader.PropertyToID("_SFRevealEnabled");
    static readonly int CenterId = Shader.PropertyToID("_SFRevealCenter");
    static readonly int ShapeId = Shader.PropertyToID("_SFRevealShape");
    static readonly int CameraPositionId = Shader.PropertyToID("_SFRevealCameraPosition");
    static readonly int CameraForwardId = Shader.PropertyToID("_SFRevealCameraForward");
    static readonly int DepthId = Shader.PropertyToID("_SFRevealDepth");

    void OnEnable()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        RenderPipelineManager.beginCameraRendering += UpdateReveal;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= UpdateReveal;
        Shader.SetGlobalFloat(EnabledId, 0f);
        
        revealAmount = 0f;
    }

    void UpdateReveal(ScriptableRenderContext context, Camera renderingCamera)
    {
        Shader.SetGlobalFloat(EnabledId, 0f);

        if (renderingCamera != targetCamera || player == null || targetCamera == null)
            return;

        Vector3 revealPoint = player.position + worldOffset;

        bool blocked = Physics.Linecast(revealPoint, targetCamera.transform.position, obstructionLayers, QueryTriggerInteraction.Ignore);
        revealAmount = Mathf.MoveTowards(revealAmount, blocked ? 1f : 0f, Time.deltaTime / Mathf.Max(fadeDuration, 0.01f));
        
        if (revealAmount <= 0f) 
            return;

        Vector3 center = targetCamera.WorldToViewportPoint(revealPoint);

        if (center.z <= targetCamera.nearClipPlane)
            return;

        Shader.SetGlobalVector(CenterId, new Vector4(center.x, center.y, center.z, targetCamera.aspect));
        Shader.SetGlobalVector(ShapeId, new Vector4(radius, edgeSoftness, Mathf.Lerp(1f, centerOpacity, revealAmount), 0f));
        Shader.SetGlobalVector(CameraPositionId, targetCamera.transform.position);
        Shader.SetGlobalVector(CameraForwardId, targetCamera.transform.forward);
        Shader.SetGlobalVector(DepthId, new Vector4(depthMargin, depthSoftness, 0f, 0f));
        Shader.SetGlobalFloat(EnabledId, 1f);
    }
}
