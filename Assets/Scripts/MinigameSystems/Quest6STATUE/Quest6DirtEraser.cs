using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Quest6DirtEraser : MonoBehaviour
{
    [Header("References")]
    public Image dirtyImage;
    public Quest6CleaningManager cleaningManager;

    [Header("Brush")]
    public int spongeRadius = 40;
    [Range(0.01f, 1f)]
    public float spongeStrength = 0.12f;

    Texture2D runtimeTexture;
    Sprite runtimeSprite;
    Color[] pixels;

    int textureWidth;
    int textureHeight;

    float totalDirtyAlpha;
    float removedDirtyAlpha;

    Vector2Int lastSpongePixel;
    bool hasLastSpongePixel;

    Canvas canvas;
    RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (dirtyImage == null) dirtyImage = GetComponent<Image>();
        CreateRuntimeTexture();
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (cleaningManager == null) return;

        if (!cleaningManager.IsSpongeSelected())
        {
            hasLastSpongePixel = false;
            return;
        }

        if (Mouse.current.leftButton.isPressed) TryScrub(Mouse.current.position.ReadValue());
        else hasLastSpongePixel = false;
    }

    public Vector2 GetSpongeUISize()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (textureWidth <= 0 || textureHeight <= 0) return Vector2.zero;

        float width = rectTransform.rect.width * (spongeRadius * 2f / textureWidth);
        float height = rectTransform.rect.height * (spongeRadius * 2f / textureHeight);
        return new Vector2(width, height);
    }

    void CreateRuntimeTexture()
    {
        Sprite sourceSprite = dirtyImage.sprite;
        Rect sourceRect = sourceSprite.rect;

        textureWidth = Mathf.RoundToInt(sourceRect.width);
        textureHeight = Mathf.RoundToInt(sourceRect.height);

        Color[] sourcePixels = sourceSprite.texture.GetPixels(Mathf.RoundToInt(sourceRect.x), Mathf.RoundToInt(sourceRect.y), textureWidth, textureHeight);

        runtimeTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        runtimeTexture.filterMode = sourceSprite.texture.filterMode;
        runtimeTexture.SetPixels(sourcePixels);
        runtimeTexture.Apply();

        Vector2 pivot = new Vector2(sourceSprite.pivot.x / sourceRect.width, sourceSprite.pivot.y / sourceRect.height);
        runtimeSprite = Sprite.Create(runtimeTexture, new Rect(0, 0, textureWidth, textureHeight), pivot, sourceSprite.pixelsPerUnit);

        dirtyImage.sprite = runtimeSprite;
        pixels = runtimeTexture.GetPixels();

        totalDirtyAlpha = 0f;
        removedDirtyAlpha = 0f;

        for (int i = 0; i < pixels.Length; i++)
            totalDirtyAlpha += pixels[i].a;
    }

    void TryScrub(Vector2 screenPosition)
    {
        if (!TryGetTexturePixel(screenPosition, out Vector2Int pixel))
        {
            hasLastSpongePixel = false;
            return;
        }

        if (!hasLastSpongePixel)
        {
            lastSpongePixel = pixel;
            hasLastSpongePixel = true;
            return;
        }

        if (pixel == lastSpongePixel)
            return;

        EraseLine(lastSpongePixel, pixel);
        lastSpongePixel = pixel;
    }

    void EraseLine(Vector2Int from, Vector2Int to)
    {
        HashSet<int> touchedPixels = new HashSet<int>();
        float distance = Vector2.Distance(from, to);
        float movementStrength = Mathf.Clamp(distance / 2f, 0.7f, 1.6f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
            EraseCircle(x, y, touchedPixels, movementStrength);
        }

        runtimeTexture.SetPixels(pixels);
        runtimeTexture.Apply();
    }

    bool TryGetTexturePixel(Vector2 screenPosition, out Vector2Int pixel)
    {
        pixel = Vector2Int.zero;
        Camera uiCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, uiCamera, out Vector2 localPoint))
            return false;

        Rect rect = rectTransform.rect;

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        if (normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1)
            return false;

        int pixelX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (textureWidth - 1)), 0, textureWidth - 1);
        int pixelY = Mathf.Clamp(Mathf.RoundToInt(normalizedY * (textureHeight - 1)), 0, textureHeight - 1);

        pixel = new Vector2Int(pixelX, pixelY);

        return true;
    }

    void EraseCircle(int centerX, int centerY, HashSet<int> touchedPixels, float movementStrength)
    {
        int radiusSquared = spongeRadius * spongeRadius;

        for (int y = -spongeRadius; y <= spongeRadius; y++)
        {
            for (int x = -spongeRadius; x <= spongeRadius; x++)
            {
                if (x * x + y * y > radiusSquared) continue;

                int px = centerX + x;
                int py = centerY + y;

                if (px < 0 || px >= textureWidth || py < 0 || py >= textureHeight) continue;

                int index = py * textureWidth + px;

                if (!touchedPixels.Add(index)) continue;
                if (pixels[index].a <= 0f) continue;

                Color pixel = pixels[index];
                float oldAlpha = pixel.a;
                pixel.a = Mathf.Max(0f, pixel.a - spongeStrength * movementStrength);
                removedDirtyAlpha += oldAlpha - pixel.a;
                pixels[index] = pixel;
            }
        }
    }

    public float GetCleanPercent()
    {
        if (totalDirtyAlpha <= 0f)
            return 0f;

        return Mathf.Clamp01(removedDirtyAlpha / totalDirtyAlpha);
    }

    void OnDestroy()
    {
        if (runtimeSprite != null)
            Destroy(runtimeSprite);

        if (runtimeTexture != null)
            Destroy(runtimeTexture);
    }
}