using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

/// <summary>
/// Conveyor tabagi: kuyrukta dik, boarding'de duz, yolculukta SplineFollower tasiyicisi.
/// Thread seatPoint altina oturur; plate duz kalir.
/// Tum plate ayarlari bu prefab uzerindedir (tek kaynak).
/// </summary>
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

    [Header("Thread Travel Arc")]
    [SerializeField] private float travelArcHeightMin = 4.5f;
    [SerializeField] private float travelArcHeightMax = 7.5f;

    private Transform cachedTransform;
    private Tween activeTween;
    private SplineFollower cachedFollower;
    private bool hasPassenger;

    public Transform SeatPoint => seatPoint != null ? seatPoint : cachedTransform;
    public bool HasPassenger => hasPassenger;
    public SplineFollower CachedFollower => cachedFollower;

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

    public float RollTravelArcHeight()
    {
        float min = Mathf.Max(0f, travelArcHeightMin);
        float max = Mathf.Max(min, travelArcHeightMax);
        return Random.Range(min, max);
    }

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

    public void PlaceInQueue(Vector3 worldPosition, Quaternion worldRotation)
    {
        KillTween();
        DetachFollower();
        hasPassenger = false;
        cachedTransform.SetParent(null, true);
        cachedTransform.position = worldPosition;
        cachedTransform.rotation = worldRotation;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Kuyruktan conveyor basina DynamicSpline FollowTo + duz rotasyon.
    /// </summary>
    public async UniTask AnimateDeployFlatAsync(
        Vector3 flatWorldPosition,
        Quaternion flatWorldRotation,
        CancellationToken cancellationToken)
    {
        KillTween();
        DetachFollower();

        Quaternion startRot = cachedTransform.rotation;
        await gameObject.FollowToAsync(
            flatWorldPosition,
            DeployTravelSpeed,
            DeployArcHeight,
            progress =>
            {
                cachedTransform.rotation = Quaternion.Slerp(startRot, flatWorldRotation, progress);
            },
            cancellationToken);

        cachedTransform.position = flatWorldPosition;
        cachedTransform.rotation = flatWorldRotation;
    }

    public async UniTask AnimateSlideAsync(
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

    /// <summary>
    /// Thread'i seat'e oturtur (yan durus localRotation). Plate duz parent kalir.
    /// </summary>
    public void SeatPassenger(Transform passenger)
    {
        if (passenger == null)
            return;

        KillTween();
        passenger.SetParent(SeatPoint, false);
        passenger.localPosition = Vector3.zero;
        passenger.localRotation = Quaternion.Euler(passengerLocalEuler);
        hasPassenger = true;
    }

    public void ClearPassenger(Transform passenger)
    {
        if (passenger != null && passenger.parent == SeatPoint)
            passenger.SetParent(null, true);

        hasPassenger = false;
    }

    public SplineFollower EnsureFollower()
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

    public void RecycleHide()
    {
        KillTween();
        DetachFollower();
        hasPassenger = false;
        cachedTransform.SetParent(null, true);
        gameObject.SetActive(false);
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
