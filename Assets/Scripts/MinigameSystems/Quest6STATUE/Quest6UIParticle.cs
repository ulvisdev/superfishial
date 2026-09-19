using UnityEngine;
using UnityEngine.UI;

public class Quest6UIParticle : MonoBehaviour
{
    Vector2 velocity;
    float gravity;
    float rotationSpeed;
    float lifetime;
    float timer;

    RectTransform rectTransform;
    Image image;
    Color startColor;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        startColor = image.color;
    }

    public void Launch(Vector2 startVelocity, float startGravity, float startRotationSpeed, float startLifetime, float scale)
    {
        velocity = startVelocity;
        gravity = startGravity;
        rotationSpeed = startRotationSpeed;
        lifetime = startLifetime;
        transform.localScale = Vector3.one * scale;
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;
        timer += delta;
        velocity.y -= gravity * delta;
        rectTransform.anchoredPosition += velocity * delta;
        rectTransform.Rotate(0f, 0f, rotationSpeed * delta);

        float progress = Mathf.Clamp01(timer / lifetime);
        Color color = startColor;
        color.a = startColor.a * (1f - progress);
        image.color = color;

        if (timer >= lifetime) Destroy(gameObject);
    }
}