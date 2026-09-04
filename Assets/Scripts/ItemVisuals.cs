using DG.Tweening;
using UnityEngine;

public class ItemVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private ParticleSystem questParticles;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers;

    [Header("Y Axis Spin")]
    [SerializeField] private float spinDuration = 2f;

    [Header("Hop")]
    [SerializeField] private float hopHeight = 0.12f;
    [SerializeField] private float hopPause = 0.35f;
    [SerializeField] private float squashDuration = 0.08f;
    [SerializeField] private float riseDuration = 0.18f;
    [SerializeField] private float fallDuration = 0.16f;
    [SerializeField] private float settleDuration = 0.1f;

    [Header("Squash And Stretch")]
    [SerializeField] private float squashWidth = 1.12f;
    [SerializeField] private float squashHeight = 0.88f;
    [SerializeField] private float stretchWidth = 0.9f;
    [SerializeField] private float stretchHeight = 1.15f;

    private Vector3 restingLocalPosition;
    private Vector3 restingLocalRotation;
    private Vector3 restingLocalScale;
    private Tween spinTween;
    private Sequence hopSequence;
    private bool isAnimating;

    private void Awake()
    {
        if (visual == null)
        {
            enabled = false;
            return;
        }

        restingLocalPosition = visual.localPosition;
        restingLocalRotation = visual.localEulerAngles;
        restingLocalScale = visual.localScale;
    }

    private void StartGroundAnimation()
    {
        if (isAnimating) return;

        isAnimating = true;
        visual.localPosition = restingLocalPosition;
        visual.localEulerAngles = restingLocalRotation;
        visual.localScale = restingLocalScale;

        spinTween = visual.DOLocalRotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);

        Vector3 squashedScale = new Vector3(restingLocalScale.x * squashWidth, restingLocalScale.y * squashHeight, restingLocalScale.z);
        Vector3 stretchedScale = new Vector3(restingLocalScale.x * stretchWidth, restingLocalScale.y * stretchHeight, restingLocalScale.z);

        hopSequence = DOTween.Sequence();
        hopSequence.AppendInterval(hopPause);
        hopSequence.Append(visual.DOScale(squashedScale, squashDuration).SetEase(Ease.OutQuad));
        hopSequence.Append(visual.DOLocalMoveY(restingLocalPosition.y + hopHeight, riseDuration).SetEase(Ease.OutQuad));
        hopSequence.Join(visual.DOScale(stretchedScale, riseDuration).SetEase(Ease.OutQuad));
        hopSequence.Append(visual.DOLocalMoveY(restingLocalPosition.y, fallDuration).SetEase(Ease.InQuad));
        hopSequence.Join(visual.DOScale(squashedScale, fallDuration).SetEase(Ease.InQuad));
        hopSequence.Append(visual.DOScale(restingLocalScale, settleDuration).SetEase(Ease.OutBack));
        hopSequence.SetLoops(-1, LoopType.Restart);
    }

    private void StopGroundAnimation()
    {
        isAnimating = false;
        spinTween?.Kill();
        hopSequence?.Kill();
        spinTween = null;
        hopSequence = null;

        if (visual == null) return;

        visual.localPosition = restingLocalPosition;
        visual.localEulerAngles = restingLocalRotation;
        visual.localScale = restingLocalScale;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsGroundCollision(collision)) StartGroundAnimation();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsGroundCollision(collision)) StartGroundAnimation();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (IsGroundLayer(collision.gameObject)) StopGroundAnimation();
    }

    private bool IsGroundCollision(Collision2D collision)
    {
        if (!IsGroundLayer(collision.gameObject)) return false;

        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > 0.5f) return true;

        return false;
    }

    private bool IsGroundLayer(GameObject target)
    {
        return (groundLayers.value & (1 << target.layer)) != 0;
    }

    public void SetQuestItem(bool isQuestItem)
    {
        if (questParticles == null) return;

        if (isQuestItem)
            if (!questParticles.isPlaying) questParticles.Play();
        else
            questParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        StopGroundAnimation();
    }
}