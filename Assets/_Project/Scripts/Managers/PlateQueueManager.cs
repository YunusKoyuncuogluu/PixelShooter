using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using LevelManagement;
using UnityEngine;
using Zenject;

public class PlateQueueManager : MonoBehaviour
{
    private const int DefaultPlateCount = 5;

    [Inject] private Plate.Factory plateFactory;

    [Header("References (drag & drop)")]
    [SerializeField] private Transform queueOrigin;
    [SerializeField] private Transform boardingAnchor;
    [SerializeField] private SplineComputer conveyorSpline;

    private readonly List<Plate> queuedPlates = new List<Plate>(16);
    private readonly List<Plate> activePlates = new List<Plate>(8);
    private readonly Dictionary<ShooterController, Plate> boundByShooter = new Dictionary<ShooterController, Plate>(8);
    private readonly List<UniTask> slideTasksBuffer = new List<UniTask>(16);

    private CancellationTokenSource lifeCts;
    private bool isBoardingBusy;

    public bool HasAvailablePlate => queuedPlates.Count > 0 && !isBoardingBusy;

    public Plate GetBoundPlate(ShooterController shooterController)
    {
        if (shooterController == null)
            return null;

        boundByShooter.TryGetValue(shooterController, out Plate plate);
        return plate;
    }

    public void Construct(LevelData levelData)
    {
        ClearAll();

        lifeCts?.Cancel();
        lifeCts?.Dispose();
        lifeCts = new CancellationTokenSource();

        SpawnQueue(ResolvePlateCount(levelData));
    }

    public async UniTask<bool> TryBoardShooterAsync(
        ShooterController shooterController,
        CancellationToken cancellationToken)
    {
        if (shooterController == null)
            return false;

        if (boundByShooter.ContainsKey(shooterController))
            return true;

        if (queuedPlates.Count == 0)
            return false;

        while (isBoardingBusy && !cancellationToken.IsCancellationRequested)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        if (queuedPlates.Count == 0)
            return false;

        isBoardingBusy = true;
        CancellationTokenSource linkedCts = null;
        CancellationToken linked = cancellationToken;

        if (lifeCts != null)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifeCts.Token);
            linked = linkedCts.Token;
        }

        try
        {
            Plate plate = queuedPlates[0];
            queuedPlates.RemoveAt(0);

            GetConveyorStartPose(plate, out Vector3 flatPos, out Quaternion flatRot);
            Vector3 seatWorld = plate.PredictSeatWorldPosition(flatPos, flatRot);

            Shooter shooter = shooterController.GetComponent<Shooter>();
            float travelSpeed = shooter != null ? shooter.PlateTravelSpeed : 20f;
            float travelArc = plate.RollTravelArcHeight();
            UniTask deployTask = plate.AnimateDeployFlatAsync(flatPos, flatRot, linked);
            UniTask slideTask = SlideQueueForwardAsync(linked);
            UniTask travelTask = shooterController.gameObject.FollowToAsync(
                seatWorld,
                travelSpeed,
                travelArc,
                null,
                linked);

            await UniTask.WhenAll(deployTask, slideTask, travelTask);

            if (linked.IsCancellationRequested || shooterController == null)
            {
                ReturnPlateToQueueEnd(plate);
                return false;
            }

            SplineExtensions.ClearExistingFollowers(shooterController.gameObject);

            plate.transform.position = flatPos;
            plate.transform.rotation = flatRot;
            plate.SeatPassenger(shooterController.transform);

            activePlates.Add(plate);
            boundByShooter[shooterController] = plate;
            return true;
        }
        finally
        {
            linkedCts?.Dispose();
            isBoardingBusy = false;
        }
    }

    public void ReleaseBoundPlate(ShooterController shooterController, bool returnToQueue)
    {
        if (shooterController == null)
            return;

        if (!boundByShooter.TryGetValue(shooterController, out Plate plate))
            return;

        boundByShooter.Remove(shooterController);
        activePlates.Remove(plate);

        plate.DetachFollower();
        plate.ClearPassenger(shooterController.transform);

        if (returnToQueue)
        {
            ReturnPlateToQueueEnd(plate);
            return;
        }

        plate.RecycleHide();
    }

    public void GetConveyorStartPose(Plate settingsSource, out Vector3 position, out Quaternion rotation)
    {
        float yOffset = settingsSource != null ? settingsSource.BoardingYOffset : 0f;
        Vector3 flatEuler = settingsSource != null ? settingsSource.FlatEuler : Vector3.zero;

        if (conveyorSpline != null)
        {
            SplineSample sample = conveyorSpline.Evaluate(0.0);
            Vector3 forward = sample.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(flatEuler);
            else
                rotation = Quaternion.Euler(flatEuler);

            if (boardingAnchor != null)
            {
                position = boardingAnchor.position;
                position.y += yOffset;
            }
            else
            {
                position = sample.position;
                position.y += yOffset;
            }

            return;
        }

        if (boardingAnchor != null)
        {
            position = boardingAnchor.position;
            position.y += yOffset;
            rotation = boardingAnchor.rotation * Quaternion.Euler(flatEuler);
            return;
        }

        Transform origin = queueOrigin != null ? queueOrigin : transform;
        position = origin.position;
        rotation = origin.rotation * Quaternion.Euler(flatEuler);
    }

    private async UniTask SlideQueueForwardAsync(CancellationToken cancellationToken)
    {
        int count = queuedPlates.Count;
        if (count == 0)
            return;

        slideTasksBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            Plate plate = queuedPlates[i];
            GetQueuePose(i, plate, out Vector3 pos, out Quaternion rot);
            slideTasksBuffer.Add(plate.AnimateSlideAsync(pos, rot, cancellationToken));
        }

        await UniTask.WhenAll(slideTasksBuffer);
    }

    private void ReturnPlateToQueueEnd(Plate plate)
    {
        if (plate == null)
            return;

        if (!queuedPlates.Contains(plate))
            queuedPlates.Add(plate);

        GetQueuePose(queuedPlates.Count - 1, plate, out Vector3 pos, out Quaternion rot);
        plate.FlyToQueue(pos, rot);
    }

    private void SpawnQueue(int count)
    {
        if (count <= 0 || plateFactory == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Plate plate = plateFactory.Create();
            plate.transform.SetParent(transform, false);
            GetQueuePose(i, plate, out Vector3 pos, out Quaternion rot);
            plate.PlaceInQueue(pos, rot);
            queuedPlates.Add(plate);
        }
    }

    private void GetQueuePose(int index, Plate settingsSource, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        float spacing = settingsSource != null ? settingsSource.QueueSpacing : 0.14f;
        Transform origin = queueOrigin != null ? queueOrigin : transform;
        Vector3 axis = ResolveQueueWorldAxis(origin, settingsSource);
        worldPosition = origin.position + axis * (spacing * index);
        worldRotation = ResolveVerticalRotation(axis, settingsSource);
    }

    private Vector3 ResolveQueueWorldAxis(Transform origin, Plate settingsSource)
    {
        bool useSpline = settingsSource == null || settingsSource.UseSplineQueueDirection;
        if (useSpline && conveyorSpline != null)
        {
            Vector3 hint = boardingAnchor != null ? boardingAnchor.position : origin.position;
            SplineSample sample = conveyorSpline.Evaluate(0.0);
            if (boardingAnchor != null)
                sample = conveyorSpline.Project(hint);

            Vector3 tangent = sample.forward;
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.0001f)
            {
                tangent.Normalize();
                bool opposite = settingsSource == null || settingsSource.QueueOppositeToTravel;
                return opposite ? -tangent : tangent;
            }
        }

        Vector3 localAxis = settingsSource != null ? settingsSource.QueueAxis : Vector3.right;
        if (localAxis.sqrMagnitude < 0.0001f)
            localAxis = Vector3.right;

        return origin.TransformDirection(localAxis.normalized);
    }

    private Quaternion ResolveVerticalRotation(Vector3 queueWorldAxis, Plate settingsSource)
    {
        Vector3 euler = settingsSource != null ? settingsSource.VerticalEuler : new Vector3(90f, 0f, 0f);
        Vector3 axis = queueWorldAxis;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f)
            return Quaternion.Euler(euler);

        return Quaternion.LookRotation(axis.normalized, Vector3.up) * Quaternion.Euler(euler);
    }

    private int ResolvePlateCount(LevelData levelData)
    {
        if (levelData == null)
            return DefaultPlateCount;

        int count = levelData.PlateCount;
        return count > 0 ? count : DefaultPlateCount;
    }

    private void ClearAll()
    {
        for (int i = 0; i < queuedPlates.Count; i++)
        {
            if (queuedPlates[i] != null)
                Destroy(queuedPlates[i].gameObject);
        }

        for (int i = 0; i < activePlates.Count; i++)
        {
            if (activePlates[i] != null)
                Destroy(activePlates[i].gameObject);
        }

        queuedPlates.Clear();
        activePlates.Clear();
        boundByShooter.Clear();
        isBoardingBusy = false;
    }

    private void OnDestroy()
    {
        lifeCts?.Cancel();
        lifeCts?.Dispose();
        lifeCts = null;
    }
}
