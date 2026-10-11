using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using RopeWrap.Game;
using UnityEngine;
using Zenject;

public class ConveyorManager : MonoBehaviour
{
    [Inject] private SlotManager slotManager;
    [Inject] private ImageManager imageManager;
    [InjectOptional] private RopeWrap.Game.LevelFlowController levelFlowController;
    [InjectOptional] private PlateQueueManager plateQueueManager;
    [Inject] private IFastForwardGate fastForwardGate;

    [SerializeField] private SplineComputer conveyorSpline;

    private readonly List<ShooterController> activeShooters = new List<ShooterController>(8);
    public int ActiveShooterCount => activeShooters.Count;

    public ShooterController GetActiveShooter(int index)
    {
        return activeShooters[index];
    }

    private bool failTriggered;
    private bool eventsHooked;

    private void Start()
    {
        HookEvents();
    }

    private void OnDestroy()
    {
        UnhookEvents();
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

    public void SendShooterToConveyor(ShooterController shooterController, Slot originSlot)
    {
        if (shooterController == null || conveyorSpline == null)
        {
            return;
        }

        if (activeShooters.Contains(shooterController))
            return;

        Shooter shooter = shooterController.GetComponent<Shooter>();
        if (shooter != null)
            slotManager.RemoveShooter(shooter);

        if (originSlot != null)
            originSlot.SetFull(false);

        shooterController.ReleaseOriginSlot();
        activeShooters.Add(shooterController);

        AttachPlateFollower(shooterController);
        shooterController.BeginConveyorTravel(this).Forget();
    }

    public void HandleShooterFinished(ShooterController shooterController, bool outOfAmmo)
    {
        if (shooterController == null)
            return;

        DetachPlateFollower(shooterController);
        activeShooters.Remove(shooterController);

        if (plateQueueManager != null)
            plateQueueManager.ReleaseBoundPlate(shooterController, true);

        if (outOfAmmo)
        {
            shooterController.DestroyEmptyShooter();
            TryTriggerWin();
            return;
        }

        ReturnShooterToSlotArea(shooterController);
    }

    private void ReturnShooterToSlotArea(ShooterController shooterController)
    {
        if (slotManager.GetAvailableSlot() == null)
        {
            if (fastForwardGate != null && fastForwardGate.IsActive)
            {
                HoldUntilSlotAsync(shooterController).Forget();
                return;
            }

            TriggerFail();
            shooterController.NotifyReturnedWithoutSlot();
            return;
        }

        PlaceReturningShooter(shooterController);
    }

    private async UniTaskVoid HoldUntilSlotAsync(ShooterController shooterController)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (fastForwardGate == null || !fastForwardGate.IsActive)
                break;
            if (slotManager.GetAvailableSlot() != null)
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (shooterController == null)
            return;

        if (slotManager.GetAvailableSlot() == null)
        {
            TriggerFail();
            shooterController.NotifyReturnedWithoutSlot();
            return;
        }

        PlaceReturningShooter(shooterController);
    }

    private void PlaceReturningShooter(ShooterController shooterController)
    {
        Slot freeSlot = slotManager.GetAvailableSlot();
        if (freeSlot == null)
        {
            TriggerFail();
            shooterController.NotifyReturnedWithoutSlot();
            return;
        }

        Shooter shooter = shooterController.GetComponent<Shooter>();
        slotManager.ForcePlaceShooter(shooter, freeSlot);
        shooterController.OnReturnedToSlot(freeSlot);

        if (shooter != null && ShouldAutoContinue(shooterController))
            shooter.ArmAutoContinue();

        TryTriggerWin();
    }

    private void AttachPlateFollower(ShooterController shooterController)
    {
        SplineExtensions.ClearExistingFollowers(shooterController.gameObject);

        Plate plate = plateQueueManager != null ? plateQueueManager.GetBoundPlate(shooterController) : null;
        if (plate == null)
        {
            AttachShooterFollowerFallback(shooterController);
            return;
        }

        SplineExtensions.ClearExistingFollowers(plate.gameObject);

        Shooter shooter = shooterController.GetComponent<Shooter>();
        float speed = shooter != null ? shooter.ConveyorSpeed : 1f;

        SplineFollower follower = plate.EnsureFollower();
        follower.follow = false;
        follower.spline = conveyorSpline;
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
        Shooter shooter = shooterController.GetComponent<Shooter>();
        float speed = shooter != null ? shooter.ConveyorSpeed : 1f;

        SplineFollower follower = shooterController.EnsureFollower();
        follower.follow = false;
        follower.spline = conveyorSpline;
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
            follower.onEndReached -= shooterController.HandleLapReached;
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

        return imageManager.HasRemainingPixelsOfPaletteId(shooterController.PaletteId);
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
