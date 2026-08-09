using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Zenject;

public class Thread : MonoBehaviour
{
    private readonly Vector3 slotEuler = new Vector3(90f, 0f, 90f);

    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private SlotManager m_SlotManager;
    [Inject] private TunnelManager m_TunnelManager;

    [SerializeField] private Renderer m_Renderer;
    [SerializeField] private Material m_QuestionMaterial;
    [SerializeField] private Material m_QuestionDarkMaterial;
    [SerializeField] private GameObject m_QuestionMark;

    [Header("Motion Speeds")]
    [Tooltip("Grid -> slot FollowTo hizi.")]
    [SerializeField] private float slotTravelSpeed = 25f;
    [Tooltip("Slot/grid -> plate seat FollowTo hizi.")]
    [SerializeField] private float plateTravelSpeed = 20f;
    [Tooltip("Conveyor uzerinde ilerleme hizi.")]
    [SerializeField] private float conveyorSpeed = 5f;

    public float SlotTravelSpeed => Mathf.Max(0.01f, slotTravelSpeed);
    public float PlateTravelSpeed => Mathf.Max(0.01f, plateTravelSpeed);
    public float ConveyorSpeed => Mathf.Max(0.01f, conveyorSpeed);

    private bool m_IsClickable;
    public bool IsClickable => m_IsClickable;

    private bool m_IsQuestionThread;
    public bool IsQuestionThread => m_IsQuestionThread;

    private Vector2Int m_Coordinate;
    public Vector2Int Coordinate => m_Coordinate;

    private bool m_IsTunnelThread;
    public bool IsTunnelThread => m_IsTunnelThread;

    private Slot m_MySlot;
    public Slot MySlot => m_MySlot;

    private GeneralColor m_Color;
    public GeneralColor Color => m_Color;
    private ThreadData m_ThreadData;
    private List<FacadeProperty> m_FacadeProperties;
    private Material[] m_Materials;
    private Material[] m_QuestionMaterials;
    private Transform m_Transform;

    private int m_ColorLevel;
    public int ColorLevel => m_ColorLevel;

    private Sequence scaleSequence;
    private bool m_IsBusy;

    /// <summary> Slot / plate animasyonunda veya kuyrukta. </summary>
    public bool IsBusy => m_IsBusy;

    public void Construct(ThreadData threadData)
    {
        m_Transform = transform;
        m_ThreadData = threadData;
        m_Transform.position = m_ThreadData.Position;
        m_Coordinate = m_ThreadData.Coordinate;
        m_Color = threadData.Color;
        m_ColorLevel = (int)m_Color - 1;
        m_IsQuestionThread = threadData.IsQuestionThread;
        m_QuestionMark.SetActive(m_IsQuestionThread);
        m_IsBusy = false;
        SetStartColor();
    }

    private void SetStartColor()
    {
        m_FacadeProperties = m_ProjectSettings.GetOriginalFacadeProperties();
        Material[] originalMaterials = m_FacadeProperties[(int)m_ThreadData.Color - 1].ThreadMaterials;
        m_Materials = new Material[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
            m_Materials[i] = new Material(originalMaterials[i]);

        m_QuestionMaterial = new Material(m_QuestionMaterial);
        m_QuestionDarkMaterial = new Material(m_QuestionDarkMaterial);
        m_QuestionMaterials = new[] { m_QuestionMaterial, m_QuestionDarkMaterial };

        SetMaterial(m_IsQuestionThread);
    }

    public void SetMaterial(bool isQuestion) =>
        m_Renderer.sharedMaterials = isQuestion ? m_QuestionMaterials : m_Materials;

    public void SetClickable(bool isClickable) => m_IsClickable = isClickable;

    public void SetTunnelThread(bool isTunnelThread) => m_IsTunnelThread = isTunnelThread;

    public void SetCoordinate(Vector2Int coordinate) => m_Coordinate = coordinate;

    public void SetOutline(bool open) => m_Materials[0].SetFloat("_OutlineWidth", open ? 1f : 0f);

    public void ClickableProcess()
    {
        SetOutline(IsClickable);
        if (!m_IsClickable)
            return;

        if (m_IsQuestionThread)
        {
            m_IsQuestionThread = false;
            m_QuestionMark.gameObject.SetActive(m_IsQuestionThread);
            SetMaterial(false);
        }
    }

    public void OnClick()
    {
        if (m_IsBusy || !m_IsClickable)
            return;

        m_IsClickable = false;
        m_IsBusy = true;
        m_ThreadManager.RemoveThread(m_Coordinate);
        m_SlotManager.SaveThread(this);
    }

    /// <summary>
    /// Plate hazirsa dogrudan boarding; degilse slot animasyonu.
    /// </summary>
    public void DepartFromSlot(Slot slot)
    {
        m_MySlot = slot;
        m_IsBusy = true;

        SpoolController spoolController = GetComponent<SpoolController>();
        if (spoolController != null && spoolController.TryBeginDeparture(slot, m_Color))
        {
            m_TunnelManager.ControlTunnel();
            return;
        }

        PlacementSlotAnimation(slot);
    }

    public void PlacementSlotAnimation(Slot slot)
    {
        m_MySlot = slot;
        m_IsBusy = true;

        SplineExtensions.ClearExistingFollowers(gameObject);

        Vector3 slotPos = m_MySlot.transform.position;

        transform.gameObject.FollowTo(
            m_MySlot.transform,
            SlotTravelSpeed,
            onComplete: () =>
            {
                // Tam slot pozisyonu + eski yan oturus.
                m_Transform.position = slotPos;
                m_Transform.eulerAngles = slotEuler;

                scaleSequence?.Kill();
                scaleSequence = DOTween.Sequence();
                scaleSequence.Append(m_Transform.DOScale(Vector3.one * 0.95f, 0.05f)).SetEase(Ease.Linear);
                scaleSequence.Append(m_MySlot.transform.DOScale(Vector3.one * 0.95f, 0.05f)).SetEase(Ease.Linear);
                scaleSequence.Append(m_Transform.DOScale(Vector3.one, 0.05f)).SetEase(Ease.Linear);
                scaleSequence.Append(m_MySlot.transform.DOScale(Vector3.one, 0.05f)).SetEase(Ease.Linear);
                scaleSequence.OnComplete(() =>
                {
                    m_TunnelManager.ControlTunnel();
                    SpoolController spoolController = GetComponent<SpoolController>();
                    if (spoolController != null)
                        spoolController.TryStartUnravel(m_MySlot, m_Color);
                });
            },
            onUpdate: progress =>
            {
                transform.eulerAngles = Vector3.Lerp(Vector3.zero, slotEuler, progress);
            });
    }

    public void BindToSlotImmediate(Slot slot)
    {
        m_MySlot = slot;
        m_IsBusy = false;
        SplineExtensions.ClearExistingFollowers(gameObject);

        if (slot != null)
        {
            m_Transform.position = slot.transform.position;
            m_Transform.eulerAngles = slotEuler;
        }
    }

    public void ClearBusy()
    {
        m_IsBusy = false;
    }

    public class Factory : PlaceholderFactory<Thread>
    {
    }
}
