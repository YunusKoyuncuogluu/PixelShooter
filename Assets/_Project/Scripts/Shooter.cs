using System.Collections.Generic;
using DG.Tweening;
using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Shooter : MonoBehaviour
{
    private readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private readonly int HighlightColorId = Shader.PropertyToID("_HColor");
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
    [SerializeField] private float conveyorSpeed = 5f;

    public float SlotTravelSpeed => slotTravelSpeed > 0.01f ? slotTravelSpeed : 0.01f;
    public float PlateTravelSpeed => plateTravelSpeed > 0.01f ? plateTravelSpeed : 0.01f;
    public float ConveyorSpeed => conveyorSpeed > 0.01f ? conveyorSpeed : 0.01f;

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

    private Sequence scaleSequence;
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
    private bool homePoseCached;
    private Vector3 homeLocalScale;
    private Vector3 homePosition;
    private Sequence revealSequence;

    private const float LockedScaleFactor = 0.78f;
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

    private ShooterController GetController()
    {
        if (shooterController == null)
            shooterController = GetComponent<ShooterController>();

        return shooterController;
    }

    private void SetStartColor(ShooterData data)
    {
        List<FacadeProperty> facadeProperties = projectSettings.GetOriginalFacadeProperties();
        Material[] originalMaterials = facadeProperties[(int)data.Color - 1].ShooterMaterials;
        shooterMaterial = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
        shooterColor = facadeProperties[(int)data.Color - 1].Color;
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
            else if (!presentedLocked)
                ApplyLockedLook();
            return;
        }

        if (unlockHandled)
        {
            return;
        }

        unlockHandled = true;
        if (presentedLocked)
            PlayUnlockAnimation();
        else
            ApplyOpenLook();
    }

    public void OnClick()
    {
        if (slotManager.IsHalted)
            return;

        if (IsHeldInSlot)
        {
            if (audioPool != null)
                audioPool.PlayShooterSelect();

            ContinueFromSlot();
            return;
        }

        if (isBusy || !isClickable)
            return;

        if (audioPool != null)
            audioPool.PlayShooterSelect();

        SendToSlot();
    }

    public void SendToSlot()
    {
        if (isBusy || !isClickable || isWaitingInSlot || slotManager.IsHalted)
            return;

        keepOutlineOpen = true;
        SetClickable(false);
        isBusy = true;
        revealSequence?.Kill();

        if (homePoseCached && cachedTransform != null)
            cachedTransform.localScale = homeLocalScale;

        shooterManager.RemoveShooter(coordinate);
        slotManager.SaveShooter(this);
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
        if (mySlot == null || slotManager.IsHalted)
            return;

        ShooterController controller = GetController();
        if (controller == null)
            return;

        isWaitingInSlot = false;
        keepOutlineOpen = true;
        SetClickable(false);
        isBusy = true;

        if (controller.StartRun(mySlot, color))
            return;

        if (controller.IsTraveling)
            return;

        isBusy = false;
        isWaitingInSlot = true;
        SetClickable(true);
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
        scaleSequence?.Kill();

        Vector3 shooterScale = cachedTransform.localScale;
        Transform slotTransform = mySlot.transform;
        Vector3 slotScale = slotTransform.localScale;

        scaleSequence = DOTween.Sequence();
        scaleSequence.Append(cachedTransform.DOScale(shooterScale * 0.95f, 0.05f).SetEase(Ease.Linear));
        scaleSequence.Append(slotTransform.DOScale(slotScale * 0.95f, 0.05f).SetEase(Ease.Linear));
        scaleSequence.Append(cachedTransform.DOScale(shooterScale, 0.05f).SetEase(Ease.Linear));
        scaleSequence.Append(slotTransform.DOScale(slotScale, 0.05f).SetEase(Ease.Linear));
        scaleSequence.OnKill(() => RestorePlacementScales(shooterScale, slotTransform, slotScale));
        scaleSequence.OnComplete(() =>
        {
            RestorePlacementScales(shooterScale, slotTransform, slotScale);
            tunnelManager.ControlTunnel();

            ShooterController controller = GetController();

            if (controller != null && controller.StartRun(mySlot, color))
                return;

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
        revealSequence?.Kill();
        scaleSequence?.Kill();
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
        revealSequence?.Kill();
        revealSequence = null;
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

        if (cachedTransform == null || !homePoseCached)
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

        revealSequence?.Kill();
        cachedTransform.position = homePosition;
        cachedTransform.localScale = homeLocalScale * LockedScaleFactor;

        Vector3 lifted = homePosition + Vector3.up * UnlockLift;
        Vector3 peakScale = homeLocalScale * 1.2f;

        revealSequence = DOTween.Sequence();
        revealSequence.SetTarget(cachedTransform);
        revealSequence.SetRecyclable(true);
        revealSequence.Append(cachedTransform.DOMove(lifted, UnlockRiseDuration).SetEase(Ease.OutCubic));
        revealSequence.Join(cachedTransform.DOScale(peakScale, UnlockRiseDuration).SetEase(Ease.OutCubic));
        revealSequence.AppendInterval(0.08f);
        revealSequence.Append(cachedTransform.DOMove(homePosition, UnlockSettleDuration).SetEase(Ease.InOutSine));
        revealSequence.Join(cachedTransform.DOScale(homeLocalScale, UnlockSettleDuration).SetEase(Ease.InOutSine));
        revealSequence.OnComplete(() =>
        {
            if (!isClickable)
                return;

            RestoreHomePose();
        });
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
            //propertyBlock.SetColor(HighlightColorId, UnityEngine.Color.white);
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
