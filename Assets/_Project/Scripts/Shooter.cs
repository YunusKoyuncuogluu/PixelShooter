using System.Collections.Generic;
using DG.Tweening;
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
    [Inject] private AudioPool audioPool;
    [InjectOptional] private RopeWrap.Game.IFastForwardGate fastForwardGate;

    [FormerlySerializedAs("m_Renderer")]
    [SerializeField] private Renderer rendererComponent;
    [FormerlySerializedAs("m_QuestionMaterial")]
    [SerializeField] private Material questionMaterial;
    [FormerlySerializedAs("m_QuestionDarkMaterial")]
    [SerializeField] private Material questionDarkMaterial;
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
    private Material[] shooterMaterials;
    private Material[] questionMaterials;
    private Transform cachedTransform;
    private MaterialPropertyBlock propertyBlock;
    private Color shooterColor;

    private Sequence scaleSequence;
    private bool isBusy;
    public bool IsBusy => isBusy;

    private bool isWaitingInSlot;
    public bool IsWaitingInSlot => isWaitingInSlot;

    private bool continueWhenSeated;

    public void Construct(ShooterData data)
    {
        cachedTransform = transform;
        shooterData = data;
        cachedTransform.position = shooterData.Position;
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
        shooterMaterials = originalMaterials;
        shooterColor = facadeProperties[(int)shooterData.Color - 1].Color;
        questionMaterials = new[] { questionMaterial, questionDarkMaterial };
        SetMaterial(isQuestionShooter);
    }

    public void ApplyPaletteSwatch(Color swatch)
    {
        shooterColor = swatch;
        if (!isQuestionShooter)
            ApplyPropertyBlock(isClickable ? 1f : 0f);
    }

    public void SetMaterial(bool isQuestion)
    {
        rendererComponent.sharedMaterials = isQuestion ? questionMaterials : shooterMaterials;
        ApplyPropertyBlock(isQuestion ? 0f : (isClickable ? 1f : 0f));
    }

    public void SetClickable(bool clickable) => isClickable = clickable;

    public void SetTunnelShooter(bool tunnelShooter) => isTunnelShooter = tunnelShooter;

    public void SetCoordinate(Vector2Int value) => coordinate = value;

    public void SetOutline(bool open) => ApplyPropertyBlock(open ? 1f : 0f);

    public void ClickableProcess()
    {
        SetOutline(IsClickable);
        if (!isClickable)
            return;

        if (isQuestionShooter)
        {
            isQuestionShooter = false;
            questionMark.gameObject.SetActive(false);
            SetMaterial(false);
            ShooterController shooterController = GetComponent<ShooterController>();
            if (shooterController != null)
                shooterController.SetCountTextVisible(true);
        }
    }

    public void OnClick()
    {
        if (isBusy || !isClickable)
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
        if (isBusy || !isClickable || isWaitingInSlot)
            return;

        isClickable = false;
        isBusy = true;
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
        isClickable = true;
        SetOutline(true);

        if (!continueWhenSeated)
            return;

        continueWhenSeated = false;
        if (fastForwardGate != null && !fastForwardGate.IsActive)
            return;

        ContinueFromSlot();
    }

    public void ContinueFromSlot()
    {
        if (mySlot == null)
            return;

        isWaitingInSlot = false;
        isClickable = false;
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
        isClickable = false;

        if (cachedTransform == null)
            cachedTransform = transform;

        SplineExtensions.ClearExistingFollowers(gameObject);

        Vector3 slotPos = slot.transform.position;
        Quaternion startRotation = cachedTransform.rotation;
        Quaternion slotRotation = Quaternion.identity;

        gameObject.FollowTo(
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

        SplineExtensions.ClearExistingFollowers(gameObject);

        if (cachedTransform == null)
            cachedTransform = transform;

        Vector3 slotPos = mySlot.transform.position;
        Quaternion startRotation = cachedTransform != null ? cachedTransform.rotation : transform.rotation;
        Quaternion slotRotation = Quaternion.identity;

        transform.gameObject.FollowTo(
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
        SplineExtensions.ClearExistingFollowers(gameObject);

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

    private void ApplyPropertyBlock(float outlineWidth)
    {
        if (propertyBlock == null || rendererComponent == null)
            return;

        propertyBlock.Clear();
        if (!isQuestionShooter)
        {
            propertyBlock.SetColor(BaseColorId, shooterColor);
            propertyBlock.SetColor(HighlightColorId, UnityEngine.Color.white);
        }

        propertyBlock.SetFloat(OutlineWidthId, outlineWidth);
        rendererComponent.SetPropertyBlock(propertyBlock);
    }

    public class Factory : PlaceholderFactory<Shooter>
    {
    }
}
