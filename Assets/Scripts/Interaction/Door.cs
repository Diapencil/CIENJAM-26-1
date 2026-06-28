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
            transform.localRotation = Quaternion.Euler(startLocalRotation);
            isOpen = false;
        }
    }

    public void Open()
    {
        Open(null);
    }

    public void Open(System.Action onComplete)
    {
        if (isOpen)
        {
            onComplete?.Invoke();
            return;
        }
        isOpen = true;
        Play(endLocalPosition, endLocalRotation, onComplete);
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

    void Play(Vector3 targetPosition, Vector3 targetRotation, System.Action onComplete = null)
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.Join(transform.DOLocalMove(targetPosition, duration).SetEase(ease));
        sequence.Join(transform.DOLocalRotateQuaternion(Quaternion.Euler(targetRotation), duration).SetEase(ease));
        sequence.SetLink(gameObject);
        if (onComplete != null)
            sequence.OnComplete(() => onComplete());
    }

    void OnDestroy()
    {
        sequence?.Kill();
    }
}
