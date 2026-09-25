using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class FloatingRock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Floating")]
    [SerializeField, Min(0f)] private float bobHeight = 0.15f;
    [SerializeField, Min(0f)] private float bobFrequency = 0.35f;
    [SerializeField, Min(0f)] private float swayDistance = 0.08f;
    [SerializeField, Min(0f)] private float swayFrequency = 0.2f;
    [SerializeField] private bool randomizePhase = true;

    private Rigidbody body;
    private Vector3 origin;
    private float elapsed;
    private float bobPhase;
    private float swayPhase;

    private bool IsPaused => PauseController.IsGamePaused || (playerMovement != null && !playerMovement.IsMovementEnabled);

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        GetComponent<SphereCollider>().isTrigger = false;

        if (randomizePhase)
        {
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            swayPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        origin = body.position - GetOffset();
    }

    private void FixedUpdate()
    {
        if (IsPaused)
        {
            body.MovePosition(body.position);
            return;
        }

        elapsed += Time.fixedDeltaTime;
        body.MovePosition(origin + GetOffset());
    }

    private Vector3 GetOffset()
    {
        float x = Mathf.Sin(elapsed * swayFrequency * Mathf.PI * 2f + swayPhase) * swayDistance;
        float y = Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f + bobPhase) * bobHeight;
        return new Vector3(x, y, 0f);
    }
}
