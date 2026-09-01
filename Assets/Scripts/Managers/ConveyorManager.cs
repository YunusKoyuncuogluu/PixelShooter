using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using UnityEngine;
using Zenject;

/// <summary>
/// Conveyor routing: plate SplineFollower tasiyicidir (duz kalir).
/// Thread seat'te yan oturur; sarma spool world position ile calisir.
/// </summary>
public class ConveyorManager : MonoBehaviour
{
    [Inject] private SlotManager slotManager;
    [Inject] private FabricManager fabricManager;
    [InjectOptional] private RopeWrap.Game.LevelFlowController levelFlowController;
    [InjectOptional] private PlateQueueManager plateQueueManager;

    [Header("Conveyor Track")]
    [SerializeField] private SplineComputer conveyorSpline;

    private readonly List<SpoolController> activeSpools = new List<SpoolController>(8);
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
        if (eventsHooked || fabricManager == null)
            return;

        fabricManager.OnFabricCleared += TryTriggerWin;
        eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!eventsHooked || fabricManager == null)
            return;

        fabricManager.OnFabricCleared -= TryTriggerWin;
        eventsHooked = false;
    }

    public void SendSpoolToConveyor(SpoolController spool, Slot originSlot)
    {
        if (spool == null || conveyorSpline == null)
        {
            Debug.LogWarning("[ConveyorManager] Spool veya ConveyorSpline eksik.");
            return;
        }

        if (activeSpools.Contains(spool))
            return;

        Thread thread = spool.GetComponent<Thread>();
        if (thread != null)
            slotManager.RemoveThread(thread);

        if (originSlot != null)
            originSlot.SetFull(false);

        spool.ReleaseOriginSlot();
        activeSpools.Add(spool);

        AttachPlateFollower(spool);
        spool.BeginConveyorTravel(this).Forget();
    }

    public void HandleSpoolFinished(SpoolController spool, bool completedFull)
    {
        if (spool == null)
            return;

        DetachPlateFollower(spool);
        activeSpools.Remove(spool);

        if (plateQueueManager != null)
            plateQueueManager.ReleaseBoundPlate(spool, returnToQueue: !completedFull);

        if (completedFull)
        {
            spool.PlayCompleteAndDestroyAsync().Forget();
            TryTriggerWin();
            return;
        }

        ReturnSpoolToSlotArea(spool);
    }

    private void ReturnSpoolToSlotArea(SpoolController spool)
    {
        if (slotManager.IsAreaFull)
        {
            TriggerFail();
            spool.NotifyReturnedWithoutSlot();
            return;
        }

        Slot freeSlot = slotManager.GetAvailableSlot();
        if (freeSlot == null)
        {
            TriggerFail();
            spool.NotifyReturnedWithoutSlot();
            return;
        }

        Thread thread = spool.GetComponent<Thread>();
        slotManager.ForcePlaceThread(thread, freeSlot);
        spool.OnReturnedToSlot(freeSlot);
        TryTriggerWin();
    }

    /// <summary>
    /// Follower plate uzerinde: plate duz ilerler, thread seat child olarak yan kalir.
    /// </summary>
    private void AttachPlateFollower(SpoolController spool)
    {
        SplineExtensions.ClearExistingFollowers(spool.gameObject);

        Plate plate = plateQueueManager != null ? plateQueueManager.GetBoundPlate(spool) : null;
        if (plate == null)
        {
            Debug.LogWarning("[ConveyorManager] Bound plate yok; spool follower fallback.");
            AttachSpoolFollowerFallback(spool);
            return;
        }

        SplineExtensions.ClearExistingFollowers(plate.gameObject);

        Thread thread = spool.GetComponent<Thread>();
        float speed = thread != null ? thread.ConveyorSpeed : 1f;

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

        // BoardingAnchor (veya spline 0) ile spline ornegi arasindaki ofset:
        // plate yolculuk boyunca ayni yukseklikte kalir, spline'a zip'lenmez.
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

        spool.SetTravelFollower(follower);
        follower.follow = true;
    }

    private Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
    }

    private void AttachSpoolFollowerFallback(SpoolController spool)
    {
        Thread thread = spool.GetComponent<Thread>();
        float speed = thread != null ? thread.ConveyorSpeed : 1f;

        SplineFollower follower = spool.EnsureFollower();
        follower.follow = false;
        follower.spline = conveyorSpline;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.followSpeed = speed;
        follower.wrapMode = SplineFollower.Wrap.Loop;
        follower.motion.applyPosition = true;
        follower.motion.applyRotation = true;
        follower.SetPercent(0.0);
        spool.SetTravelFollower(follower);
        follower.follow = true;
    }

    private void DetachPlateFollower(SpoolController spool)
    {
        SplineFollower follower = spool.CachedFollower;
        if (follower != null)
        {
            follower.follow = false;
            follower.onEndReached -= spool.HandleOrbitEndReached;
            follower.spline = null;
        }

        Plate plate = plateQueueManager != null ? plateQueueManager.GetBoundPlate(spool) : null;
        if (plate != null)
            plate.DetachFollower();

        spool.ClearTravelFollower();
    }

    private void TriggerFail()
    {
        if (failTriggered)
            return;

        failTriggered = true;
        slotManager.NotifySpoolAreaStuck();
    }

    private void TryTriggerWin()
    {
        HookEvents();

        if (fabricManager == null || levelFlowController == null)
            return;

        if (!fabricManager.IsFabricCleared())
            return;

        if (slotManager.SpoolCount > 0 || activeSpools.Count > 0)
            return;

        levelFlowController.TriggerWin();
    }
}
