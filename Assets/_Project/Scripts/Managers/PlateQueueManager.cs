using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using LevelManagement;
using PixelShooter.Pooling;
using TMPro;
using UnityEngine;
using Zenject;

public class PlateQueueManager : MonoBehaviour
{
    private const int DefaultPlateCount = 5;

    [Inject] private Plate.Factory plateFactory;
    [Inject] private ObjectPool objectPool;

    private Transform platesRoot;

    [Header("References (drag & drop)")]
    [SerializeField] private Transform queueOrigin;
    [SerializeField] private Transform boardingAnchor;
    [SerializeField] private SplineComputer conveyorSpline;
    [SerializeField] private TMP_Text plateCountText;

    private readonly List<Plate> queuedPlates = new List<Plate>(16);
    private readonly List<Plate> activePlates = new List<Plate>(8);
    private readonly Dictionary<ShooterController, Plate> boundByShooter = new Dictionary<ShooterController, Plate>(8);
    private readonly List<UniTask> slideTasksBuffer = new List<UniTask>(16);

    private readonly char[] plateCountBuffer = new char[24];

    private CancellationTokenSource lifeCts;
    private bool isBoardingBusy;
    private int maxPlateCount;
    private int heldPlateCount;
    public bool HasAvailablePlate => queuedPlates.Count > 0 && !isBoardingBusy;
    public bool HasIdlePlate => queuedPlates.Count > heldPlateCount;

    public readonly struct PlateBoardResult
    {
        public readonly bool IsSeated;
        public readonly bool HoldSpent;

        public PlateBoardResult(bool isSeated, bool holdSpent)
        {
            IsSeated = isSeated;
            HoldSpent = holdSpent;
        }
    }


    private void Awake()
    {
        EnsurePlatesRoot();
    }

    private void EnsurePlatesRoot()
    {
        if (platesRoot != null)
            return;

        GameObject rootObject = new GameObject("Plates");
        platesRoot = rootObject.transform;
        platesRoot.SetParent(transform, false);
    }

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

    private bool CanTakePlate(bool spendHold)
    {
        if (spendHold)
            return queuedPlates.Count > 0;

        return queuedPlates.Count > heldPlateCount;
    }

    public bool TryHoldPlate()
    {
        if (queuedPlates.Count <= heldPlateCount)
            return false;

        heldPlateCount++;
        return true;
    }

    public void ReleaseHeldPlate()
    {
        if (heldPlateCount <= 0)
            return;

        heldPlateCount--;
    }

    public async UniTask<PlateBoardResult> TryBoardShooterAsync(
        ShooterController shooterController,
        CancellationToken cancellationToken,
        bool spendHold)
    {
        if (shooterController == null)
            return default;

        if (boundByShooter.ContainsKey(shooterController))
            return new PlateBoardResult(true, false);

        if (!CanTakePlate(spendHold))
            return default;

        while (isBoardingBusy && !cancellationToken.IsCancellationRequested)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        if (!CanTakePlate(spendHold))
            return default;

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
            RefreshPlateCountText();

            if (spendHold)
                ReleaseHeldPlate();

            PlateBoardResult taken = new PlateBoardResult(false, spendHold);

            GetConveyorStartPose(plate, out Vector3 flatPos, out Quaternion flatRot);
            Vector3 seatWorld = plate.PredictSeatWorldPosition(flatPos, flatRot);

            Shooter shooter = shooterController.Owner;
            float travelSpeed = shooter != null ? shooter.PlateTravelSpeed : 20f;
            float travelArc = shooter != null ? shooter.RandomPlateArcHeight() : 7f;
            float travelDistanceMultiplier = shooter != null ? shooter.PlateDistanceMultiplier : 0.08f;
            UniTask deployTask = plate.DeployFlatAsync(flatPos, flatRot, linked);
            UniTask slideTask = SlideQueueForwardAsync(linked);
            UniTask travelTask = shooterController.gameObject.FollowToAsync(
                objectPool,
                seatWorld,
                travelSpeed,
                travelArc,
                null,
                linked,
                travelDistanceMultiplier);

            await UniTask.WhenAll(deployTask, slideTask, travelTask);

            if (linked.IsCancellationRequested || shooterController == null)
            {
                ReturnPlateToQueueEnd(plate);
                return taken;
            }

            SplineExtensions.ClearExistingFollowers(shooterController.gameObject, objectPool);

            plate.transform.position = flatPos;
            plate.transform.rotation = flatRot;
            plate.SeatPassenger(shooterController.transform);

            activePlates.Add(plate);
            boundByShooter[shooterController] = plate;
            return new PlateBoardResult(true, spendHold);
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

        plate.HideAtHome();
    }

    public void GetConveyorStartPose(Plate settingsSource, out Vector3 position, out Quaternion rotation)
    {
        float yOffset = settingsSource != null ? settingsSource.BoardingYOffset : 0f;
        Vector3 euler = settingsSource != null ? settingsSource.BoardingEuler : new Vector3(0f, 90f, 180f);
        rotation = Quaternion.Euler(euler);

        if (boardingAnchor != null)
        {
            position = boardingAnchor.position;
            position.y += yOffset;
            return;
        }

        Transform origin = queueOrigin != null ? queueOrigin : transform;
        position = origin.position;
        position.y += yOffset;
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
            slideTasksBuffer.Add(plate.SlideToQueueAsync(pos, rot, cancellationToken));
        }

        await UniTask.WhenAll(slideTasksBuffer);
    }

    private void ReturnPlateToQueueEnd(Plate plate)
    {
        if (plate == null)
            return;

        if (!queuedPlates.Contains(plate))
        {
            queuedPlates.Add(plate);
            RefreshPlateCountText();
        }

        GetQueuePose(queuedPlates.Count - 1, plate, out Vector3 pos, out Quaternion rot);
        plate.FlyToQueue(pos, rot);
    }

    private void SpawnQueue(int count)
    {
        maxPlateCount = count > 0 ? count : 0;
        if (count <= 0 || plateFactory == null)
        {
            RefreshPlateCountText();
            return;
        }

        EnsurePlatesRoot();

        for (int i = 0; i < count; i++)
        {
            Plate plate = plateFactory.Create();
            plate.SetHome(platesRoot);
            GetQueuePose(i, plate, out Vector3 pos, out Quaternion rot);
            plate.PlaceInQueue(pos, rot);
            queuedPlates.Add(plate);
        }

        RefreshPlateCountText();
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
            ReleasePlate(queuedPlates[i]);

        for (int i = 0; i < activePlates.Count; i++)
            ReleasePlate(activePlates[i]);

        queuedPlates.Clear();
        activePlates.Clear();
        boundByShooter.Clear();

        isBoardingBusy = false;
        heldPlateCount = 0;
        maxPlateCount = 0;

        RefreshPlateCountText();
    }

    private void RefreshPlateCountText()
    {
        if (plateCountText == null)
            return;

        int length = WritePlateCount(queuedPlates.Count, maxPlateCount);
        plateCountText.SetCharArray(plateCountBuffer, 0, length);
    }

    private int WritePlateCount(int waiting, int max)
    {
        int length = WriteNonNegative(0, waiting);
        plateCountBuffer[length] = '/';
        length++;

        return WriteNonNegative(length, max);
    }

    private int WriteNonNegative(int start, int value)
    {
        if (value < 0)
            value = 0;

        int digits = 1;
        int probe = value;

        while (probe >= 10)
        {
            probe /= 10;
            digits++;
        }

        int index = start + digits;
        for (int i = digits - 1; i >= 0; i--)
        {
            index--;
            plateCountBuffer[index] = (char)('0' + (value % 10));
            value /= 10;
        }

        return start + digits;
    }

    private void ReleasePlate(Plate plate)
    {
        if (plate == null)
            return;

        SplineExtensions.ClearExistingFollowers(plate.gameObject, objectPool);
        Destroy(plate.gameObject);
    }

    private void OnDestroy()
    {
        lifeCts?.Cancel();
        lifeCts?.Dispose();
        lifeCts = null;
    }
}
