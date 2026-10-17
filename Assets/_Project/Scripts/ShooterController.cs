using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class ShooterController : MonoBehaviour
{
    [Inject] private ImageManager imageManager;
    [Inject] private ProjectilePool projectilePool;
    [Inject] private AudioPool audioPool;
    [InjectOptional] private ConveyorManager conveyorManager;
    [InjectOptional] private PlateQueueManager plateQueueManager;
    [Inject] private SlotManager slotManager;
    [InjectOptional] private PixelShooter.Game.IFastForwardGate fastForwardGate;

    [FormerlySerializedAs("minOrbitDuration")]
    [SerializeField] private float minLapDuration = 1.8f;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Transform muzzlePoint;

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
    private float conveyorEnterTime;
    private SplineFollower cachedFollower;
    private Transform seatAnchor;
    private int firedSide = -1;
    private int firedLane = int.MinValue;
    private Quaternion countTextLocalRotation = Quaternion.identity;
    private bool countTextLocalCached;
    private int ammoAtLapStart;
    private CancellationTokenSource travelCts;
    private GeneralColor generalColor;
    private Shooter shooter;

    public int MaxCapacity => maxCapacity > 0 ? maxCapacity : 0;
    public SplineFollower CachedFollower => cachedFollower;
    public short PaletteId => shooterPaletteId;

    public int RemainingShots
    {
        get
        {
            int remaining = MaxCapacity - currentFill - inFlightShots;
            if (remaining < 0)
                return 0;

            return remaining;
        }
    }

    private void Awake()
    {
        shooter = GetComponent<Shooter>();
        if (muzzlePoint == null)
            muzzlePoint = FindMuzzlePoint();
        if (countText == null)
            countText = FindCountText();
    }

    public void SetShotCapacity(int shots)
    {
        maxCapacity = shots > 0 ? shots : 0;
        currentFill = 0;
        inFlightShots = 0;
        RefreshCountText();
    }

    public void SetCountTextVisible(bool visible)
    {
        if (countText == null)
            return;

        if (countText.gameObject.activeSelf != visible)
            countText.gameObject.SetActive(visible);
    }

    public void FaceCountTextUp()
    {
        if (countText == null)
            return;

        if (!countTextLocalCached)
        {
            countTextLocalRotation = countText.transform.localRotation;
            countTextLocalCached = true;
        }

        countText.gameObject.SetActive(true);
        countText.transform.rotation = countTextLocalRotation;
    }

    public void RestoreCountText()
    {
        if (countText == null)
            return;

        countText.gameObject.SetActive(true);
        if (!countTextLocalCached)
            return;

        countText.transform.localRotation = countTextLocalRotation;
    }

    private Transform FindMuzzlePoint()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "MuzzlePoint")
                return children[i];
        }

        return null;
    }

    private TMP_Text FindCountText()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != "CountText")
                continue;

            return children[i].GetComponent<TMP_Text>();
        }

        return null;
    }

    private Vector3 MuzzlePosition()
    {
        return muzzlePoint != null ? muzzlePoint.position : transform.position;
    }

    public bool TryBeginDeparture(Slot slot, GeneralColor shooterGeneralColor)
    {
        if (!CanDepart() || plateQueueManager == null || !plateQueueManager.HasAvailablePlate)
            return false;

        if (!PrepareColor(shooterGeneralColor))
            return false;

        BeginDeparture(slot);
        return true;
    }

    public void TryStartRun(Slot slot, GeneralColor shooterGeneralColor)
    {
        if (!CanDepart() || !PrepareColor(shooterGeneralColor))
            return;

        BeginDeparture(slot);
    }

    private bool CanDepart()
    {
        return !isRunRoutine && !isOnConveyor && !isAwaitingDeparture
            && imageManager != null && conveyorManager != null;
    }

    private void BeginDeparture(Slot slot)
    {
        ResetFillIfEmpty();
        isAwaitingDeparture = true;
        StartDepartureAsync(slot).Forget();
    }

    private bool PrepareColor(GeneralColor shooterGeneralColor)
    {
        generalColor = shooterGeneralColor;
        shooterColor = imageManager.ResolveShooterColor(shooterGeneralColor);
        shooterPaletteId = imageManager.ResolvePaletteId(shooterGeneralColor);
        return shooterPaletteId >= 0;
    }

    private void ResetFillIfEmpty()
    {
        if (currentFill > 0)
            return;

        currentFill = 0;
        inFlightShots = 0;
        RefreshCountText();
    }

    private async UniTaskVoid StartDepartureAsync(Slot slot)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();

        try
        {
            if (plateQueueManager != null)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    while (!plateQueueManager.HasAvailablePlate && !cancellationToken.IsCancellationRequested)
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                    if (cancellationToken.IsCancellationRequested || this == null)
                        return;

                    bool boarded = await plateQueueManager.TryBoardShooterAsync(this, cancellationToken);
                    if (boarded)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            if (this == null || conveyorManager == null || cancellationToken.IsCancellationRequested)
                return;

            if (shooter != null)
                shooter.ClearBusy();

            conveyorManager.SendShooterToConveyor(this, slot);
        }
        finally
        {
            isAwaitingDeparture = false;
        }
    }

    public SplineFollower EnsureFollower()
    {
        if (cachedFollower == null)
            cachedFollower = GetComponent<SplineFollower>();
        if (cachedFollower == null)
            cachedFollower = gameObject.AddComponent<SplineFollower>();
        return cachedFollower;
    }

    public void SetTravelFollower(SplineFollower follower)
    {
        cachedFollower = follower;
    }

    public void ClearTravelFollower()
    {
        cachedFollower = null;
    }

    public void ReleaseOriginSlot()
    {
        isOnConveyor = true;
    }

    public async UniTaskVoid BeginConveyorTravel(ConveyorManager owner)
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
        DetachFromSeatSpin();
        RestoreCountText();
        firedSide = -1;
        firedLane = int.MinValue;
        FaceCenter(true);

        SplineFollower follower = cachedFollower;
        if (follower == null)
            follower = EnsureFollower();

        follower.onEndReached -= HandleLapReached;
        follower.onEndReached += HandleLapReached;
        follower.follow = true;

        bool finishedCleanly = false;
        try
        {
            await TravelRunRoutine(cancellationToken);
            finishedCleanly = !cancellationToken.IsCancellationRequested;
        }
        catch (System.OperationCanceledException)
        {
            finishedCleanly = false;
        }
        finally
        {
            isRunRoutine = false;
            isOnConveyor = false;
            if (follower != null)
                follower.follow = false;
        }

        if (!finishedCleanly || this == null || gameObject == null)
            return;

        bool outOfAmmo = MaxCapacity <= 0 || currentFill >= MaxCapacity;
        owner.HandleShooterFinished(this, outOfAmmo);
    }

    private async UniTask TravelRunRoutine(CancellationToken cancellationToken)
    {
        int capacity = MaxCapacity;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (currentFill >= capacity && inFlightShots <= 0)
                break;

            if (runCompleted && inFlightShots <= 0)
                break;

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            FaceCenter(false);
            TryFire(capacity, cancellationToken);
        }

        while (inFlightShots > 0 && !cancellationToken.IsCancellationRequested)
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
    }

    private void DetachFromSeatSpin()
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        seatAnchor = parent;
        transform.SetParent(null, true);
    }

    private void TryFire(int capacity, CancellationToken cancellationToken)
    {
        if (runCompleted || currentFill + inFlightShots >= capacity)
            return;

        if (!imageManager.TryClaimFrontPixel(
                transform.position,
                shooterPaletteId,
                firedSide,
                firedLane,
                out int side,
                out int lane,
                out Vector2Int gridPosition,
                out Vector3 pixelWorldPos))
            return;

        Projectile projectile = projectilePool != null ? projectilePool.Rent() : null;
        if (projectile == null)
        {
            imageManager.ReleaseReservation(gridPosition);
            return;
        }

        firedSide = side;
        firedLane = lane;

        if (audioPool != null)
            audioPool.PlayBulletFire();

        inFlightShots++;
        RefreshCountText();
        ShootPixelAsync(projectile, gridPosition, MuzzlePosition(), pixelWorldPos, cancellationToken).Forget();
    }

    private void FaceCenter(bool snap)
    {
        Vector3 position = seatAnchor != null ? seatAnchor.position : transform.position;
        if (seatAnchor != null)
            transform.position = position;

        if (imageManager == null)
            return;

        Vector3 toCenter = imageManager.GridCenter - position;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.0001f)
            return;

        if (!TryGetMoveDirection(out Vector3 tangent))
        {
            if (snap)
                transform.rotation = Quaternion.LookRotation(NearestCardinal(toCenter), Vector3.up);
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

        Vector3 cardinal = NearestCardinal(inward);
        float absX = inward.x < 0f ? -inward.x : inward.x;
        float absZ = inward.z < 0f ? -inward.z : inward.z;
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

    private Vector3 NearestCardinal(Vector3 direction)
    {
        float absX = direction.x < 0f ? -direction.x : direction.x;
        float absZ = direction.z < 0f ? -direction.z : direction.z;
        if (absX > absZ)
            return direction.x >= 0f ? Vector3.right : Vector3.left;

        return direction.z >= 0f ? Vector3.forward : Vector3.back;
    }

    private async UniTask ShootPixelAsync(
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
            await projectile.FlyAsync(muzzlePosition, pixelWorldPos, cancellationToken);
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

            RefreshCountText();

            if (projectilePool != null)
                projectilePool.Return(projectile);
        }
    }

    public void HandleLapReached(double unusedPercent)
    {
        if (Time.time - conveyorEnterTime < minLapDuration)
            return;

        if (fastForwardGate != null
            && fastForwardGate.IsActive
            && RemainingShots > 0
            && imageManager != null
            && imageManager.HasRemainingPixelsOfPaletteId(shooterPaletteId))
        {
            bool firedThisLap = RemainingShots < ammoAtLapStart;
            ammoAtLapStart = RemainingShots;
            if (firedThisLap && !OtherColorNeedsBelt())
                return;
        }

        runCompleted = true;
        PinTravelToSplineEnd();
    }

    private bool OtherColorNeedsBelt()
    {
        if (slotManager == null || imageManager == null)
            return false;

        int occupants = slotManager.OccupantCount;
        for (int i = 0; i < occupants; i++)
        {
            if (NeedsDifferentColor(slotManager.GetOccupant(i)))
                return true;
        }

        int queued = slotManager.QueuedCount;
        for (int i = 0; i < queued; i++)
        {
            if (NeedsDifferentColor(slotManager.GetQueued(i)))
                return true;
        }

        return false;
    }

    private bool NeedsDifferentColor(Shooter other)
    {
        if (other == null || other.gameObject == gameObject)
            return false;

        ShooterController controller = other.GetComponent<ShooterController>();
        if (controller == null || controller.RemainingShots <= 0)
            return false;

        short paletteId = controller.PaletteId;
        if (paletteId <= 0)
            paletteId = imageManager.ResolvePaletteId(other.Color);
        if (paletteId <= 0 || paletteId == shooterPaletteId)
            return false;

        return imageManager.HasRemainingPixelsOfPaletteId(paletteId);
    }

    private void PinTravelToSplineEnd()
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
            transform.position = seatAnchor.position;
        else if (cachedFollower.transform != transform)
            transform.position = endPosition;
    }

    public void OnReturnedToSlot(Slot slot)
    {
        isOnConveyor = false;
        seatAnchor = null;
        if (shooter != null)
            shooter.ParkInSlot(slot);
    }

    public void NotifyReturnedWithoutSlot()
    {
        isOnConveyor = false;
    }

    public void DestroyEmptyShooter()
    {
        isOnConveyor = false;
        ShrinkAndDestroyAsync().Forget();
    }

    private async UniTaskVoid ShrinkAndDestroyAsync()
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        Vector3 startScale = transform.localScale;
        const float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            if (progress > 1f)
                progress = 1f;

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (this == null)
            return;

        if (slotManager != null && shooter != null)
            slotManager.RemoveShooter(shooter);

        Destroy(gameObject);
    }

    private void RefreshCountText()
    {
        if (countText == null)
            return;

        int remaining = MaxCapacity - currentFill - inFlightShots;
        if (remaining < 0)
            remaining = 0;

        if (remaining == displayedRemaining)
            return;

        displayedRemaining = remaining;
        countText.SetText("{0:0}", (float)remaining);
    }

    private void OnDestroy()
    {
        travelCts?.Cancel();
        if (cachedFollower != null)
            cachedFollower.onEndReached -= HandleLapReached;
    }
}
