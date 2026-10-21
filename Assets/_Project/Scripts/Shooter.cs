using System.Collections.Generic;
using DG.Tweening;
using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Shooter : MonoBehaviour
{
    private readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Inject] private ProjectSettings projectSettings;
    [Inject] private ShooterManager shooterManager;
    [Inject] private SlotManager slotManager;
    [Inject] private TunnelManager tunnelManager;
    [Inject] private GameAudio audioPool;
    [Inject] private ObjectPool objectPool;
    [InjectOptional] private PixelShooter.Game.IFastForwardGate fastForwardGate;

    [FormerlySerializedAs("m_Renderer")]
    [SerializeField] private Renderer rendererComponent;
    [SerializeField] private Renderer extraRenderer;
    [FormerlySerializedAs("m_QuestionMaterial")]
    [SerializeField] private Material questionMaterial;
    [FormerlySerializedAs("m_QuestionMark")]
    [SerializeField] private GameObject questionMark;

    [SerializeField] private float slotTravelSpeed = 25f;
    [SerializeField] private float plateTravelSpeed = 20f;
    [SerializeField] private float plateDistanceMultiplier = 0.08f;
    [SerializeField] private float plateArcHeightMin = 7f;
    [SerializeField] private float plateArcHeightMax = 15f;
    [SerializeField] private float conveyorSpeed = 5f;
    [SerializeField] private float fireRecoilDistance = 0.2f;
    [SerializeField] private float fireRecoilDuration = 0.16f;

    public float SlotTravelSpeed => slotTravelSpeed > 0.01f ? slotTravelSpeed : 0.01f;
    public float PlateTravelSpeed => plateTravelSpeed > 0.01f ? plateTravelSpeed : 0.01f;
    public float PlateDistanceMultiplier => plateDistanceMultiplier > 0f ? plateDistanceMultiplier : 0f;
    public float ConveyorSpeed => conveyorSpeed > 0.01f ? conveyorSpeed : 0.01f;

    public float RandomPlateArcHeight()
    {
        float min = Mathf.Max(0f, plateArcHeightMin);
        float max = Mathf.Max(min, plateArcHeightMax);

        return Random.Range(min, max);
    }

    private bool isClickable;
    public bool IsClickable => isClickable;

    private float openOutlineWidth = 1f;
    private bool outlineStateReady;
    private bool outlineIsOpen;
    private bool keepOutlineOpen;

    private bool isQuestionShooter;
    public bool IsQuestionShooter => isQuestionShooter;

    private Vector2Int coordinate;
    public Vector2Int Coordinate => coordinate;

    private bool isTunnelShooter;
    public bool IsTunnelShooter => isTunnelShooter;

    private Slot mySlot;

    private GeneralColor color;
    public GeneralColor Color => color;

    private Material shooterMaterial;
    private Transform cachedTransform;
    private MaterialPropertyBlock propertyBlock;
    private Color shooterColor;
    private ShooterController shooterController;

    private Sequence placementBounceSequence;
    private bool isBusy;
    public bool IsBusy => isBusy;

    private bool isWaitingInSlot;
    public bool IsWaitingInSlot => isWaitingInSlot;

    public bool IsHeldInSlot
    {
        get
        {
            if (mySlot == null)
                return false;

            ShooterController controller = GetController();
            if (controller != null && controller.IsTraveling)
                return false;

            return true;
        }
    }

    private bool continueWhenSeated;
    private bool presentedLocked;
    private bool unlockHandled;
    private bool holdPresentationScale;
    private bool homePoseCached;
    private Vector3 homeLocalScale;
    private Vector3 homePosition;
    private int revealTweenId;

    private Transform fireVisual;
    private Vector3 fireVisualRestPosition;
    private Vector3 fireVisualRecoilPosition;
    private bool fireVisualReady;
    private bool fireRecoilQueued;
    private Tween fireRecoilTween;

    private const float LockedScaleFactor = 0.78f;
    private const float FireRecoilOvershoot = 1.35f;
    private const float UnlockLift = 0.55f;
    private const float UnlockRiseDuration = 0.34f;
    private const float UnlockSettleDuration = 0.4f;

    public void Construct(ShooterData data)
    {
        cachedTransform = transform;
        cachedTransform.position = data.Position;

        CacheHomePose();
        coordinate = data.Coordinate;
        color = data.Color;

        isQuestionShooter = data.IsQuestionShooter;
        questionMark.SetActive(isQuestionShooter);
        isBusy = false;

        propertyBlock = new MaterialPropertyBlock();
        SetStartColor(data);

        ShooterController controller = GetController();

        if (controller != null)
        {
            controller.SetAmmoCapacity(data.ShotCount);
            controller.BindPalette(data.Color);
            controller.ShowAmmoCount(!isQuestionShooter);
        }
    }

    public ShooterController Controller => GetController();

    private ShooterController GetController()
    {
        if (shooterController == null)
            shooterController = GetComponent<ShooterController>();

        return shooterController;
    }

    private void SetStartColor(ShooterData data)
    {
        FacadeProperty facade = projectSettings.GetOriginalFacade((int)data.Color - 1);
        Material[] originalMaterials = facade != null ? facade.ShooterMaterials : null;
        shooterMaterial = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
        shooterColor = facade != null ? facade.Color : shooterColor;
        CacheOpenOutlineWidth();
        SetMaterial(isQuestionShooter);
    }

    public void ApplyPaletteSwatch(Color swatch)
    {
        shooterColor = swatch;

        if (!isQuestionShooter)
            ApplyPropertyBlock(ShouldShowOutline() ? openOutlineWidth : 0f);
    }

    public void SetMaterial(bool isQuestion)
    {
        Material material = isQuestion ? questionMaterial : shooterMaterial;

        if (rendererComponent != null)
            rendererComponent.sharedMaterial = material;

        if (extraRenderer != null)
            extraRenderer.sharedMaterial = material;

        ApplyPropertyBlock(isQuestion ? 0f : (ShouldShowOutline() ? openOutlineWidth : 0f));
    }

    public void SetClickable(bool clickable)
    {
        if (isClickable == clickable)
            return;

        isClickable = clickable;
        SetOutline(clickable);
    }

    private bool ShouldShowOutline() => keepOutlineOpen || isClickable;

    public void SetTunnelShooter(bool tunnelShooter) => isTunnelShooter = tunnelShooter;

    public void PlayFireRecoil()
    {
        if (!EnsureFireVisual())
            return;

        if (fireRecoilTween != null && fireRecoilTween.IsActive())
        {
            fireRecoilQueued = true;
            return;
        }

        BeginFireRecoil();
    }

    private void OnDisable()
    {
        fireRecoilQueued = false;
        fireRecoilTween?.Kill();
        fireRecoilTween = null;

        if (fireVisualReady && fireVisual != null)
            fireVisual.localPosition = fireVisualRestPosition;
    }

    private bool EnsureFireVisual()
    {
        if (fireVisualReady)
            return fireVisual != null;

        fireVisualReady = true;

        if (extraRenderer == null)
            return false;

        fireVisual = extraRenderer.transform;
        fireVisualRestPosition = fireVisual.localPosition;
        fireVisualRecoilPosition = fireVisualRestPosition;
        fireVisualRecoilPosition.z -= fireRecoilDistance > 0f ? fireRecoilDistance : 0f;
        return true;
    }

    private void BeginFireRecoil()
    {
        fireRecoilQueued = false;
        fireRecoilTween?.Kill();

        float duration = fireRecoilDuration > 0.02f ? fireRecoilDuration : 0.02f;
        fireVisual.localPosition = fireVisualRecoilPosition;

        fireRecoilTween = fireVisual
            .DOLocalMove(fireVisualRestPosition, duration)
            .SetEase(Ease.OutBack, FireRecoilOvershoot)
            .SetRecyclable(true)
            .OnComplete(HandleFireRecoilComplete);
    }

    private void HandleFireRecoilComplete()
    {
        fireRecoilTween = null;
        fireVisual.localPosition = fireVisualRestPosition;

        if (!fireRecoilQueued || !isActiveAndEnabled)
        {
            fireRecoilQueued = false;
            return;
        }

        BeginFireRecoil();
    }

    public void SetHoldPresentationScale(bool hold) => holdPresentationScale = hold;

    public Vector3 LockedLocalScale => (homePoseCached ? homeLocalScale : cachedTransform.localScale) * LockedScaleFactor;

    public Vector3 ExitScale(bool pathOpen)
    {
        if (pathOpen)
            return homePoseCached ? homeLocalScale : Vector3.one;

        return LockedLocalScale;
    }

    public void PrepareTunnelExit()
    {
        SetClickable(false);
        SetOutline(false);
        SetHoldPresentationScale(true);

        ShooterController board = GetController();
        if (board != null)
            board.ShowAmmoCount(false);
    }

    public void SetCoordinate(Vector2Int value) => coordinate = value;

    public void SetOutline(bool open)
    {
        if (keepOutlineOpen)
            open = true;

        if (outlineStateReady && outlineIsOpen == open)
            return;

        ApplyPropertyBlock(open ? openOutlineWidth : 0f);
    }

    public void RefreshLockState()
    {
        if (!isClickable)
        {
            if (unlockHandled)
                Relock();
            else
                ApplyLockedLook();
            return;
        }

        if (holdPresentationScale || unlockHandled)
            return;

        unlockHandled = true;
        if (presentedLocked)
            PlayUnlockAnimation();
        else
            ApplyOpenLook();
    }

    public bool OnClick()
    {
        if (slotManager.IsHalted)
            return false;

        bool sent = IsHeldInSlot ? TryLeaveSlot() : TryLaunchFromGrid();
        if (!sent)
            return false;

        PlaySelectSound();
        return true;
    }

    public bool TryLaunchFromGrid()
    {
        if (!CanLeaveGrid())
            return false;

        ShooterController controller = GetController();
        if (controller == null || !controller.TryHoldPlate())
            return false;

        if (!slotManager.HasOpenSlot())
        {
            controller.ReleaseHeldPlate();
            return false;
        }

        MarkDeparting();

        if (!slotManager.TrySeatShooter(this))
        {
            CancelDeparting();
            controller.ReleaseHeldPlate();
            return false;
        }

        shooterManager.RemoveShooter(coordinate);
        return true;
    }

    public bool TryEnterSlot()
    {
        if (!CanLeaveGrid())
            return false;

        MarkDeparting();

        if (!slotManager.TrySeatShooter(this))
        {
            CancelDeparting();
            return false;
        }

        shooterManager.RemoveShooter(coordinate);
        return true;
    }

    private bool CanLeaveGrid()
    {
        return !isBusy && isClickable && !isWaitingInSlot && !slotManager.IsHalted;
    }

    private void MarkDeparting()
    {
        keepOutlineOpen = true;
        SetClickable(false);
        isBusy = true;
        KillRevealTweens();

        if (homePoseCached && cachedTransform != null)
            cachedTransform.localScale = homeLocalScale;
    }

    private void CancelDeparting()
    {
        isBusy = false;
        keepOutlineOpen = false;
        SetClickable(true);
    }

    private void PlaySelectSound()
    {
        if (audioPool != null)
            audioPool.PlayShooterSelect();
    }

    public void ArmAutoContinue() => continueWhenSeated = true;

    public void ParkInSlot(Slot slot)
    {
        mySlot = slot;
        isBusy = false;
        isWaitingInSlot = true;
        SetClickable(true);

        if (!continueWhenSeated)
            return;

        continueWhenSeated = false;
        if (fastForwardGate != null && !fastForwardGate.IsActive)
            return;

        ContinueFromSlot();
    }

    public void ContinueFromSlot()
    {
        TryLeaveSlot();
    }

    private bool TryLeaveSlot()
    {
        if (mySlot == null || slotManager.IsHalted)
            return false;

        ShooterController controller = GetController();
        if (controller == null || !controller.TryHoldPlate())
            return false;

        isWaitingInSlot = false;
        keepOutlineOpen = true;
        SetClickable(false);
        isBusy = true;

        if (controller.TryDepart(mySlot, color))
            return true;

        if (controller.IsTraveling)
            return false;

        isBusy = false;
        isWaitingInSlot = true;
        SetClickable(true);
        return false;
    }

    public void ReturnToSlot(Slot slot)
    {
        mySlot = slot;
        isBusy = true;
        isWaitingInSlot = false;

        SetClickable(false);
        MoveToSlot(slot, () => ParkInSlot(slot));
    }

    public void DepartFromSlot(Slot slot)
    {
        mySlot = slot;
        isBusy = true;

        ShooterController controller = GetController();

        if (controller != null && controller.TryDepart(slot, color))
        {
            tunnelManager.ControlTunnel();
            return;
        }

        PlaySlotLanding(slot);
    }

    private void PlaySlotLanding(Slot slot)
    {
        mySlot = slot;
        isBusy = true;

        MoveToSlot(slot, PlayPlacementBounce);
    }

    private void MoveToSlot(Slot slot, System.Action onArrived)
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        SplineExtensions.ClearExistingFollowers(gameObject, objectPool);

        Vector3 slotPos = slot.transform.position;
        Quaternion startRotation = cachedTransform.rotation;
        Quaternion slotRotation = Quaternion.identity;

        gameObject.FollowTo(
            objectPool,
            slot.transform,
            SlotTravelSpeed,
            onComplete: () =>
            {
                if (cachedTransform == null)
                    return;

                cachedTransform.position = slotPos;
                cachedTransform.rotation = slotRotation;

                ShooterController controller = GetController();

                if (controller != null)
                    controller.ShowAmmoCount(true);

                onArrived?.Invoke();
            },
            onUpdate: progress =>
            {
                transform.rotation = Quaternion.Slerp(startRotation, slotRotation, progress);
            });
    }

    private void PlayPlacementBounce()
    {
        placementBounceSequence?.Kill();

        Vector3 shooterScale = cachedTransform.localScale;
        Transform slotTransform = mySlot.transform;
        Vector3 slotScale = slotTransform.localScale;

        placementBounceSequence = DOTween.Sequence();
        placementBounceSequence.SetRecyclable(true);
        placementBounceSequence.Append(cachedTransform.DOScale(shooterScale * 0.95f, 0.05f).SetEase(Ease.Linear));
        placementBounceSequence.Append(slotTransform.DOScale(slotScale * 0.95f, 0.05f).SetEase(Ease.Linear));
        placementBounceSequence.Append(cachedTransform.DOScale(shooterScale, 0.05f).SetEase(Ease.Linear));
        placementBounceSequence.Append(slotTransform.DOScale(slotScale, 0.05f).SetEase(Ease.Linear));
        placementBounceSequence.OnKill(() => RestorePlacementScales(shooterScale, slotTransform, slotScale));
        placementBounceSequence.OnComplete(() =>
        {
            RestorePlacementScales(shooterScale, slotTransform, slotScale);
            tunnelManager.ControlTunnel();

            isBusy = false;
            isWaitingInSlot = true;
            SetClickable(true);
        });
    }

    private void RestorePlacementScales(Vector3 shooterScale, Transform slotTransform, Vector3 slotScale)
    {
        if (cachedTransform != null)
            cachedTransform.localScale = shooterScale;

        if (slotTransform != null)
            slotTransform.localScale = slotScale;
    }

    public void ClearBusy() => isBusy = false;

    public void RestoreSlotWait()
    {
        if (mySlot == null)
            return;

        ShooterController controller = GetController();
        if (controller != null && controller.IsTraveling)
            return;

        isBusy = false;
        isWaitingInSlot = true;
        SetClickable(true);
    }

    private void OnDestroy()
    {
        KillRevealTweens();
        placementBounceSequence?.Kill();
    }

    private void KillRevealTweens()
    {
        if (revealTweenId != 0)
            DOTween.Kill(revealTweenId, false);
    }

    private int GetRevealTweenId()
    {
        if (revealTweenId == 0)
            revealTweenId = GetInstanceID();

        return revealTweenId;
    }

    private void CacheHomePose()
    {
        if (homePoseCached || cachedTransform == null)
            return;

        homeLocalScale = cachedTransform.localScale;
        homePosition = cachedTransform.position;
        homePoseCached = true;
    }

    private void Relock()
    {
        unlockHandled = false;
        KillRevealTweens();
        ApplyLockedLook();

        if (cachedTransform != null && homePoseCached)
            cachedTransform.position = homePosition;
    }

    private void ApplyLockedLook()
    {
        presentedLocked = true;
        SetOutline(false);

        ShooterController controller = GetController();

        if (controller != null)
            controller.ShowAmmoCount(false);

        if (holdPresentationScale || cachedTransform == null || !homePoseCached)
            return;

        cachedTransform.localScale = homeLocalScale * LockedScaleFactor;
    }

    private void ApplyOpenLook()
    {
        RestoreHomePose();
        RevealQuestionIfNeeded();
        SetOutline(true);

        ShooterController controller = GetController();

        if (controller != null)
            controller.ShowAmmoCount(true);
    }

    private void PlayUnlockAnimation()
    {
        RevealQuestionIfNeeded();
        SetOutline(true);

        ShooterController controller = GetController();

        if (controller != null)
            controller.ShowAmmoCount(true);

        if (cachedTransform == null || !homePoseCached)
        {
            RestoreHomePose();
            return;
        }

        int tweenId = GetRevealTweenId();
        DOTween.Kill(tweenId, false);
        cachedTransform.position = homePosition;
        cachedTransform.localScale = homeLocalScale * LockedScaleFactor;

        Vector3 lifted = homePosition + Vector3.up * UnlockLift;
        Vector3 peakScale = homeLocalScale * 1.2f;

        cachedTransform
            .DOMove(lifted, UnlockRiseDuration)
            .SetEase(Ease.Linear)
            .SetId(tweenId)
            .SetRecyclable(true)
            .OnComplete(PlayUnlockSettle);

        cachedTransform
            .DOScale(peakScale, UnlockRiseDuration)
            .SetEase(Ease.Linear)
            .SetId(tweenId)
            .SetRecyclable(true);
    }

    private void PlayUnlockSettle()
    {
        if (cachedTransform == null || !homePoseCached)
            return;

        int tweenId = GetRevealTweenId();

        cachedTransform
            .DOMove(homePosition, UnlockSettleDuration)
            .SetEase(Ease.Linear)
            .SetId(tweenId)
            .SetRecyclable(true)
            .OnComplete(HandleUnlockComplete);

        cachedTransform
            .DOScale(homeLocalScale, UnlockSettleDuration)
            .SetEase(Ease.Linear)
            .SetId(tweenId)
            .SetRecyclable(true);
    }

    private void HandleUnlockComplete()
    {
        if (!isClickable)
            return;

        RestoreHomePose();
    }

    private void RestoreHomePose()
    {
        if (cachedTransform == null || !homePoseCached)
            return;

        cachedTransform.position = homePosition;
        cachedTransform.localScale = homeLocalScale;
    }

    private void RevealQuestionIfNeeded()
    {
        if (!isQuestionShooter)
            return;

        isQuestionShooter = false;
        questionMark.SetActive(false);
        SetMaterial(false);
    }

    private void CacheOpenOutlineWidth()
    {
        if (shooterMaterial != null && shooterMaterial.HasProperty(OutlineWidthId))
            openOutlineWidth = shooterMaterial.GetFloat(OutlineWidthId);
    }

    private void ApplyPropertyBlock(float width)
    {
        if (propertyBlock == null)
            return;

        outlineStateReady = true;
        outlineIsOpen = width > 0f;
        propertyBlock.Clear();

        if (!isQuestionShooter)
        {
            propertyBlock.SetColor(BaseColorId, shooterColor);
        }

        propertyBlock.SetFloat(OutlineWidthId, width);

        if (rendererComponent != null)
            rendererComponent.SetPropertyBlock(propertyBlock);

        if (extraRenderer != null)
            extraRenderer.SetPropertyBlock(propertyBlock);
    }

    public class Factory : PlaceholderFactory<Shooter>
    {
    }
}
