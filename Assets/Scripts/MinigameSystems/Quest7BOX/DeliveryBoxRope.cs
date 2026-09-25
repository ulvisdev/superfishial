using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class DeliveryBoxRope : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DeliveryBox deliveryBox;
    [SerializeField] private Transform playerAttachment;
    [Tooltip("Top attachment on the box. Your existing reference is preserved.")]
    [SerializeField] private Transform boxAttachment;
    [SerializeField] private Transform bottomBoxAttachment;
    [SerializeField] private SpriteRenderer netOverlay;
    [SerializeField] private bool showNetBeforePickup = true;
    [SerializeField] private Camera viewCamera;

    [Header("Net Rotation")]
    [SerializeField] private Transform netPivot;
    [SerializeField] private bool rotateNetTowardsPlayer = true;
    [SerializeField, Min(0.1f)] private float rotationSmoothness = 8f;
    [SerializeField] private float rotationOffset = 0f;
    [SerializeField, Min(0.001f)] private float rotationDeadZone = 0.1f;

    [Header("Appearance")]
    [SerializeField] private Shader ropeShader;
    [SerializeField] private Color ropeColor = new Color(0.65f, 0.49f, 0.3f, 1f);
    [SerializeField, Range(0f, 1f)] private float opacity = 1f;
    [SerializeField, Min(0.001f)] private float ropeWidth = 0.045f;
    [SerializeField, Range(4, 64)] private int segments = 24;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 1;

    [Header("Two Ropes and Fishnet")]
    [SerializeField] private bool twoRopes = true;
    [SerializeField] private bool showCrossedNet = true;
    [SerializeField, Range(2, 24)] private int netLengthCells = 8;
    [SerializeField, Range(1, 4)] private int netWidthCells = 2;
    [SerializeField, Range(0f, 0.8f)] private float netStart = 0.15f;
    [SerializeField, Range(0.1f, 1f)] private float netStrandWidth = 0.65f;

    [Header("Pixel Style")]
    [SerializeField] private bool pixelated = true;
    [SerializeField, Min(0.005f)] private float pixelSize = 0.0625f;

    [Header("Slack Movement")]
    [SerializeField, Min(0f)] private float maximumSag = 0.55f;
    [SerializeField, Min(0f)] private float swayAmount = 0.08f;
    [SerializeField, Min(0f)] private float swayFrequency = 0.6f;

    private LineRenderer mainLine;
    private ConfigurableJoint tether;
    private PlayerMovement playerMovement;
    private Material runtimeMaterial;
    private Vector3[] topPoints;
    private Vector3[] bottomPoints;
    private readonly Vector3[] strandPoints = new Vector3[2];
    private readonly List<LineRenderer> extraLines = new List<LineRenderer>();
    private readonly List<Vector3> pixelPoints = new List<Vector3>(512);
    private float animationTime;
    private int usedLines;

    private void Awake()
    {
        mainLine = GetComponent<LineRenderer>();
        mainLine.enabled = false;

        if (viewCamera == null) 
            viewCamera = Camera.main;
        if (deliveryBox == null) 
            deliveryBox = GetComponentInParent<DeliveryBox>();
        if (netOverlay != null) 
            netOverlay.enabled = showNetBeforePickup;

        if (ropeShader == null || ropeShader.name != "Superfishial/DeliveryNetUnlit" || !ropeShader.isSupported)
        {
            enabled = false;
            return;
        }

        runtimeMaterial = new Material(ropeShader);
        runtimeMaterial.name = "Delivery Net (Runtime)";
        ConfigureLine(mainLine);
    }

    private void ConfigureLine(LineRenderer line)
    {
        line.sharedMaterial = runtimeMaterial;
        line.SetPropertyBlock(null);
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    private void LateUpdate()
    {
        bool collected = deliveryBox != null && deliveryBox.IsCollected;
        if (netOverlay != null) netOverlay.enabled = showNetBeforePickup || collected;

        if (!collected)
        {
            HideLines();
            tether = null;
            playerMovement = null;
            return;
        }

        if (tether == null)
        {
            tether = deliveryBox.GetComponent<ConfigurableJoint>();

            if (tether != null && tether.connectedBody != null) 
                playerMovement = tether.connectedBody.GetComponent<PlayerMovement>();
        }

        if (tether == null || tether.connectedBody == null)
        {
            HideLines();
            return;
        }

        bool frozen = PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);
        
        if (!frozen) 
            animationTime += Time.deltaTime;

        Color tint = ropeColor;
        tint.a *= opacity;
        runtimeMaterial.SetColor("_Color", tint);

        Vector3 physicalStart = tether.connectedBody.transform.TransformPoint(tether.connectedAnchor);
        Vector3 physicalEnd = tether.transform.TransformPoint(tether.anchor);
        Vector3 start = playerAttachment != null ? playerAttachment.position : physicalStart;

        if (!frozen) 
            UpdateNetRotation(start);

        Vector3 top = boxAttachment != null ? boxAttachment.position : physicalEnd;
        Vector3 bottom = bottomBoxAttachment != null ? bottomBoxAttachment.position : physicalEnd - Vector3.up * 0.4f;
        float slack = Mathf.Clamp01(1f - Vector3.Distance(physicalStart, physicalEnd) / Mathf.Max(0.001f, tether.linearLimit.limit));
        Vector3 direction = (twoRopes ? (top + bottom) * 0.5f : top) - start;
        Vector3 sagDirection = Vector3.ProjectOnPlane(Vector3.down, direction.normalized);

        if (sagDirection.sqrMagnitude < 0.01f) 
            sagDirection = Vector3.ProjectOnPlane(Vector3.right, direction.normalized);

        sagDirection.Normalize();

        int count = Mathf.Clamp(segments, 4, 64) + 1;

        if (topPoints == null || topPoints.Length != count) 
            topPoints = new Vector3[count];
        if (bottomPoints == null || bottomPoints.Length != count) 
            bottomPoints = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float wave = Mathf.Sin(animationTime * swayFrequency * Mathf.PI * 2f + t * Mathf.PI * 2f) * swayAmount;
            Vector3 bend = sagDirection * ((maximumSag + wave) * slack * Mathf.Sin(t * Mathf.PI));
            topPoints[i] = Vector3.Lerp(start, top, t) + bend;
            bottomPoints[i] = Vector3.Lerp(start, bottom, t) + bend;
        }

        usedLines = 0;
        DrawLine(mainLine, topPoints, 1f);

        if (twoRopes) 
            DrawLine(GetExtraLine(), bottomPoints, 1f);
        if (twoRopes && showCrossedNet) 
            DrawNet();

        for (int i = usedLines; i < extraLines.Count; i++) 
            extraLines[i].enabled = false;
    }

    private void UpdateNetRotation(Vector3 playerPosition)
    {
        if (!rotateNetTowardsPlayer || netPivot == null) 
            return;
        if (netPivot == deliveryBox.transform || !netPivot.IsChildOf(deliveryBox.transform)) 
            return;

        Quaternion planeRotation = viewCamera != null ? viewCamera.transform.rotation : Quaternion.identity;
        Vector3 direction = Quaternion.Inverse(planeRotation) * (playerPosition - netPivot.position);
        if (new Vector2(direction.x, direction.y).sqrMagnitude < rotationDeadZone * rotationDeadZone) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        Quaternion target = planeRotation * Quaternion.Euler(0f, 0f, angle);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationSmoothness) * Time.deltaTime);
        netPivot.rotation = Quaternion.Slerp(netPivot.rotation, target, blend);
    }

    private Vector3 NetPoint(float length, float width)
    {
        float index = Mathf.Clamp01(length) * (topPoints.Length - 1);
        int a = Mathf.Min(Mathf.FloorToInt(index), topPoints.Length - 2);
        float t = index - a;
        Vector3 top = Vector3.Lerp(topPoints[a], topPoints[a + 1], t);
        Vector3 bottom = Vector3.Lerp(bottomPoints[a], bottomPoints[a + 1], t);
        return Vector3.Lerp(top, bottom, width);
    }

    private void DrawNet()
    {
        int columns = Mathf.Clamp(netLengthCells, 2, 24);
        int rows = Mathf.Clamp(netWidthCells, 1, 4);
        for (int x = 0; x < columns; x++)
        {
            float a = Mathf.Lerp(netStart, 1f, x / (float)columns);
            float b = Mathf.Lerp(netStart, 1f, (x + 1f) / columns);
            for (int y = 0; y < rows; y++)
            {
                float upper = y / (float)rows;
                float lower = (y + 1f) / rows;
                strandPoints[0] = NetPoint(a, upper);
                strandPoints[1] = NetPoint(b, lower);
                DrawLine(GetExtraLine(), strandPoints, netStrandWidth);
                strandPoints[0] = NetPoint(a, lower);
                strandPoints[1] = NetPoint(b, upper);
                DrawLine(GetExtraLine(), strandPoints, netStrandWidth);
            }
        }
    }

    private LineRenderer GetExtraLine()
    {
        if (usedLines == extraLines.Count)
        {
            GameObject strand = new GameObject("Net Strand " + usedLines);
            strand.layer = gameObject.layer;
            strand.transform.SetParent(transform, false);
            LineRenderer line = strand.AddComponent<LineRenderer>();
            ConfigureLine(line);
            extraLines.Add(line);
        }
        return extraLines[usedLines++];
    }

    private void DrawLine(LineRenderer line, Vector3[] points, float widthScale)
    {
        line.widthMultiplier = (pixelated ? Mathf.Max(0.005f, pixelSize) : ropeWidth) * widthScale;
        line.numCapVertices = pixelated ? 0 : 4;
        line.numCornerVertices = pixelated ? 0 : 4;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;
        if (pixelated) 
            DrawPixelLine(line, points);
        else
        {
            line.positionCount = points.Length;
            line.SetPositions(points);
        }
        line.enabled = true;
    }

    private void DrawPixelLine(LineRenderer line, Vector3[] points)
    {
        float size = Mathf.Max(0.005f, pixelSize);
        Transform cameraTransform = viewCamera != null ? viewCamera.transform : null;
        pixelPoints.Clear();
        Vector3 previous = cameraTransform != null ? cameraTransform.InverseTransformPoint(points[0]) : points[0];
        previous.x = Mathf.Round(previous.x / size) * size;
        previous.y = Mathf.Round(previous.y / size) * size;
        pixelPoints.Add(cameraTransform != null ? cameraTransform.TransformPoint(previous) : previous);

        for (int i = 1; i < points.Length; i++)
        {
            Vector3 target = cameraTransform != null ? cameraTransform.InverseTransformPoint(points[i]) : points[i];
            target.x = Mathf.Round(target.x / size) * size;
            target.y = Mathf.Round(target.y / size) * size;
            Vector3 origin = previous;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Abs(target.x - origin.x), Mathf.Abs(target.y - origin.y)) / size), 1, 128);
            for (int step = 1; step <= steps; step++)
            {
                Vector3 next = Vector3.Lerp(origin, target, step / (float)steps);
                next.x = Mathf.Round(next.x / size) * size;
                next.y = Mathf.Round(next.y / size) * size;
                Vector3 corner = new Vector3(next.x, previous.y, next.z);
                if ((corner - previous).sqrMagnitude > 0.000001f) 
                    pixelPoints.Add(cameraTransform != null ? cameraTransform.TransformPoint(corner) : corner);
                if ((next - corner).sqrMagnitude > 0.000001f) 
                    pixelPoints.Add(cameraTransform != null ? cameraTransform.TransformPoint(next) : next);
                previous = next;
            }
        }

        if (pixelPoints.Count == 1) 
            pixelPoints.Add(pixelPoints[0]);
        line.positionCount = pixelPoints.Count;
        for (int i = 0; i < pixelPoints.Count; i++) 
            line.SetPosition(i, pixelPoints[i]);
    }

    private void HideLines()
    {
        if (mainLine != null) 
            mainLine.enabled = false;
        foreach (LineRenderer line in extraLines) 
            if (line != null) line.enabled = false;
    }

    private void OnDisable()
    {
        HideLines();
    }

    private void OnDestroy()
    {
        foreach (LineRenderer line in extraLines) if (line != null) 

            Destroy(line.gameObject);
        if (runtimeMaterial != null) 
            Destroy(runtimeMaterial);
    }
}
