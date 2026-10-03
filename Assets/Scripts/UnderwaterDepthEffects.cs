using UnityEngine;
using UnityEngine.Rendering;

public class UnderwaterDepthEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DepthSystem depthSystem;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Material[] spriteFogMaterials = new Material[0];
    [SerializeField] private Volume deepWaterVolume;

    [Header("Transition")]
    [SerializeField] private float transitionStartDepth = 10f;
    [SerializeField] private float transitionEndDepth = 30f;

    [Header("Deep Water")]
    [SerializeField] private Color deepFogColor = new Color(0.02f, 0.07f, 0.12f, 1f);
    [SerializeField] private float deepFogStart = 5f;
    [SerializeField] private float deepFogEnd = 25f;

    private float originalVolumeWeight;
    private Color shallowFogColor;
    private Color shallowBackgroundColor;
    private float shallowFogStart;
    private float shallowFogEnd;
    private Material[] cachedMaterials;
    private Color[] originalMaterialColors;
    private Vector2[] originalMaterialRanges;
    private bool initialized;
    private bool originalFogEnabled;

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogStartId = Shader.PropertyToID("_FogStart");
    private static readonly int FogEndId = Shader.PropertyToID("_FogEnd");
    private static readonly int FogPlayerPositionId = Shader.PropertyToID("_SFFogPlayerPosition");
    private static readonly int FogEnabledId = Shader.PropertyToID("_SFFogEnabled");

    private void OnEnable()
    {
        if (depthSystem == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        shallowFogColor = RenderSettings.fogColor;
        shallowFogStart = RenderSettings.fogStartDistance;
        shallowFogEnd = RenderSettings.fogEndDistance;

        if (targetCamera != null)
            shallowBackgroundColor = targetCamera.backgroundColor;

        cachedMaterials = (Material[])spriteFogMaterials.Clone();
        originalMaterialColors = new Color[cachedMaterials.Length];
        originalMaterialRanges = new Vector2[cachedMaterials.Length];

        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            Material material = cachedMaterials[i];

            if (material == null)
                continue;

            if (!material.HasProperty(FogColorId) || !material.HasProperty(FogStartId) || !material.HasProperty(FogEndId))
            {
                cachedMaterials[i] = null;
                continue;
            }

            originalMaterialColors[i] = material.GetColor(FogColorId);
            originalMaterialRanges[i] = new Vector2(material.GetFloat(FogStartId), material.GetFloat(FogEndId));
        }

        if (deepWaterVolume != null)
        {
            originalVolumeWeight = deepWaterVolume.weight;
            deepWaterVolume.weight = 0f;
        }

        originalFogEnabled = RenderSettings.fog;
        RenderSettings.fog = false;

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized || depthSystem == null)
            return;

        Vector3 playerPosition = depthSystem.transform.position;
        Shader.SetGlobalVector(FogPlayerPositionId, new Vector4(playerPosition.x, playerPosition.y, playerPosition.z, 1f));
        Shader.SetGlobalFloat(FogEnabledId, 1f);

        float blend = Mathf.InverseLerp(transitionStartDepth, transitionEndDepth, depthSystem.CurrentDepth);
        blend = Mathf.SmoothStep(0f, 1f, blend);

        if (deepWaterVolume != null)
            deepWaterVolume.weight = blend;

        Color fogColor = Color.Lerp(shallowFogColor, deepFogColor, blend);
        float fogStart = Mathf.Lerp(shallowFogStart, deepFogStart, blend);
        float fogEnd = Mathf.Lerp(shallowFogEnd, deepFogEnd, blend);

        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        if (targetCamera != null)
            targetCamera.backgroundColor = Color.Lerp(shallowBackgroundColor, deepFogColor, blend);

        foreach (Material material in cachedMaterials)
        {
            if (material == null)
                continue;

            material.SetColor(FogColorId, fogColor);
            material.SetFloat(FogStartId, fogStart);
            material.SetFloat(FogEndId, fogEnd);
        }
    }

    private void OnDisable()
    {
        if (!initialized)
            return;

        Shader.SetGlobalFloat(FogEnabledId, 0f);
        RenderSettings.fog = originalFogEnabled;

        if (deepWaterVolume != null)
            deepWaterVolume.weight = originalVolumeWeight;

        RenderSettings.fogColor = shallowFogColor;
        RenderSettings.fogStartDistance = shallowFogStart;
        RenderSettings.fogEndDistance = shallowFogEnd;

        if (targetCamera != null)
            targetCamera.backgroundColor = shallowBackgroundColor;

        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            Material material = cachedMaterials[i];

            if (material == null)
                continue;

            material.SetColor(FogColorId, originalMaterialColors[i]);
            material.SetFloat(FogStartId, originalMaterialRanges[i].x);
            material.SetFloat(FogEndId, originalMaterialRanges[i].y);
        }

        initialized = false;
    }

    private void OnValidate()
    {
        transitionStartDepth = Mathf.Max(0f, transitionStartDepth);
        transitionEndDepth = Mathf.Max(transitionStartDepth + 0.1f, transitionEndDepth);
        deepFogStart = Mathf.Max(0f, deepFogStart);
        deepFogEnd = Mathf.Max(deepFogStart + 0.1f, deepFogEnd);
    }
}