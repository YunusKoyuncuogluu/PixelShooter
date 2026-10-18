using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dreamteck.Splines;
using PixelShooter.Pooling;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

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
    [SerializeField] private Vector3 flatEuler = Vector3.zero;
    [SerializeField] private Vector3 passengerLocalEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private float boardingYOffset = 0f;

    [Header("Deploy Motion")]
    [SerializeField] private float deployTravelSpeed = 18f;
    [SerializeField] private float deployArcHeight = 2f;

    [Header("Shooter Travel Arc")]
    [SerializeField] private float travelArcHeightMin = 4.5f;
    [SerializeField] private float travelArcHeightMax = 7.5f;

    private Transform cachedTransform;
    private Transform homeRoot;

    [Inject] private ObjectPool objectPool;
    private Tween activeTween;
    private SplineFollower cachedFollower;

    public Transform SeatPoint => seatPoint != null ? seatPoint : cachedTransform;

    public float QueueSpacing => Mathf.Max(0.01f, queueSpacing);
    public Vector3 QueueAxis => queueAxis;
    public bool UseSplineQueueDirection => useSplineQueueDirection;
    public bool QueueOppositeToTravel => queueOppositeToTravel;
    public Vector3 VerticalEuler => verticalEuler;
    public float SlideDuration => Mathf.Max(0.01f, slideDuration);
    public Vector3 FlatEuler => flatEuler;
    public Vector3 PassengerLocalEuler => passengerLocalEuler;
    public float BoardingYOffset => boardingYOffset;
    public float DeployTravelSpeed => Mathf.Max(0.01f, deployTravelSpeed);
    public float DeployArcHeight => Mathf.Max(0f, deployArcHeight);

    private void Awake()
    {
        cachedTransform = transform;
    }
    public float RandomTravelArcHeight()
    {
        float min = Mathf.Max(0f, travelArcHeightMin);
        float max = Mathf.Max(min, travelArcHeightMax);

        return Random.Range(min, max);
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

    private async UniTask FlyToQueueAsync(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        KillTween();
        DetachFollower();
        AttachHome();
        gameObject.SetActive(true);
        await FlyArcAsync(worldPosition, worldRotation, this.GetCancellationTokenOnDestroy());
    }

    public void PlaceInQueue(Vector3 worldPosition, Quaternion worldRotation)
    {
        KillTween();
        DetachFollower();
        AttachHome();
        cachedTransform.position = worldPosition;
        cachedTransform.rotation = worldRotation;
        gameObject.SetActive(true);
    }

    public async UniTask DeployFlatAsync(
        Vector3 flatWorldPosition,
        Quaternion flatWorldRotation,
        CancellationToken cancellationToken)
    {
        KillTween();
        DetachFollower();

        await FlyArcAsync(flatWorldPosition, flatWorldRotation, cancellationToken);
    }

    private async UniTask FlyArcAsync(
        Vector3 worldPosition,
        Quaternion worldRotation,
        CancellationToken cancellationToken)
    {
        Quaternion startRotation = cachedTransform.rotation;
        await gameObject.FollowToAsync(
            objectPool,
            worldPosition,
            DeployTravelSpeed,
            DeployArcHeight,
            progress =>
            {
                if (cachedTransform != null)
                    cachedTransform.rotation = Quaternion.Slerp(startRotation, worldRotation, progress);
            },
            cancellationToken);

        if (cachedTransform == null)
            return;

        cachedTransform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public async UniTask SlideToQueueAsync(
        Vector3 worldPosition,
        Quaternion worldRotation,
        CancellationToken cancellationToken)
    {
        KillTween();
        float duration = SlideDuration;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(cachedTransform.DOMove(worldPosition, duration).SetEase(Ease.OutQuad));
        sequence.Join(cachedTransform.DORotateQuaternion(worldRotation, duration).SetEase(Ease.OutQuad));
        activeTween = sequence;

        try
        {
            await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            if (activeTween == sequence)
                activeTween = null;
        }

        cachedTransform.position = worldPosition;
        cachedTransform.rotation = worldRotation;
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
        KillTween();
        DetachFollower();
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

    private void KillTween()
    {
        if (activeTween == null)
            return;

        activeTween.Kill();
        activeTween = null;
    }

    private void OnDestroy()
    {
        KillTween();
    }

    public class Factory : PlaceholderFactory<Plate>
    {
    }
}
