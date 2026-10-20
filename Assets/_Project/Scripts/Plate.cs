using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dreamteck.Splines;
using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Plate : MonoBehaviour
{
    [SerializeField] private Transform seatPoint;

    [Header("Queue")]
    [SerializeField] private float queueSpacing = 0.14f;
    [SerializeField] private Vector3 queueAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] private bool useSplineQueueDirection = true;
    [SerializeField] private bool queueOppositeToTravel = true;
    [SerializeField] private Vector3 verticalEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private float slideDuration = 0.22f;

    [Header("Boarding Pose")]
    [SerializeField] private Vector3 boardingEuler = new Vector3(0f, 90f, 180f);
    [SerializeField] private float boardingYOffset = 0f;

    [Header("Boarding Motion")]
    [SerializeField] private float boardingDuration = 0.45f;
    [SerializeField] private Ease boardingMoveEase = Ease.InOutQuad;
    [SerializeField] private Ease boardingRotateEase = Ease.InOutQuad;

    [Header("Return Spline")]
    [FormerlySerializedAs("deployTravelSpeed")]
    [SerializeField] private float returnTravelSpeed = 18f;
    [FormerlySerializedAs("deployArcHeight")]
    [SerializeField] private float returnArcHeight = 2f;
    [SerializeField] private float returnDistanceMultiplier = 0.12f;

    private Transform cachedTransform;
    private Transform homeRoot;

    [Inject] private ObjectPool objectPool;
    private Tween activeTween;
    private SplineFollower cachedFollower;
    private CancellationTokenSource moveCts;

    public Transform SeatPoint => seatPoint != null ? seatPoint : cachedTransform;

    public float QueueSpacing => Mathf.Max(0.01f, queueSpacing);
    public Vector3 QueueAxis => queueAxis;
    public bool UseSplineQueueDirection => useSplineQueueDirection;
    public bool QueueOppositeToTravel => queueOppositeToTravel;
    public Vector3 VerticalEuler => verticalEuler;
    public float SlideDuration => Mathf.Max(0.01f, slideDuration);
    public Vector3 BoardingEuler => boardingEuler;
    public float BoardingYOffset => boardingYOffset;
    public float BoardingDuration => Mathf.Max(0.01f, boardingDuration);
    public Ease BoardingMoveEase => boardingMoveEase;
    public Ease BoardingRotateEase => boardingRotateEase;
    public float ReturnTravelSpeed => Mathf.Max(0.01f, returnTravelSpeed);
    public float ReturnArcHeight => Mathf.Max(0f, returnArcHeight);
    public float ReturnDistanceMultiplier => Mathf.Max(0f, returnDistanceMultiplier);

    private void Awake()
    {
        cachedTransform = transform;
    }

    public Vector3 PredictSeatWorldPosition(Vector3 flatWorldPosition, Quaternion flatWorldRotation)
    {
        if (seatPoint == null)
            return flatWorldPosition;

        return flatWorldPosition + flatWorldRotation * seatPoint.localPosition;
    }

    public void SetHome(Transform home)
    {
        homeRoot = home;
        AttachHome();
    }

    public void FlyToQueue(Vector3 worldPosition, Quaternion worldRotation)
    {
        FlyToQueueAsync(worldPosition, worldRotation).Forget();
    }

    private async UniTaskVoid FlyToQueueAsync(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        AttachHome();
        gameObject.SetActive(true);

        CancellationToken token = RestartMove(this.GetCancellationTokenOnDestroy());
        Quaternion startRotation = cachedTransform.rotation;

        try
        {
            await gameObject.FollowToAsync(
                objectPool,
                worldPosition,
                ReturnTravelSpeed,
                ReturnArcHeight,
                progress =>
                {
                    if (cachedTransform != null)
                        cachedTransform.rotation = Quaternion.Slerp(startRotation, worldRotation, progress);
                },
                token,
                ReturnDistanceMultiplier);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || cachedTransform == null)
            return;

        cachedTransform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public void PlaceInQueue(Vector3 worldPosition, Quaternion worldRotation)
    {
        StopMove();
        AttachHome();
        cachedTransform.position = worldPosition;
        cachedTransform.rotation = worldRotation;
        gameObject.SetActive(true);
    }

    public UniTask DeployFlatAsync(
        Vector3 flatWorldPosition,
        Quaternion flatWorldRotation,
        CancellationToken cancellationToken)
    {
        return TweenToAsync(
            flatWorldPosition,
            flatWorldRotation,
            BoardingDuration,
            BoardingMoveEase,
            BoardingRotateEase,
            cancellationToken);
    }

    public UniTask SlideToQueueAsync(
        Vector3 worldPosition,
        Quaternion worldRotation,
        CancellationToken cancellationToken)
    {
        return TweenToAsync(
            worldPosition,
            worldRotation,
            SlideDuration,
            Ease.OutQuad,
            Ease.OutQuad,
            cancellationToken);
    }

    private async UniTask TweenToAsync(
        Vector3 worldPosition,
        Quaternion worldRotation,
        float duration,
        Ease moveEase,
        Ease rotateEase,
        CancellationToken cancellationToken)
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        CancellationToken token = RestartMove(cancellationToken);

        Tween moveTween = cachedTransform
            .DOMove(worldPosition, duration)
            .SetEase(moveEase)
            .SetRecyclable(true);
        cachedTransform
            .DORotateQuaternion(worldRotation, duration)
            .SetEase(rotateEase)
            .SetRecyclable(true);
        activeTween = moveTween;

        try
        {
            await moveTween.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (activeTween == moveTween)
                activeTween = null;
        }

        if (token.IsCancellationRequested || cachedTransform == null)
            return;

        cachedTransform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public void SeatPassenger(Transform passenger)
    {
        if (passenger == null)
            return;

        KillTween();
        Quaternion keptRotation = passenger.rotation;
        passenger.SetParent(SeatPoint, true);
        passenger.rotation = keptRotation;
    }

    public void ClearPassenger(Transform passenger)
    {
        if (passenger != null && passenger.parent == SeatPoint)
            passenger.SetParent(null, true);
    }

    public SplineFollower GetTravelFollower()
    {
        if (cachedFollower == null)
            cachedFollower = GetComponent<SplineFollower>();
        if (cachedFollower == null)
            cachedFollower = gameObject.AddComponent<SplineFollower>();

        return cachedFollower;
    }

    public void DetachFollower()
    {
        if (cachedFollower == null)
            cachedFollower = GetComponent<SplineFollower>();

        if (cachedFollower == null)
            return;

        cachedFollower.follow = false;
        cachedFollower.spline = null;
        cachedFollower = null;
    }

    public void HideAtHome()
    {
        StopMove();
        AttachHome();
        gameObject.SetActive(false);
    }

    private void AttachHome()
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        if (homeRoot == null || cachedTransform.parent == homeRoot)
            return;

        cachedTransform.SetParent(homeRoot, true);
    }

    private CancellationToken RestartMove(CancellationToken externalToken)
    {
        StopMove();

        moveCts = externalToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
            : new CancellationTokenSource();

        return moveCts.Token;
    }

    private void StopMove()
    {
        KillTween();

        if (objectPool != null)
            SplineExtensions.ClearExistingFollowers(gameObject, objectPool);
        else
            DetachFollower();

        cachedFollower = null;

        if (moveCts != null)
        {
            moveCts.Cancel();
            moveCts.Dispose();
            moveCts = null;
        }
    }

    private void KillTween()
    {
        if (cachedTransform != null)
            DOTween.Kill(cachedTransform, false);

        activeTween = null;
    }

    private void OnDestroy()
    {
        if (moveCts != null)
        {
            moveCts.Cancel();
            moveCts.Dispose();
            moveCts = null;
        }

        KillTween();
    }

    public class Factory : PlaceholderFactory<Plate>
    {
    }
}
