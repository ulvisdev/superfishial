using UnityEngine;
using Unity.Cinemachine;

// Cinemachine 3.x. Add to the CinemachineCamera, not the Main Camera.
// Use Follow / World Space / Rotation Control None. Disable other collision extensions.
// Intended for open terrain with upward-facing 3D colliders, not cave ceilings.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
[AddComponentMenu("Cinemachine/Extensions/Terrain Camera Lift")]
public class TerrainCameraLift : CinemachineExtension
{
    [Header("Target")]
    public Transform player;
    public Vector3 targetOffset = Vector3.zero;

    [Header("Ground")]
    public LayerMask groundLayers;
    [Min(0.05f)] public float cameraRadius = 0.6f;
    [Min(0f)] public float extraClearance = 0.8f;
    [Min(0.1f)] public float maximumLift = 15f;
    [Min(0.01f)] public float riseSmoothTime = 0.15f;
    [Min(0.01f)] public float returnSmoothTime = 0.5f;

    [Header("Fixed View")]
    [Range(-80f, 80f)] public float normalPitch = 16.3f;

    public float fixedYaw = 0f;
    [Min(0.01f)] public float tiltSmoothTime = 0.12f;

    public float CurrentLift => currentLift;
    public bool HeightLimitReached { get; private set; }

    float currentLift;
    float liftVelocity;
    float currentPitch;
    float pitchVelocity;
    bool initialized;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) 
            return;

        Transform target = player != null ? player : vcam.Follow;

        if (target == null) 
            return;

        Vector3 baseline = state.RawPosition + state.PositionCorrection;
        float radius = Mathf.Max(0.05f, cameraRadius);
        float liftLimit = Mathf.Max(0.1f, maximumLift);
        float buffer = Mathf.Max(0f, extraClearance);
        float probeHeight = liftLimit + radius + buffer + 1f;

        Vector3 origin = baseline + Vector3.up * probeHeight;
        float minimumLift = 0f;
        float desiredLift = 0f;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, probeHeight + buffer + radius + 2f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            float safeCenterY = origin.y - hit.distance + 0.05f;
            minimumLift = Mathf.Max(0f, safeCenterY - baseline.y);
            desiredLift = Mathf.Max(0f, safeCenterY + buffer - baseline.y);
        }

        HeightLimitReached = minimumLift > liftLimit || Physics.CheckSphere(origin, radius, groundLayers, QueryTriggerInteraction.Ignore);
        desiredLift = Mathf.Min(desiredLift, liftLimit);
        minimumLift = Mathf.Min(minimumLift, liftLimit);
        bool reset = !initialized || deltaTime < 0f || !Application.isPlaying;

        if (reset)
        {
            currentLift = desiredLift;
            liftVelocity = 0f;
            pitchVelocity = 0f;
        }
        else if (deltaTime > 0f)
        {
            float smoothTime = desiredLift > currentLift ? riseSmoothTime : returnSmoothTime;
            currentLift = Mathf.SmoothDamp(currentLift, desiredLift, ref liftVelocity, Mathf.Max(0.01f, smoothTime), Mathf.Infinity, deltaTime);
        }

        if (currentLift < minimumLift)
        {
            currentLift = minimumLift;
            liftVelocity = Mathf.Max(0f, liftVelocity);
        }

        currentLift = Mathf.Clamp(currentLift, 0f, liftLimit);
        Vector3 aimPoint = target.position + targetOffset;
        Vector3 flatForward = Quaternion.Euler(0f, fixedYaw, 0f) * Vector3.forward;
        float forwardDistance = Mathf.Max(0.1f, Vector3.Dot(aimPoint - baseline, flatForward));
        float baseAngle = Mathf.Atan2(baseline.y - aimPoint.y, forwardDistance) * Mathf.Rad2Deg;
        float liftedAngle = Mathf.Atan2(baseline.y + currentLift - aimPoint.y, forwardDistance) * Mathf.Rad2Deg;
        float desiredPitch = Mathf.Clamp(normalPitch + liftedAngle - baseAngle, -80f, 80f);

        if (reset) currentPitch = desiredPitch;
        else if (deltaTime > 0f) currentPitch = Mathf.SmoothDamp(currentPitch, desiredPitch, ref pitchVelocity, Mathf.Max(0.01f, tiltSmoothTime), Mathf.Infinity, deltaTime);

        state.PositionCorrection += Vector3.up * currentLift;

        state.RawOrientation = Quaternion.Euler(currentPitch, fixedYaw, 0f);
        state.OrientationCorrection = Quaternion.identity;
        state.Lens.Dutch = 0f;
        initialized = true;
    }
}
