using UnityEngine;

public class YSort : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    
    [Tooltip("optional second sprite, drawn directly above the main sprite")]
    [SerializeField] private SpriteRenderer overlaySpriteRenderer;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (playerSpriteRenderer == null && player != null) playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (player == null || spriteRenderer == null || playerSpriteRenderer == null) return;

        bool hasOverlay = overlaySpriteRenderer != null && overlaySpriteRenderer != spriteRenderer;
        int behindOffset = hasOverlay ? 2 : 1;
        spriteRenderer.sortingOrder = transform.position.z < player.position.z ? playerSpriteRenderer.sortingOrder + 1 : playerSpriteRenderer.sortingOrder - behindOffset;

        if (!hasOverlay) return;
        overlaySpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        overlaySpriteRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
    }
}
