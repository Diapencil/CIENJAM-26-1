// Door.cs
// Feature: Opens/closes a door by tweening this object's local position & rotation between a start and end pose using DOTween.
// Usage: Attach to the door pivot object. Set start/end local position & rotation in the inspector,
//        then call Open()/Close()/Toggle() (e.g. wire Toggle() to an Interactable's onInteract event).

using DG.Tweening;
using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Start Pose (local)")]
    [SerializeField] Vector3 startLocalPosition;
    [SerializeField] Vector3 startLocalRotation;

    [Header("End Pose (local)")]
    [SerializeField] Vector3 endLocalPosition;
    [SerializeField] Vector3 endLocalRotation;

    [Header("Tween")]
    [SerializeField, Min(0f)] float duration = 1f;
    [SerializeField] Ease ease = Ease.InOutSine;

    [Header("Init")]
    [SerializeField] bool applyStartPoseOnAwake = true;

    bool isOpen;
    Sequence sequence;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (applyStartPoseOnAwake)
        {
            transform.localPosition = startLocalPosition;
            transform.localEulerAngles = startLocalRotation;
            isOpen = false;
        }
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        Play(endLocalPosition, endLocalRotation);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Play(startLocalPosition, startLocalRotation);
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    void Play(Vector3 targetPosition, Vector3 targetRotation)
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.Join(transform.DOLocalMove(targetPosition, duration).SetEase(ease));
        sequence.Join(transform.DOLocalRotate(targetRotation, duration).SetEase(ease));
        sequence.SetLink(gameObject);
    }

    void OnDestroy()
    {
        sequence?.Kill();
    }
}
