using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using PixelShooter.Game;
using PixelShooter.Pooling;
using UnityEngine;
using Zenject;

public class ConveyorManager : MonoBehaviour
{
    [Inject] private SlotManager slotManager;
    [Inject] private ImageManager imageManager;
    [InjectOptional] private LevelFlowController levelFlowController;
    [InjectOptional] private PlateQueueManager plateQueueManager;
    [Inject] private IFastForwardGate fastForwardGate;
    [Inject] private ObjectPool objectPool;

    [SerializeField] private SplineComputer conveyorSpline;

    private readonly List<ShooterController> activeShooters = new List<ShooterController>(8);
    public int ActiveShooterCount => activeShooters.Count;

    private bool failTriggered;
    private bool isStopped;
    private bool eventsHooked;

    public bool IsStopped => isStopped;

    private void Start()
    {
        HookEvents();

        if (levelFlowController != null)
            levelFlowController.GameplayStopped += Stop;
    }

    private void OnDestroy()
    {
        if (levelFlowController != null)
            levelFlowController.GameplayStopped -= Stop;

        UnhookEvents();
    }

    public ShooterController GetActiveShooter(int index) => activeShooters[index];

    public void Stop()
    {
        if (isStopped)
            return;

        isStopped = true;
        for (int i = activeShooters.Count - 1; i >= 0; i--)
        {
            ShooterController shooterController = activeShooters[i];
            if (shooterController == null)
                continue;

            shooterController.StopTravel();
        }
    }

    private void HookEvents()
    {
        if (eventsHooked || imageManager == null)
            return;

        imageManager.OnImageCleared += TryTriggerWin;
        eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!eventsHooked || imageManager == null)
            return;

        imageManager.OnImageCleared -= TryTriggerWin;
        eventsHooked = false;
    }

    public bool SendShooterToConveyor(ShooterController shooterController, Slot originSlot)
    {
        if (isStopped || shooterController == null || conveyorSpline == null)
            return false;

        if (activeShooters.Contains(shooterController))
            return true;

        Shooter shooter = shooterController.Owner;
        if (shooter != null)
            slotManager.RemoveShooter(shooter);

        if (originSlot != null)
            originSlot.SetFull(false);

        shooterController.EnterConveyor();
        activeShooters.Add(shooterController);

        AttachPlateFollower(shooterController);
        shooterController.BeginTravel(this).Forget();
        return true;
    }

    public void HandleShooterFinished(ShooterController shooterController, bool outOfAmmo)
    {
        if (isStopped || shooterController == null)
            return;

        DetachPlateFollower(shooterController);
        activeShooters.Remove(shooterController);

        if (plateQueueManager != null)
            plateQueueManager.ReleaseBoundPlate(shooterController, true);

        if (outOfAmmo)
        {
            shooterController.DestroyEmpty();
            TryTriggerWin();
            return;
        }

        ReturnShooterToSlotArea(shooterController);
    }

    private void ReturnShooterToSlotArea(ShooterController shooterController)
    {
        if (isStopped)
            return;

        if (slotManager.GetAvailableSlot() == null)
        {
            if (fastForwardGate != null && fastForwardGate.IsActive)
            {
                HoldUntilSlotAsync(shooterController).Forget();
                return;
            }

            TriggerFail();
            shooterController.LeaveConveyor();
            return;
        }

        PlaceReturningShooter(shooterController);
    }

    private async UniTaskVoid HoldUntilSlotAsync(ShooterController shooterController)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (isStopped)
                break;
            if (fastForwardGate == null || !fastForwardGate.IsActive)
                break;
            if (slotManager.GetAvailableSlot() != null)
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (isStopped || shooterController == null)
            return;

        if (slotManager.GetAvailableSlot() == null)
        {
            TriggerFail();
            shooterController.LeaveConveyor();
            return;
        }

        PlaceReturningShooter(shooterController);
    }

    private void PlaceReturningShooter(ShooterController shooterController)
    {
        if (isStopped)
            return;

        Slot freeSlot = slotManager.GetAvailableSlot();
        if (freeSlot == null)
        {
            TriggerFail();
            shooterController.LeaveConveyor();
            return;
        }

        Shooter shooter = shooterController.Owner;
        slotManager.ForcePlaceShooter(shooter, freeSlot);
        shooterController.ReturnToSlot(freeSlot);

        if (shooter != null && ShouldAutoContinue(shooterController))
            shooter.ArmAutoContinue();

        TryTriggerWin();
    }

    private void AttachPlateFollower(ShooterController shooterController)
    {
        SplineExtensions.ClearExistingFollowers(shooterController.gameObject, objectPool);

        Plate plate = plateQueueManager != null ? plateQueueManager.GetBoundPlate(shooterController) : null;
        if (plate == null)
        {
            AttachShooterFollowerFallback(shooterController);
            return;
        }

        SplineExtensions.ClearExistingFollowers(plate.gameObject, objectPool);

        Shooter shooter = shooterController.Owner;
        float speed = shooter != null ? shooter.ConveyorSpeed : 1f;

        SplineFollower follower = plate.GetTravelFollower();
        follower.follow = false;
        follower.spline = conveyorSpline;
        follower.updateMethod = SplineUser.UpdateMethod.Update;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.followSpeed = speed;
        follower.wrapMode = SplineFollower.Wrap.Loop;
        follower.motion.applyPosition = true;
        follower.motion.applyRotation = true;

        Vector3 flatEuler = plate.FlatEuler;
        follower.motion.rotationOffset = flatEuler;

        SplineSample startSample = conveyorSpline.Evaluate(0.0);
        Vector3 followPosition = startSample.position;
        Quaternion followRotation = Quaternion.LookRotation(Flatten(startSample.forward), Vector3.up)
            * Quaternion.Euler(flatEuler);
        Vector2 followOffset = Vector2.zero;

        if (plateQueueManager != null)
        {
            plateQueueManager.GetConveyorStartPose(plate, out Vector3 boardPos, out Quaternion boardRot);
            Vector3 delta = boardPos - startSample.position;
            followOffset = new Vector2(
                Vector3.Dot(delta, startSample.right),
                Vector3.Dot(delta, startSample.up));
            followPosition = startSample.position
                + startSample.right * followOffset.x
                + startSample.up * followOffset.y;
            followRotation = boardRot;
        }

        follower.motion.offset = followOffset;
        follower.SetPercent(0.0);
        plate.transform.SetPositionAndRotation(followPosition, followRotation);

        shooterController.SetTravelFollower(follower);
        follower.follow = true;
    }

    private Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
    }

    private void AttachShooterFollowerFallback(ShooterController shooterController)
    {
        Shooter shooter = shooterController.Owner;
        float speed = shooter != null ? shooter.ConveyorSpeed : 1f;

        SplineFollower follower = shooterController.GetTravelFollower();

        follower.follow = false;
        follower.spline = conveyorSpline;
        follower.updateMethod = SplineUser.UpdateMethod.Update;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.followSpeed = speed;
        follower.wrapMode = SplineFollower.Wrap.Loop;
        follower.motion.applyPosition = true;
        follower.motion.applyRotation = false;
        follower.SetPercent(0.0);
        shooterController.SetTravelFollower(follower);
        follower.follow = true;
    }

    private void DetachPlateFollower(ShooterController shooterController)
    {
        SplineFollower follower = shooterController.CachedFollower;

        if (follower != null)
        {
            follower.follow = false;
            follower.onEndReached -= shooterController.OnLapReached;
            follower.spline = null;
        }

        Plate plate = plateQueueManager != null ? plateQueueManager.GetBoundPlate(shooterController) : null;

        if (plate != null)
            plate.DetachFollower();

        shooterController.ClearTravelFollower();
    }

    private bool ShouldAutoContinue(ShooterController shooterController)
    {
        if (fastForwardGate == null || !fastForwardGate.IsActive)
            return false;

        if (imageManager == null || imageManager.IsImageCleared())
            return false;

        if (shooterController.RemainingShots <= 0)
            return false;

        return imageManager.HasClaimablePixel(shooterController.PaletteId);
    }

    private void TriggerFail()
    {
        if (failTriggered)
            return;

        failTriggered = true;
        slotManager.NotifyShooterAreaStuck();
    }

    private void TryTriggerWin()
    {
        HookEvents();

        if (imageManager == null || levelFlowController == null)
            return;

        if (!imageManager.IsImageCleared())
            return;

        if (activeShooters.Count > 0)
            return;

        levelFlowController.TriggerWin();
    }
}
