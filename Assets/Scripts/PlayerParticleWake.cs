using UnityEngine;

[RequireComponent(typeof(ParticleSystemForceField))]
public class PlayerParticleWake : MonoBehaviour
{
    public Transform movementTarget;
    public float speedForFullStrength = 5f;
    public float pushStrength = 1.5f;
    public float responseSpeed = 8f;
    public float teleportDistance = 3f;

    private ParticleSystemForceField forceField;
    private Vector3 lastPosition;
    private float currentStrength;

    void Awake()
    {
        forceField = GetComponent<ParticleSystemForceField>();
        
        if (movementTarget == null) 
            movementTarget = transform.parent != null ? transform.parent : transform;
    }

    void OnEnable()
    {
        lastPosition = movementTarget.position;
        currentStrength = 0f;
        forceField.gravity = 0f;
    }

    void LateUpdate()
    {
        Vector3 movement = movementTarget.position - lastPosition;
        lastPosition = movementTarget.position;

        if (Time.deltaTime <= 0f) 
            return;

        float speed = movement.magnitude < teleportDistance ? movement.magnitude / Time.deltaTime : 0f;
        float targetStrength = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullStrength)) * pushStrength;
        currentStrength = Mathf.Lerp(currentStrength, targetStrength, 1f - Mathf.Exp(-responseSpeed * Time.deltaTime));
        forceField.gravity = -currentStrength;
    }

    void OnDisable()
    {
        if (forceField != null) forceField.gravity = 0f;
    }
}