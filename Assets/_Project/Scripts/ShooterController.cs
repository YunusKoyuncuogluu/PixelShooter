using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using PixelShooter.Pooling;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class ShooterController : MonoBehaviour
{
    [Inject] private ImageManager imageManager;
    [Inject] private ObjectPool objectPool;
    [Inject] private GameAudio audioPool;
    [InjectOptional] private ConveyorManager conveyorManager;
    [InjectOptional] private PlateQueueManager plateQueueManager;
    [Inject] private SlotManager slotManager;
    [InjectOptional] private PixelShooter.Game.IFastForwardGate fastForwardGate;

    [FormerlySerializedAs("minOrbitDuration")]
    [SerializeField] private float minLapDuration = 1.8f;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Transform muzzlePoint;

    [Header("Vanish Animation")]
    [SerializeField] private float vanishPopScale = 1.35f;
    [SerializeField] private float vanishPopDuration = 0.28f;
    [SerializeField] private float vanishShrinkDuration = 0.8f;
    [SerializeField] private float vanishSpinDegrees = 360f;

    private Color shooterColor;
    private short shooterPaletteId = -1;
    private int maxCapacity;
    private int currentFill;
    private int displayedRemaining = -1;
    private int inFlightShots;
    private bool isRunRoutine;
    private bool runCompleted;
    private bool isOnConveyor;
    private bool isAwaitingDeparture;
    private bool isVanishing;
    private float conveyorEnterTime;
    private SplineFollower cachedFollower;
    private Transform seatAnchor;
    private int firedSide = -1;
    private int firedLane = int.MinValue;
    private Vector3 lastAimPosition;
    private bool hasLastAimPosition;
    private int ammoAtLapStart;
    private CancellationTokenSource travelCts;
    private Shooter shooter;

    public int MaxCapacity => maxCapacity > 0 ? maxCapacity : 0;
    public SplineFollower CachedFollower => cachedFollower;
    public Shooter Owner => shooter != null ? shooter : shooter = GetComponent<Shooter>();
    public short PaletteId => shooterPaletteId;

    public int RemainingShots => Mathf.Max(0, MaxCapacity - currentFill - inFlightShots);
    public bool IsTraveling => isRunRoutine || isOnConveyor || isAwaitingDeparture;

    private void Awake()
    {
        shooter = GetComponent<Shooter>();
    }

    public void SetAmmoCapacity(int shots)
    {
        maxCapacity = shots > 0 ? shots : 0;
        currentFill = 0;
        inFlightShots = 0;

        RefreshAmmoCount();
    }

    public void BindPalette(GeneralColor generalColor)
    {
        if (imageManager == null || shooterPaletteId > 0)
            return;

        short paletteId = imageManager.ResolvePaletteId(generalColor);
        if (paletteId <= 0)
            return;

        shooterPaletteId = paletteId;
        shooterColor = imageManager.ResolveShooterColor(generalColor);
    }

    public void ShowAmmoCount(bool visible)
    {
        if (countText == null || countText.gameObject.activeSelf == visible)
            return;

        countText.gameObject.SetActive(visible);
    }

    private Vector3 GetMuzzlePosition()
    {
        return muzzlePoint != null ? muzzlePoint.position : transform.position;
    }

    public bool TryDepart(Slot slot, GeneralColor shooterGeneralColor)
    {
        if (plateQueueManager == null || !plateQueueManager.HasAvailablePlate)
            return false;

        return BeginDeparture(slot, shooterGeneralColor);
    }

    public bool StartRun(Slot slot, GeneralColor shooterGeneralColor)
    {
        return BeginDeparture(slot, shooterGeneralColor);
    }

    private bool BeginDeparture(Slot slot, GeneralColor shooterGeneralColor)
    {
        if (!CanDepart() || !ApplyShooterColor(shooterGeneralColor))
            return false;

        ResetAmmoIfEmpty();
        isAwaitingDeparture = true;
        DepartAsync(slot).Forget();

        return true;
    }

    private bool CanDepart()
    {
        return !IsConveyorStopped()
            && !isRunRoutine
            && !isOnConveyor
            && !isAwaitingDeparture
            && imageManager != null
            && conveyorManager != null;
    }

    private bool IsConveyorStopped() => conveyorManager != null && conveyorManager.IsStopped;

    private bool ApplyShooterColor(GeneralColor shooterGeneralColor)
    {
        shooterColor = imageManager.ResolveShooterColor(shooterGeneralColor);
        shooterPaletteId = imageManager.ResolvePaletteId(shooterGeneralColor);

        return shooterPaletteId >= 0;
    }

    private void ResetAmmoIfEmpty()
    {
        if (currentFill > 0)
            return;

        inFlightShots = 0;
        RefreshAmmoCount();
    }

    private async UniTaskVoid DepartAsync(Slot slot)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        bool sent = false;

        try
        {
            if (plateQueueManager != null)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (IsConveyorStopped())
                        return;

                    if (!plateQueueManager.HasAvailablePlate)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        continue;
                    }

                    if (this == null || IsConveyorStopped())
                        return;

                    bool boarded = await plateQueueManager.TryBoardShooterAsync(this, cancellationToken);

                    if (boarded)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            if (this == null || conveyorManager == null || conveyorManager.IsStopped || cancellationToken.IsCancellationRequested)
                return;

            if (shooter != null)
                shooter.ClearBusy();

            sent = conveyorManager.SendShooterToConveyor(this, slot);
        }
        finally
        {
            isAwaitingDeparture = false;

            if (!sent && shooter != null)
                shooter.RestoreSlotWait();
        }
    }

    public SplineFollower GetTravelFollower()
    {
        if (cachedFollower == null)
            cachedFollower = GetComponent<SplineFollower>();

        if (cachedFollower == null)
            cachedFollower = gameObject.AddComponent<SplineFollower>();

        return cachedFollower;
    }

    public void SetTravelFollower(SplineFollower follower) => cachedFollower = follower;

    public void ClearTravelFollower() => cachedFollower = null;

    public void StopTravel()
    {
        runCompleted = true;

        if (cachedFollower != null)
            cachedFollower.follow = false;

        travelCts?.Cancel();
    }

    public void EnterConveyor() => isOnConveyor = true;

    public async UniTaskVoid BeginTravel(ConveyorManager owner)
    {
        if (owner == null)
            return;

        travelCts?.Cancel();
        travelCts = new CancellationTokenSource();
        CancellationToken cancellationToken = travelCts.Token;

        runCompleted = false;
        isRunRoutine = true;
        isOnConveyor = true;
        ammoAtLapStart = RemainingShots;
        conveyorEnterTime = Time.time;
        LockToSeat();
        ShowAmmoCount(true);
        firedSide = -1;
        firedLane = int.MinValue;
        FaceGridCenter(true);
        lastAimPosition = transform.position;
        hasLastAimPosition = true;

        SplineFollower follower = cachedFollower;

        if (follower == null)
            follower = GetTravelFollower();

        follower.onEndReached -= OnLapReached;
        follower.onEndReached += OnLapReached;
        follower.follow = true;

        bool finishedCleanly = false;

        try
        {
            await TravelLoop(cancellationToken);
            finishedCleanly = !cancellationToken.IsCancellationRequested;
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            isRunRoutine = false;
            isOnConveyor = false;

            bool keepMoving = this != null
                && !cancellationToken.IsCancellationRequested
                && (MaxCapacity <= 0 || currentFill >= MaxCapacity);

            if (!keepMoving && follower != null)
                follower.follow = false;
        }

        if (!finishedCleanly || this == null || gameObject == null)
            return;

        bool outOfAmmo = MaxCapacity <= 0 || currentFill >= MaxCapacity;

        owner.HandleShooterFinished(this, outOfAmmo);
    }

    private async UniTask TravelLoop(CancellationToken cancellationToken)
    {
        int capacity = MaxCapacity;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (currentFill >= capacity && inFlightShots <= 0)
                break;

            if (runCompleted && inFlightShots <= 0)
                break;

            await UniTask.Yield(PlayerLoopTiming.LastPreLateUpdate, cancellationToken);
            FaceGridCenter(false);
            TryFire(capacity, cancellationToken);
        }

        while (inFlightShots > 0 && !cancellationToken.IsCancellationRequested)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
    }

    private void LockToSeat()
    {
        if (plateQueueManager == null)
            return;

        Plate plate = plateQueueManager.GetBoundPlate(this);
        if (plate == null)
            return;

        Transform seat = plate.SeatPoint;
        if (transform.parent != seat)
            transform.SetParent(seat, true);

        seatAnchor = seat;
        transform.localPosition = Vector3.zero;
    }

    private void TryFire(int capacity, CancellationToken cancellationToken)
    {
        if (runCompleted || IsConveyorStopped())
            return;

        Vector3 current = transform.position;
        if (!hasLastAimPosition)
        {
            hasLastAimPosition = true;
            lastAimPosition = current;
            TryFireAt(current, capacity, cancellationToken);

            return;
        }

        Vector3 from = lastAimPosition;
        lastAimPosition = current;
        float dx = current.x - from.x;
        float dz = current.z - from.z;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        float step = imageManager != null ? imageManager.ClaimSampleDistance : 0.12f;

        if (step < 0.05f)
            step = 0.05f;

        const int maxSamples = 16;

        if (distance > step * maxSamples)
        {
            TryFireAt(current, capacity, cancellationToken);
            return;
        }

        int samples = Mathf.CeilToInt(distance / step);

        if (samples < 1)
            samples = 1;

        float inverse = 1f / samples;

        for (int i = 1; i <= samples; i++)
        {
            if (runCompleted || IsConveyorStopped())
                return;

            if (currentFill + inFlightShots >= capacity)
                return;

            float t = i * inverse;
            TryFireAt(new Vector3(from.x + dx * t, current.y, from.z + dz * t), capacity, cancellationToken);
        }
    }

    private void TryFireAt(Vector3 aimPosition, int capacity, CancellationToken cancellationToken)
    {
        if (currentFill + inFlightShots >= capacity)
            return;

        if (!imageManager.TryClaimFrontPixel(
                aimPosition,
                shooterPaletteId,
                firedSide,
                firedLane,
                out int side,
                out int lane,
                out Vector2Int gridPosition,
                out Vector3 pixelWorldPos))
            return;

        GameObject projectileObject = null;
        Projectile projectile = null;

        if (objectPool != null)
        {
            projectileObject = objectPool.SpawnFromPool(PoolTags.Bullet, Vector3.zero, Quaternion.identity);

            if (projectileObject != null && objectPool.TryGetPoolBehaviour(projectileObject, out IPoolObject behaviour))
                projectile = behaviour as Projectile;
        }

        if (projectile == null)
        {
            imageManager.ReleaseReservation(gridPosition);
            return;
        }

        firedSide = side;
        firedLane = lane;

        if (audioPool != null)
            audioPool.PlayBulletFire();

        Vector3 muzzlePosition = GetMuzzlePosition();
        inFlightShots++;
        RefreshAmmoCount();

        if (shooter != null)
            shooter.PlayFireRecoil();

        ShootPixel(projectile, gridPosition, muzzlePosition, pixelWorldPos, cancellationToken).Forget();
    }

    private void FaceGridCenter(bool snap)
    {
        Vector3 position = seatAnchor != null ? seatAnchor.position : transform.position;

        if (imageManager == null)
            return;

        Vector3 toCenter = imageManager.GridCenter - position;
        toCenter.y = 0f;

        if (toCenter.sqrMagnitude < 0.0001f)
            return;

        if (!TryGetMoveDirection(out Vector3 tangent))
        {
            if (snap)
                transform.rotation = Quaternion.LookRotation(GetCardinalDirection(toCenter), Vector3.up);

            return;
        }

        Vector3 sideA = new Vector3(-tangent.z, 0f, tangent.x);
        Vector3 sideB = new Vector3(tangent.z, 0f, -tangent.x);
        Vector3 inward = Vector3.Dot(sideA, toCenter) >= Vector3.Dot(sideB, toCenter) ? sideA : sideB;
        float length = Mathf.Sqrt(inward.x * inward.x + inward.z * inward.z);

        if (length < 0.0001f)
            return;

        inward.x /= length;
        inward.z /= length;

        Vector3 cardinal = GetCardinalDirection(inward);
        float absX = Mathf.Abs(inward.x);
        float absZ = Mathf.Abs(inward.z);
        float align = absX > absZ ? absX : absZ;
        float straight = snap ? 1f : Mathf.InverseLerp(0.78f, 0.96f, align);
        Vector3 facing = Vector3.Slerp(inward, cardinal, straight);

        if (facing.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
    }

    private bool TryGetMoveDirection(out Vector3 tangent)
    {
        tangent = Vector3.zero;

        if (cachedFollower == null)
            return false;

        Vector3 forward = cachedFollower.result.forward;
        forward.y = 0f;
        float magnitude = forward.x * forward.x + forward.z * forward.z;

        if (magnitude < 0.0001f)
            return false;

        float inverse = 1f / Mathf.Sqrt(magnitude);
        tangent = new Vector3(forward.x * inverse, 0f, forward.z * inverse);

        return true;
    }

    private Vector3 GetCardinalDirection(Vector3 direction)
    {
        float absX = Mathf.Abs(direction.x);
        float absZ = Mathf.Abs(direction.z);

        if (absX > absZ)
            return direction.x >= 0f ? Vector3.right : Vector3.left;

        return direction.z >= 0f ? Vector3.forward : Vector3.back;
    }

    private async UniTask ShootPixel(
        Projectile projectile,
        Vector2Int gridPosition,
        Vector3 muzzlePosition,
        Vector3 pixelWorldPos,
        CancellationToken cancellationToken)
    {
        bool activated = false;

        try
        {
            projectile.SetColor(shooterColor);
            await projectile.FlyToTarget(muzzlePosition, pixelWorldPos, cancellationToken);
            imageManager.ActivatePixel(gridPosition);
            activated = true;
            currentFill++;
            if (currentFill > MaxCapacity)
                currentFill = MaxCapacity;
        }
        catch (System.OperationCanceledException)
        {
            if (!activated)
                imageManager.ReleaseReservation(gridPosition);
        }
        finally
        {
            if (inFlightShots > 0)
                inFlightShots--;

            RefreshAmmoCount();

            if (objectPool != null && projectile != null)
                objectPool.DestroyToPool(PoolTags.Bullet, projectile.gameObject);
        }
    }

    public void OnLapReached(double unusedPercent)
    {
        if (isVanishing)
            return;

        if (Time.time - conveyorEnterTime < minLapDuration)
            return;

        if (fastForwardGate != null
            && fastForwardGate.IsActive
            && RemainingShots > 0
            && imageManager != null
            && imageManager.HasClaimablePixel(shooterPaletteId))
        {
            bool firedThisLap = RemainingShots < ammoAtLapStart;
            ammoAtLapStart = RemainingShots;

            if (firedThisLap && !HasWaitingOtherColor())
            {
                firedSide = -1;
                firedLane = int.MinValue;
                return;
            }
        }

        runCompleted = true;
        PinToSplineEnd();
    }

    private bool HasWaitingOtherColor()
    {
        if (slotManager == null || imageManager == null)
            return false;

        int occupants = slotManager.OccupantCount;

        for (int i = 0; i < occupants; i++)
        {
            if (NeedsOtherColor(slotManager.GetOccupant(i)))
                return true;
        }

        int queued = slotManager.QueuedCount;

        for (int i = 0; i < queued; i++)
        {
            if (NeedsOtherColor(slotManager.GetQueued(i)))
                return true;
        }

        return false;
    }

    private bool NeedsOtherColor(Shooter other)
    {
        if (other == null || other.gameObject == gameObject)
            return false;

        ShooterController controller = other.Controller;

        if (controller == null || controller.RemainingShots <= 0)
            return false;

        short paletteId = controller.PaletteId;

        if (paletteId <= 0)
            paletteId = imageManager.ResolvePaletteId(other.Color);

        if (paletteId <= 0 || paletteId == shooterPaletteId)
            return false;

        return imageManager.HasClaimablePixel(paletteId);
    }

    private void PinToSplineEnd()
    {
        if (cachedFollower == null || cachedFollower.spline == null)
            return;

        cachedFollower.follow = false;
        cachedFollower.wrapMode = SplineFollower.Wrap.Default;

        SplineComputer spline = cachedFollower.spline;
        int pointCount = spline.pointCount;
        int endIndex = pointCount - 1;

        if (pointCount > 2)
        {
            Vector3 first = spline.GetPoint(0).position;
            Vector3 closing = spline.GetPoint(endIndex).position;
            if ((closing - first).sqrMagnitude < 0.04f)
                endIndex = pointCount - 2;
        }

        SplineSample endSample = default;
        spline.Evaluate(endIndex, ref endSample);
        cachedFollower.SetPercent(spline.GetPointPercent(endIndex));

        Vector2 offset = cachedFollower.motion.offset;
        Vector3 endPosition = endSample.position
            + endSample.right * offset.x
            + endSample.up * offset.y;
        cachedFollower.transform.position = endPosition;

        if (seatAnchor != null)
        {
            transform.localPosition = Vector3.zero;
            return;
        }

        if (cachedFollower.transform != transform)
            transform.position = endPosition;
    }

    public void ReturnToSlot(Slot slot)
    {
        isOnConveyor = false;
        seatAnchor = null;

        if (shooter != null)
            shooter.ParkInSlot(slot);
    }

    public void LeaveConveyor() => isOnConveyor = false;

    public void DetachFromPlate(Vector3 worldPosition, Quaternion worldRotation)
    {
        seatAnchor = null;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public void DestroyEmpty()
    {
        ShrinkAndDestroy().Forget();
    }

    private async UniTaskVoid ShrinkAndDestroy()
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        Transform body = transform;
        isVanishing = true;
        isOnConveyor = true;

        if (cachedFollower != null)
        {
            cachedFollower.onEndReached -= OnLapReached;
            cachedFollower.follow = true;
        }

        float popDuration = vanishPopDuration > 0.01f ? vanishPopDuration : 0.01f;
        float shrinkDuration = vanishShrinkDuration > 0.01f ? vanishShrinkDuration : 0.01f;
        float totalDuration = popDuration + shrinkDuration;

        Vector3 startScale = body.localScale;
        Quaternion spinOrigin = body.localRotation;
        Vector3 peakScale = startScale * vanishPopScale;
        float startTime = Time.time;

        while (!cancellationToken.IsCancellationRequested)
        {
            float elapsed = Time.time - startTime;
            if (elapsed >= totalDuration)
                break;

            ApplyVanishPose(body, elapsed, popDuration, shrinkDuration, startScale, peakScale, spinOrigin);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (this == null)
            return;

        if (!cancellationToken.IsCancellationRequested)
            ApplyVanishPose(body, totalDuration, popDuration, shrinkDuration, startScale, peakScale, spinOrigin);

        if (cachedFollower != null)
        {
            cachedFollower.follow = false;
            cachedFollower.spline = null;
            cachedFollower = null;
        }

        isOnConveyor = false;

        if (slotManager != null && shooter != null)
            slotManager.RemoveShooter(shooter);

        Destroy(gameObject);
    }

    private void ApplyVanishPose(
        Transform body,
        float elapsed,
        float popDuration,
        float shrinkDuration,
        Vector3 startScale,
        Vector3 peakScale,
        Quaternion spinOrigin)
    {
        if (elapsed < popDuration)
        {
            float progress = elapsed / popDuration;
            body.localScale = Vector3.LerpUnclamped(startScale, peakScale, progress);
            body.localRotation = spinOrigin;
            return;
        }

        float linear = (elapsed - popDuration) / shrinkDuration;
        if (linear > 1f)
            linear = 1f;

        body.localScale = Vector3.LerpUnclamped(peakScale, Vector3.zero, linear);
        body.localRotation = spinOrigin * Quaternion.Euler(0f, vanishSpinDegrees * linear, 0f);
    }

    private void RefreshAmmoCount()
    {
        if (countText == null)
            return;

        int remaining = RemainingShots;
        if (remaining == displayedRemaining)
            return;

        displayedRemaining = remaining;
        countText.SetText("{0:0}", (float)remaining);
    }

    private void OnDestroy()
    {
        travelCts?.Cancel();

        if (cachedFollower != null)
            cachedFollower.onEndReached -= OnLapReached;
    }
}
