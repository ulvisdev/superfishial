using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(DeliveryBox), typeof(Rigidbody))]
public class DeliveryBoxHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer boxSprite;
    [SerializeField] private Transform shakeRoot;
    [SerializeField] private Shader flashShader;
    [SerializeField] private Camera worldCamera;

    [Header("Following Health Bar")]
    [SerializeField] private RectTransform healthBarRoot;
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider delayedHealthBar;
    [SerializeField] private Vector3 barWorldOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField, Min(0.01f)] private float fillSmoothness = 14f;
    [SerializeField, Min(0.01f)] private float trailSmoothness = 3f;
    [SerializeField, Min(0f)] private float trailDelay = 0.3f;
    [SerializeField, Min(0f)] private float barShakePixels = 5f;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool onlyDamageWhenCollected = true;

    [Header("Impact Damage")]
    [SerializeField] private LayerMask damagingLayers = ~0;
    [SerializeField, Min(0f)] private float minimumImpactSpeed = 1.5f;
    [SerializeField, Min(0f)] private float damageMultiplier = 4f;
    [SerializeField, Min(0f)] private float maximumDamagePerHit = 40f;
    [SerializeField, Min(0f)] private float damageCooldown = 0.2f;
    [SerializeField] private bool logImpacts;

    [Header("Hit Feedback")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.22f;
    [SerializeField, Min(0f)] private float hitShakeAmount = 0.04f;
    [SerializeField, Min(0f)] private float hitShakeAngle = 4f;

    [Header("Break and Respawn")]
    [SerializeField, Min(0.01f)] private float deathFadeDuration = 0.6f;
    [SerializeField, Min(0f)] private float deathShakeAmount = 0.13f;
    [SerializeField, Min(0f)] private float deathShakeAngle = 15f;
    [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
    [SerializeField, Range(0, 64)] private int chunkCount = 20;
    [SerializeField] private Color chunkColor = new Color(0.6f, 0.37f, 0.16f, 1f);
    [SerializeField] private Vector2 chunkSize = new Vector2(0.04f, 0.09f);
    [SerializeField] private Vector2 chunkSpeed = new Vector2(0.7f, 2.2f);
    [SerializeField, Min(0.01f)] private float chunkLifetime = 1.3f;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onDamaged = new UnityEvent<float>();
    [SerializeField] private UnityEvent<float> onHealthChanged = new UnityEvent<float>();
    [SerializeField] private UnityEvent onBroken = new UnityEvent();
    [SerializeField] private UnityEvent onReset = new UnityEvent();

    public float CurrentHealth => currentHealth;
    public float HealthFraction => currentHealth / Mathf.Max(1f, maximumHealth);
    public bool IsBroken { get; private set; }

    private DeliveryBox deliveryBox;
    private Rigidbody body;
    private Vector3 spawnPosition, shakePosition;
    private Quaternion spawnRotation, shakeRotation;
    private SpriteRenderer[] sprites;
    private Material[] originalMaterials;
    private Color[] originalColors;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Material flashMaterial, chunkMaterial;
    private Texture2D chunkTexture;
    private Sprite chunkSprite;
    private Canvas barCanvas;
    private CanvasGroup barGroup;
    private float cooldown, flash, shake, trailWait, deathTime;
    private float displayedHealth = 1f, delayedHealth = 1f;
    private bool hasBeenHit;
    private Vector2 barShake;
    private readonly List<Chunk> chunks = new List<Chunk>();
    private class Chunk
    {
        public SpriteRenderer sprite;
        public Vector3 velocity;
        public float spin;
        public float age;
    }
    private bool IsPaused => PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);

    private void Awake()
    {
        deliveryBox = GetComponent<DeliveryBox>();
        body = GetComponent<Rigidbody>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        currentHealth = Mathf.Max(1f, maximumHealth);

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (shakeRoot == transform || (shakeRoot != null && !shakeRoot.IsChildOf(transform)))
            shakeRoot = null;

        if (shakeRoot != null)
        {
            shakePosition = shakeRoot.localPosition;
            shakeRotation = shakeRoot.localRotation;
        }
        sprites = shakeRoot != null ? shakeRoot.GetComponentsInChildren<SpriteRenderer>(true) : GetComponentsInChildren<SpriteRenderer>(true);
        originalMaterials = new Material[sprites.Length];
        originalColors = new Color[sprites.Length];

        if (flashShader != null && flashShader.isSupported)
            flashMaterial = new Material(flashShader);

        for (int i = 0; i < sprites.Length; i++)
        {
            originalMaterials[i] = sprites[i].sharedMaterial;
            originalColors[i] = sprites[i].color;

            if (flashMaterial != null && sprites[i] == boxSprite)
                sprites[i].sharedMaterial = flashMaterial;
        }
        colliders = GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[colliders.Length];

        if (healthBarRoot != null)
        {
            barCanvas = healthBarRoot.GetComponentInParent<Canvas>();
            barGroup = healthBarRoot.GetComponent<CanvasGroup>();

            if (barGroup == null)
                barGroup = healthBarRoot.gameObject.AddComponent<CanvasGroup>();

            barGroup.blocksRaycasts = false;
            barGroup.interactable = false;
            barGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        SetupSlider(healthBar);
        SetupSlider(delayedHealthBar);

        if (healthBar != null && healthBar.fillRect != null && healthBar.fillRect.TryGetComponent(out Image greenFill))
            greenFill.color = Color.green;
        if (delayedHealthBar != null && delayedHealthBar.fillRect != null && delayedHealthBar.fillRect.TryGetComponent(out Image whiteFill))
            whiteFill.color = Color.white;

        onHealthChanged.Invoke(HealthFraction);
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.SetValueWithoutNotify(1f);
    }

    private void Update()
    {
        if (IsPaused)
            return;

        float dt = Time.deltaTime;
        cooldown = Mathf.Max(0f, cooldown - dt);
        flash = Mathf.Max(0f, flash - dt);
        shake = Mathf.Max(0f, shake - dt);
        trailWait = Mathf.Max(0f, trailWait - dt);
        displayedHealth = Mathf.Lerp(displayedHealth, HealthFraction, 1f - Mathf.Exp(-fillSmoothness * dt));

        if (trailWait <= 0f)
            delayedHealth = Mathf.Lerp(delayedHealth, HealthFraction, 1f - Mathf.Exp(-trailSmoothness * dt));

        delayedHealth = Mathf.Max(displayedHealth, delayedHealth);

        if (healthBar != null)
            healthBar.SetValueWithoutNotify(displayedHealth);
        if (delayedHealthBar != null)
            delayedHealthBar.SetValueWithoutNotify(delayedHealth);

        if (IsBroken)
            deathTime += dt;

        UpdateFeedback();
        UpdateChunks(dt);

        if (IsBroken && deathTime >= Mathf.Max(respawnDelay, deathFadeDuration))
            ResetBox();
    }

    private void LateUpdate()
    {
        if (barGroup == null || barCanvas == null || worldCamera == null)
            return;

        Vector3 screen = worldCamera.WorldToScreenPoint(transform.position + barWorldOffset);
        barGroup.alpha = hasBeenHit && !IsBroken && screen.z > 0f ? 1f : 0f;

        if (barGroup.alpha == 0f)
            return;

        screen.x += barShake.x;
        screen.y += barShake.y;
        Camera uiCamera = barCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : barCanvas.worldCamera;
        RectTransform parent = healthBarRoot.parent as RectTransform;

        if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, uiCamera, out Vector3 position))
            healthBarRoot.position = position;
    }

    private void UpdateFeedback()
    {
        float hit = shake / Mathf.Max(0.01f, hitShakeDuration);
        float fade = IsBroken ? 1f - Mathf.Clamp01(deathTime / Mathf.Max(0.01f, deathFadeDuration)) : 1f;
        float strength = IsBroken ? fade * deathShakeAmount : hit * hitShakeAmount;
        float angle = IsBroken ? fade * deathShakeAngle : hit * hitShakeAngle;
        if (shakeRoot != null)
        {
            Vector2 offset = Random.insideUnitCircle * strength;
            shakeRoot.localPosition = shakePosition + new Vector3(offset.x, offset.y, 0f);
            shakeRoot.localRotation = shakeRotation * Quaternion.Euler(0f, 0f, Random.Range(-angle, angle));
        }
        barShake = Random.insideUnitCircle * (barShakePixels * hit);

        if (flashMaterial != null)
            flashMaterial.SetFloat("_Flash", flash / Mathf.Max(0.01f, flashDuration));

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            Color color = originalColors[i];
            color.a *= fade;
            sprites[i].color = color;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled || IsBroken || IsPaused || cooldown > 0f)
            return;
        if (onlyDamageWhenCollected && !deliveryBox.IsCollected)
            return;
        if ((damagingLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        float speed = 0f;

        for (int i = 0; i < collision.contactCount; i++)
            speed = Mathf.Max(speed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal)));

        float excess = Mathf.Max(0f, speed - minimumImpactSpeed);
        float damage = Mathf.Min(maximumDamagePerHit, excess * excess * damageMultiplier);

        if (logImpacts)
            Debug.Log($"Box hit {collision.gameObject.name}: impact {speed:F2}, damage {damage:F1}", this);

        if (damage <= 0f)
            return;
        ApplyDamage(damage);
    }

    private void ApplyDamage(float damage)
    {
        float actualDamage = Mathf.Min(currentHealth, damage);
        currentHealth = Mathf.Max(0f, currentHealth - actualDamage);
        hasBeenHit = true;
        cooldown = damageCooldown;
        flash = flashDuration;
        shake = hitShakeDuration;
        trailWait = trailDelay;

        if (currentHealth <= 0f)
            BreakBox();

        UpdateFeedback();
        onHealthChanged.Invoke(HealthFraction);
        onDamaged.Invoke(actualDamage);

        if (IsBroken)
            onBroken.Invoke();
    }

    private void BreakBox()
    {
        IsBroken = true;
        deathTime = 0f;

        if (barGroup != null)
            barGroup.alpha = 0f;

        deliveryBox.enabled = false;

        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        SpawnChunks();
    }

    private void SpawnChunks()
    {
        if (flashMaterial == null || boxSprite == null)
            return;

        if (chunkSprite == null)
        {
            chunkTexture = new Texture2D(2, 2);
            chunkTexture.filterMode = FilterMode.Point;
            chunkTexture.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            chunkTexture.Apply();
            chunkSprite = Sprite.Create(chunkTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 2f);
            chunkMaterial = new Material(flashMaterial);
            chunkMaterial.SetFloat("_Flash", 0f);
        }
        Quaternion plane = worldCamera != null ? worldCamera.transform.rotation : Quaternion.identity;
        for (int i = 0; i < chunkCount; i++)
        {
            GameObject piece = new GameObject("Box Chunk");
            piece.layer = boxSprite.gameObject.layer;
            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = chunkSprite;
            renderer.sharedMaterial = chunkMaterial;
            renderer.sortingLayerID = boxSprite.sortingLayerID;
            renderer.sortingOrder = boxSprite.sortingOrder + 1;
            renderer.color = chunkColor;
            piece.transform.position = boxSprite.bounds.center + plane * (Vector3)(Random.insideUnitCircle * 0.15f);
            piece.transform.rotation = plane;
            piece.transform.localScale = Vector3.one * Random.Range(chunkSize.x, chunkSize.y);
            Vector2 direction = Random.insideUnitCircle.normalized;
            chunks.Add(new Chunk { sprite = renderer, velocity = plane * (Vector3)(direction * Random.Range(chunkSpeed.x, chunkSpeed.y)), spin = Random.Range(-240f, 240f) });
        }
    }

    private void UpdateChunks(float dt)
    {
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk chunk = chunks[i];
            chunk.age += dt;
            if (chunk.sprite == null || chunk.age >= chunkLifetime)
            {
                if (chunk.sprite != null)
                    Destroy(chunk.sprite.gameObject);

                chunks.RemoveAt(i);
                continue;
            }

            chunk.velocity += Vector3.down * (0.5f * dt);
            chunk.velocity *= Mathf.Exp(-1.2f * dt);
            chunk.sprite.transform.position += chunk.velocity * dt;
            chunk.sprite.transform.Rotate(0f, 0f, chunk.spin * dt, Space.Self);
            Color color = chunkColor;
            color.a *= 1f - chunk.age / Mathf.Max(0.01f, chunkLifetime);
            chunk.sprite.color = color;
        }
    }

    [ContextMenu("Reset Box")]
    public void ResetBox()
    {
        if (!Application.isPlaying || body == null)
            return;

        bool wasBroken = IsBroken;
        deliveryBox.enabled = false;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawnPosition;
        body.rotation = spawnRotation;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        deliveryBox.Release();

        if (wasBroken)
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderStates[i];

        IsBroken = false;
        hasBeenHit = false;
        currentHealth = Mathf.Max(1f, maximumHealth);
        displayedHealth = delayedHealth = 1f;
        cooldown = Mathf.Max(0.3f, damageCooldown);
        flash = shake = trailWait = deathTime = 0f;

        ClearChunks();
        RestoreVisuals();

        if (healthBar != null)
            healthBar.SetValueWithoutNotify(1f);
        if (delayedHealthBar != null)
            delayedHealthBar.SetValueWithoutNotify(1f);
        if (barGroup != null)
            barGroup.alpha = 0f;

        deliveryBox.enabled = true;
        onHealthChanged.Invoke(HealthFraction);
        onReset.Invoke();
    }

    private void RestoreVisuals()
    {
        if (shakeRoot != null)
        {
            shakeRoot.localPosition = shakePosition;
            shakeRoot.localRotation = shakeRotation;
        }
        if (flashMaterial != null)
            flashMaterial.SetFloat("_Flash", 0f);

        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) sprites[i].color = originalColors[i];

        barShake = Vector2.zero;
    }

    private void ClearChunks()
    {
        foreach (Chunk chunk in chunks)
            if (chunk.sprite != null)
                Destroy(chunk.sprite.gameObject);

        chunks.Clear();
    }

    private void OnDisable()
    {
        if (barGroup != null)
            barGroup.alpha = 0f;

        ClearChunks();
        RestoreVisuals();
    }

    private void OnDestroy()
    {
        ClearChunks();

        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) sprites[i].sharedMaterial = originalMaterials[i];

        if (flashMaterial != null)
            Destroy(flashMaterial);
        if (chunkMaterial != null)
            Destroy(chunkMaterial);
        if (chunkSprite != null)
            Destroy(chunkSprite);
        if (chunkTexture != null)
            Destroy(chunkTexture);
    }
}
