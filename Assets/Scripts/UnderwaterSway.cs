using UnityEngine;

public class UnderwaterSway : MonoBehaviour
{
    [Header("Sway")]
    public Vector3 rotationAmount = new Vector3(0f, 0f, 3f);
    public Vector2 cycleDuration = new Vector2(4f, 7f);

    [Header("Drift")]
    public Vector3 driftAmount = Vector3.zero;

    Vector3 initialPosition;
    Quaternion initialRotation;
    float phase;
    float speed;
    bool initialized;

    void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        phase = Random.Range(0f, Mathf.PI * 2f);
        float minimum = Mathf.Max(0.1f, Mathf.Min(cycleDuration.x, cycleDuration.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(cycleDuration.x, cycleDuration.y));
        speed = Mathf.PI * 2f / Random.Range(minimum, maximum);
        initialized = true;
    }

    void Update()
    {
        phase = Mathf.Repeat(phase + Time.deltaTime * speed, Mathf.PI * 2f);
        float sway = Mathf.Sin(phase);
        transform.localRotation = initialRotation * Quaternion.Euler(rotationAmount * sway);
        transform.localPosition = initialPosition + driftAmount * sway;
    }

    void OnDisable()
    {
        if (!initialized)
            return;

        transform.localPosition = initialPosition;
        transform.localRotation = initialRotation;
    }
}
