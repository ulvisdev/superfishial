using UnityEngine;

public class YSort : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    private void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (player == null || spriteRenderer == null || playerSpriteRenderer == null)
            return;

        if (transform.position.z < player.position.z)
            spriteRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + 1;
        else
            spriteRenderer.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
    }
}