using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class EnvironmentScatter : MonoBehaviour
{
    [Serializable]
    public class ScatterGroup
    {
        public string name;
        public GameObject[] prefabs = new GameObject[3];
        public int count = 10;
        public Vector2 scaleMultiplier = new Vector2(0.85f, 1.15f);
        public float footprintRadius = 1f;
        public float surfaceOffset;
        public float maximumSlope = 25f;
        public bool alignToSurface;
        public bool placeRendererBottom = true;
        public Vector2 yaw = new Vector2(0f, 360f);

        public ScatterGroup(string groupName, int amount, float radius)
        {
            name = groupName;
            count = amount;
            footprintRadius = radius;
        }
    }

    [Header("Surface")]
    public MeshCollider terrainCollider;
    public BoxCollider placementArea;
    public BoxCollider[] exclusionAreas = new BoxCollider[0];
    public Vector2 seabedWorldY = new Vector2(-10000f, 10000f);

    [Header("Generation")]
    public int seed = 123;
    public int attemptsPerObject = 60;

    [Header("Rocks And Corals")]
    public ScatterGroup[] groups = { new ScatterGroup("Big Rocks", 8, 3f), new ScatterGroup("Medium Rocks", 16, 1.5f), new ScatterGroup("Small Rocks", 30, 0.6f), new ScatterGroup("Big Corals", 8, 2f), new ScatterGroup("Medium Corals", 16, 1f), new ScatterGroup("Small Corals", 25, 0.5f) };

    [Header("Light Rays")]
    public GameObject[] lightRayPrefabs = new GameObject[1];
    public int lightRayCount = 8;
    public Vector2 lightRayWorldY = new Vector2(0f, 0f);
    public Vector2 lightRayScaleMultiplier = new Vector2(0.85f, 1.15f);
    public Vector2 lightRayYaw = new Vector2(0f, 0f);
    public float lightRaySpacing = 8f;
    public float lightRayMinimumGroundClearance = 1f;

    [SerializeField, HideInInspector] Transform generatedDecorations;
    [SerializeField, HideInInspector] Transform generatedLightRays;

#if UNITY_EDITOR
    struct Placement
    {
        public Vector3 position;
        public float radius;
    }

    float Range(System.Random random, Vector2 range)
    {
        return Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), (float)random.NextDouble());
    }

    List<GameObject> ValidPrefabs(GameObject[] prefabs)
    {
        List<GameObject> result = new List<GameObject>();

        if (prefabs == null)
            return result;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && PrefabUtility.IsPartOfPrefabAsset(prefab))
                result.Add(prefab);
        }

        return result;
    }

    bool ValidateSurface()
    {
        if (Application.isPlaying)
            return false;

        if (terrainCollider == null || !terrainCollider.enabled || !terrainCollider.gameObject.activeInHierarchy || terrainCollider.sharedMesh == null)
        {
            Debug.LogWarning("Assign an active terrain Mesh Collider.", this);
            return false;
        }

        if (terrainCollider.gameObject.scene != gameObject.scene)
        {
            Debug.LogWarning("Terrain must be in the same scene.", this);
            return false;
        }

        return true;
    }

    bool InsideArea(Vector3 point)
    {
        if (placementArea == null)
            return true;

        Vector3 local = placementArea.transform.InverseTransformPoint(point) - placementArea.center;
        Vector3 half = placementArea.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    bool Excluded(Vector3 point, float radius)
    {
        if (exclusionAreas == null)
            return false;

        foreach (BoxCollider area in exclusionAreas)
        {
            if (area == null)
                continue;

            Vector3 local = area.transform.InverseTransformPoint(point) - area.center;
            Vector3 scale = area.transform.lossyScale;
            Vector3 half = area.size * 0.5f;
            half.x += radius / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
            half.y += radius / Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            half.z += radius / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);

            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z)
                return true;
        }

        return false;
    }

    bool SurfacePoint(System.Random random, out RaycastHit hit)
    {
        Bounds bounds = terrainCollider.bounds;
        float x = Range(random, new Vector2(bounds.min.x, bounds.max.x));
        float z = Range(random, new Vector2(bounds.min.z, bounds.max.z));
        Ray ray = new Ray(new Vector3(x, bounds.max.y + 1f, z), Vector3.down);

        if (!terrainCollider.Raycast(ray, out hit, bounds.size.y + 2f))
            return false;

        if (hit.point.y < Mathf.Min(seabedWorldY.x, seabedWorldY.y) || hit.point.y > Mathf.Max(seabedWorldY.x, seabedWorldY.y))
            return false;

        return InsideArea(hit.point);
    }

    bool HasRoom(List<Placement> placed, Vector3 point, float radius)
    {
        foreach (Placement other in placed)
        {
            float x = point.x - other.position.x;
            float z = point.z - other.position.z;
            float distance = radius + other.radius;

            if (x * x + z * z < distance * distance)
                return false;
        }

        return true;
    }

    Transform NewRoot(string rootName)
    {
        GameObject root = new GameObject(rootName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        Undo.RegisterCreatedObjectUndo(root, "Scatter Environment");
        Undo.SetTransformParent(root.transform, transform, "Scatter Environment");

        return root.transform;
    }

    GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, float multiplier)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "Scatter Environment");
        Undo.SetTransformParent(instance.transform, parent, "Scatter Environment");
        Undo.RecordObject(instance.transform, "Scatter Environment");
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = prefab.transform.localScale * multiplier;

        return instance;
    }

    void FinishInstance(GameObject instance)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
    }

    void PlaceBottom(GameObject instance, float surfaceY)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        float bottom = float.PositiveInfinity;

        foreach (Renderer item in renderers)
        {
            if (item is MeshRenderer || item is SkinnedMeshRenderer || item is SpriteRenderer)
                bottom = Mathf.Min(bottom, item.bounds.min.y);
        }

        if (float.IsInfinity(bottom))
            return;

        instance.transform.position += Vector3.up * (surfaceY - bottom);
    }

    public void Generate(bool rays)
    {
        if (!ValidateSurface())
            return;

        List<GameObject> rayPrefabs = ValidPrefabs(lightRayPrefabs);
        List<ScatterGroup> ordered = new List<ScatterGroup>();

        if (groups != null)
        {
            foreach (ScatterGroup group in groups)
            {
                if (group != null && group.count > 0 && ValidPrefabs(group.prefabs).Count > 0)
                    ordered.Add(group);
            }
        }

        if ((rays && (rayPrefabs.Count == 0 || lightRayCount <= 0)) || (!rays && ordered.Count == 0))
        {
            Debug.LogWarning("Assign prefabs and a positive count.", this);
            return;
        }

        ordered.Sort((a, b) => (b.footprintRadius * Mathf.Max(b.scaleMultiplier.x, b.scaleMultiplier.y)).CompareTo(a.footprintRadius * Mathf.Max(a.scaleMultiplier.x, a.scaleMultiplier.y)));
        Physics.SyncTransforms();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(rays ? "Scatter Light Rays" : "Scatter Decorations");
        Undo.RecordObject(this, "Scatter Environment");
        Transform oldRoot = rays ? generatedLightRays : generatedDecorations;

        if (oldRoot != null)
            Undo.DestroyObjectImmediate(oldRoot.gameObject);

        Transform root = NewRoot(terrainCollider.name + (rays ? " - Light Rays" : " - Decorations"));

        if (rays)
            generatedLightRays = root;
        else
            generatedDecorations = root;

        System.Random random = new System.Random(unchecked(seed + (rays ? 7919 : 0)));
        List<Placement> placed = new List<Placement>();
        int requested = 0;
        int created = 0;

        try
        {
            int passes = rays ? 1 : ordered.Count;

            for (int pass = 0; pass < passes; pass++)
            {
                ScatterGroup group = rays ? null : ordered[pass];
                List<GameObject> prefabs = rays ? rayPrefabs : ValidPrefabs(group.prefabs);
                int count = rays ? lightRayCount : group.count;
                requested += count;
                int groupCreated = 0;
                long attempts = (long)count * Mathf.Clamp(attemptsPerObject, 1, 500);

                for (long attempt = 0; attempt < attempts && groupCreated < count; attempt++)
                {
                    if (attempt % 100 == 0 && EditorUtility.DisplayCancelableProgressBar("Scattering", rays ? "Light rays" : group.name, (float)groupCreated / count))
                        throw new OperationCanceledException();

                    if (!SurfacePoint(random, out RaycastHit hit))
                        continue;

                    if (!rays && Vector3.Angle(hit.normal, Vector3.up) > Mathf.Clamp(group.maximumSlope, 0f, 90f))
                        continue;

                    float multiplier = Mathf.Max(0.01f, Range(random, rays ? lightRayScaleMultiplier : group.scaleMultiplier));
                    float radius = rays ? Mathf.Max(0f, lightRaySpacing) * 0.5f : Mathf.Max(0f, group.footprintRadius) * multiplier;
                    Vector3 position = hit.point;

                    if (rays)
                        position.y = Range(random, lightRayWorldY);
                    else
                        position.y += group.surfaceOffset;

                    if (rays && position.y < hit.point.y + Mathf.Max(0f, lightRayMinimumGroundClearance))
                        continue;

                    if (Excluded(hit.point, radius) || !HasRoom(placed, position, radius))
                        continue;

                    GameObject prefab = prefabs[random.Next(prefabs.Count)];
                    Quaternion rotation = Quaternion.AngleAxis(Range(random, rays ? lightRayYaw : group.yaw), Vector3.up) * prefab.transform.localRotation;

                    if (!rays && group.alignToSurface)
                        rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;

                    GameObject instance = Spawn(prefab, root, position, rotation, multiplier);

                    if (!rays && group.placeRendererBottom)
                        PlaceBottom(instance, position.y);

                    FinishInstance(instance);
                    placed.Add(new Placement { position = position, radius = radius });
                    groupCreated++;
                    created++;
                }
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Created " + created + " / " + requested + " objects.", this);
        }
        catch (OperationCanceledException)
        {
            Undo.RevertAllDownToGroup(undoGroup);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception, this);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(EnvironmentScatter))]
public class EnvironmentScatterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Generate Decorations"))
                ((EnvironmentScatter)target).Generate(false);

            if (GUILayout.Button("Generate Light Rays"))
                ((EnvironmentScatter)target).Generate(true);
        }
    }
}
#endif
