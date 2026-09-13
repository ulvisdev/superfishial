using UnityEngine;

public class PlayerFreeze : MonoBehaviour
{
    public static PlayerFreeze Instance { get; private set; }

    [SerializeField] private PlayerMovement playerMovement;

    private int freezeCount = 0;

    public bool IsFrozen => freezeCount > 0;

    private void Awake()
    {
        Instance = this;

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    public void FreezePlayer()
    {
        freezeCount++;

        if (freezeCount > 1)
            return;

        playerMovement.SetMovementEnabled(false);
        playerMovement.SetAnimationFrozen(true);
    }

    public void UnfreezePlayer()
    {
        if (freezeCount <= 0)
            return;

        freezeCount--;

        if (freezeCount > 0)
            return;

        playerMovement.SetMovementEnabled(true);
        playerMovement.SetAnimationFrozen(false);
    }
}