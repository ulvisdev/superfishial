using System.Collections;
using UnityEngine;

public class Quest3SkateboardAway : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform skateboard;
    [SerializeField] private Transform awayTarget;
    [SerializeField] private Quest3MinigameTrigger minigameTrigger;

    [Header("Movement")]
    [SerializeField] private float maximumAwaySpeed = 5f;
    [SerializeField] private float acceleration = 3f;

    [Header("Floating")]
    [SerializeField] private float wobbleAmount = 8f;
    [SerializeField] private float wobbleSpeed = 8f;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        triggered = true;

        StartCoroutine(Away());
    }

    private IEnumerator Away()
    {
        Quaternion startRotation = skateboard.rotation;

        float currentSpeed = 0f;
        float elapsed = 0f;

        while (Vector3.Distance(skateboard.position, awayTarget.position) > 0.05f)
        {
            elapsed += Time.deltaTime;
            currentSpeed = Mathf.MoveTowards(currentSpeed, maximumAwaySpeed, acceleration * Time.deltaTime);
            skateboard.position = Vector3.MoveTowards(skateboard.position, awayTarget.position, currentSpeed * Time.deltaTime);
            float wobble = Mathf.Sin(elapsed * wobbleSpeed) * wobbleAmount;
            skateboard.rotation = startRotation * Quaternion.Euler(0f, 0f, wobble);
            yield return null;
        }

        skateboard.position = awayTarget.position;
        skateboard.rotation = startRotation;

        skateboard.gameObject.SetActive(false);

        if (minigameTrigger != null)
            minigameTrigger.ShowArrowIfNeeded();

        gameObject.SetActive(false);
    }
}