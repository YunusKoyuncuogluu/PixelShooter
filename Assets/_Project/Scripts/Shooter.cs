using System.Collections.Generic;
using DG.Tweening;
using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class Shooter : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

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

    private ShooterData shooterData;
    private Material shooterMaterial;
    private Transform cachedTransform;
    private MaterialPropertyBlock propertyBlock;
    private Color shooterColor;

    private Sequence scaleSequence;
    private bool isBusy;
    public bool IsBusy => isBusy;

    private bool isWaitingInSlot;
    public bool IsWaitingInSlot => isWaitingInSlot;

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
        shooterData = data;
        cachedTransform.position = shooterData.Position;
        CacheHomePose();
        coordinate = shooterData.Coordinate;
        color = data.Color;
        isQuestionShooter = data.IsQuestionShooter;
        questionMark.SetActive(isQuestionShooter);
        isBusy = false;
        propertyBlock = new MaterialPropertyBlock();
        SetStartColor();

        ShooterController shooterController = GetComponent<ShooterController>();
        if (shooterController != null)
        {
            shooterController.SetShotCapacity(shooterData.ShotCount);
            shooterController.SetCountTextVisible(!isQuestionShooter);
        }
    }

    private void SetStartColor()
    {
        List<FacadeProperty> facadeProperties = projectSettings.GetOriginalFacadeProperties();
        Material[] originalMaterials = facadeProperties[(int)shooterData.Color - 1].ShooterMaterials;
        shooterMaterial = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
        shooterColor = facadeProperties[(int)shooterData.Color - 1].Color;
        CacheOpenOutlineWidth();
        SetMaterial(isQuestionShooter);
    }

    public void ApplyPaletteSwatch(Color swatch)
    {
        shooterColor = swatch;
        if (!isQuestionShooter)
        {
            ApplyPropertyBlock(ShouldShowOutline() ? openOutlineWidth : 0f);
        }
    }

    public void SetMaterial(bool isQuestion)
    {
        Material material = isQuestion ? questionMaterial : shooterMaterial;
        if (rendererComponent != null)
        {
            rendererComponent.sharedMaterial = material;
        }

        if (extraRenderer != null)
        {
            extraRenderer.sharedMaterial = material;
        }

        ApplyPropertyBlock(isQuestion ? 0f : (ShouldShowOutline() ? openOutlineWidth : 0f));
    }

    public void SetClickable(bool clickable)
    {
        if (isClickable == clickable)
        {
            return;
        }

        isClickable = clickable;
        SetOutline(clickable);
    }

    private bool ShouldShowOutline()
    {
        return keepOutlineOpen || isClickable;
    }

    public void SetTunnelShooter(bool tunnelShooter) => isTunnelShooter = tunnelShooter;

    public void SetCoordinate(Vector2Int value) => coordinate = value;

    public void SetOutline(bool open)
    {
        if (keepOutlineOpen)
        {
            open = true;
        }

        if (outlineStateReady && outlineIsOpen == open)
        {
            return;
        }

        ApplyPropertyBlock(open ? openOutlineWidth : 0f);
    }

    public void ClickableProcess()
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
        if (isBusy || !isClickable || slotManager.IsHalted)
            return;

        if (audioPool != null)
            audioPool.PlayShooterSelect();

        if (isWaitingInSlot && mySlot != null)
        {
            ContinueFromSlot();
            return;
        }

        SendFromGrid();
    }

    public void SendFromGrid()
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

    public void ArmAutoContinue()
    {
        continueWhenSeated = true;
    }

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

        isWaitingInSlot = false;
        keepOutlineOpen = true;
        SetClickable(false);
        isBusy = true;
        ShooterController shooterController = GetComponent<ShooterController>();
        if (shooterController != null)
            shooterController.TryStartRun(mySlot, color);
    }

    public void ReturnToSlot(Slot slot)
    {
        mySlot = slot;
        isBusy = true;
        isWaitingInSlot = false;
        SetClickable(false);

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
                ShooterController parkedController = GetComponent<ShooterController>();
                if (parkedController != null)
                    parkedController.RestoreCountText();
                ParkInSlot(slot);
            },
            onUpdate: progress =>
            {
                transform.rotation = Quaternion.Slerp(startRotation, slotRotation, progress);
            });
    }

    public void DepartFromSlot(Slot slot)
    {
        mySlot = slot;
        isBusy = true;

        ShooterController shooterController = GetComponent<ShooterController>();
        if (shooterController != null && shooterController.TryBeginDeparture(slot, color))
        {
            tunnelManager.ControlTunnel();
            return;
        }

        PlacementSlotAnimation(slot);
    }

    public void PlacementSlotAnimation(Slot slot)
    {
        mySlot = slot;
        isBusy = true;

        SplineExtensions.ClearExistingFollowers(gameObject, objectPool);

        if (cachedTransform == null)
            cachedTransform = transform;

        Vector3 slotPos = mySlot.transform.position;
        Quaternion startRotation = cachedTransform != null ? cachedTransform.rotation : transform.rotation;
        Quaternion slotRotation = Quaternion.identity;

        transform.gameObject.FollowTo(
            objectPool,
            mySlot.transform,
            SlotTravelSpeed,
            onComplete: () =>
            {
                if (cachedTransform == null)
                    return;

                cachedTransform.position = slotPos;
                cachedTransform.rotation = slotRotation;
                ShooterController parkedController = GetComponent<ShooterController>();
                if (parkedController != null)
                    parkedController.RestoreCountText();

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
                    ShooterController shooterController = GetComponent<ShooterController>();
                    if (shooterController != null)
                        shooterController.TryStartRun(mySlot, color);
                });
            },
            onUpdate: progress =>
            {
                transform.rotation = Quaternion.Slerp(startRotation, slotRotation, progress);
            });
    }

    private void RestorePlacementScales(Vector3 shooterScale, Transform slotTransform, Vector3 slotScale)
    {
        if (cachedTransform != null)
            cachedTransform.localScale = shooterScale;

        if (slotTransform != null)
            slotTransform.localScale = slotScale;
    }

    public void BindToSlotImmediate(Slot slot)
    {
        mySlot = slot;
        isBusy = false;
        SplineExtensions.ClearExistingFollowers(gameObject, objectPool);

        if (slot != null)
        {
            cachedTransform.position = slot.transform.position;
            cachedTransform.rotation = Quaternion.identity;
            ShooterController parkedController = GetComponent<ShooterController>();
            if (parkedController != null)
                parkedController.RestoreCountText();
        }
    }

    public void ClearBusy()
    {
        isBusy = false;
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
        ShooterController controller = GetComponent<ShooterController>();
        if (controller != null)
            controller.SetCountTextVisible(false);

        if (cachedTransform == null || !homePoseCached)
            return;

        cachedTransform.localScale = homeLocalScale * LockedScaleFactor;
    }

    private void ApplyOpenLook()
    {
        RestoreHomePose();
        RevealQuestionIfNeeded();
        SetOutline(true);
        ShooterController controller = GetComponent<ShooterController>();
        if (controller != null)
            controller.SetCountTextVisible(true);
    }

    private void PlayUnlockAnimation()
    {
        RevealQuestionIfNeeded();
        SetOutline(true);
        ShooterController controller = GetComponent<ShooterController>();
        if (controller != null)
            controller.SetCountTextVisible(true);

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
        questionMark.gameObject.SetActive(false);
        SetMaterial(false);
    }

    private void CacheOpenOutlineWidth()
    {
        if (shooterMaterial != null && shooterMaterial.HasProperty(OutlineWidthId))
        {
            openOutlineWidth = shooterMaterial.GetFloat(OutlineWidthId);
        }
    }

    private void ApplyPropertyBlock(float width)
    {
        if (propertyBlock == null)
        {
            return;
        }

        outlineStateReady = true;
        outlineIsOpen = width > 0f;
        propertyBlock.Clear();
        if (!isQuestionShooter)
        {
            propertyBlock.SetColor(BaseColorId, shooterColor);
            propertyBlock.SetColor(HighlightColorId, UnityEngine.Color.white);
        }

        propertyBlock.SetFloat(OutlineWidthId, width);
        if (rendererComponent != null)
        {
            rendererComponent.SetPropertyBlock(propertyBlock);
        }

        if (extraRenderer != null)
        {
            extraRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    public class Factory : PlaceholderFactory<Shooter>
    {
    }
}
