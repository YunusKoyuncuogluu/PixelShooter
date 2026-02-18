using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;
using Zenject;
using Vector3 = UnityEngine.Vector3;

public class Thread : MonoBehaviour
{
    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private SlotManager m_SlotManager;
    [Inject] private TunnelManager m_TunnelManager;

    [SerializeField] private Renderer m_Renderer;
    [SerializeField] private Material m_QuestionMaterial;
    [SerializeField] private Material m_QuestionDarkMaterial;
    [SerializeField] private GameObject m_QuestionMark;

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
    private List<Vector3> m_Path;
    private float m_Speed;
    private float m_RotateSpeed;

    private int m_ColorLevel;
    public int ColorLevel => m_ColorLevel;

    private Sequence m_scaleSequence;


    public void Construct(ThreadData threadData)
    {
        m_Speed = m_ProjectSettings.ThreadSpeed;
        m_RotateSpeed = m_ProjectSettings.ThreadRotateSpeed;
        m_Transform = transform;
        m_ThreadData = threadData;
        m_Transform.position = m_ThreadData.Position;
        m_Coordinate = m_ThreadData.Coordinate;
        m_Color = threadData.Color;
        m_ColorLevel = (int)m_Color - 1;
        m_IsQuestionThread = threadData.IsQuestionThread;
        m_QuestionMark.SetActive(m_IsQuestionThread);
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

    public void ClickableProcess(List<Vector3> path)
    {
        SetOutline(IsClickable);
        if (!m_IsClickable)
        {
            return;
        }

        if (m_IsQuestionThread)
        {
            m_IsQuestionThread = false;
            m_QuestionMark.gameObject.SetActive(m_IsQuestionThread);
            SetMaterial(false);
        }

        m_Path = new List<Vector3>(path);
    }

    public void OnClick()
    {
        m_IsClickable = false;
        m_ThreadManager.RemoveThread(m_Coordinate);
        m_SlotManager.SaveThread(this);
    }

    public void PlacementSlotAnimation(Slot slot)
    {
        m_MySlot = slot;

        transform.gameObject.FollowTo(m_MySlot.transform, m_Speed,
            onComplete: () =>
            {
                Debug.Log("Spline End");
                m_scaleSequence?.Kill();
                m_scaleSequence = DOTween.Sequence();
                m_scaleSequence.Append(m_Transform.DOScale(Vector3.one * 0.95f, 0.05f)).SetEase(Ease.Linear);
                m_scaleSequence.Append(m_MySlot.transform.DOScale(Vector3.one * 0.95f, 0.05f)).SetEase(Ease.Linear);
                m_scaleSequence.Append(m_Transform.DOScale(Vector3.one, 0.05f)).SetEase(Ease.Linear);
                m_scaleSequence.Append(m_MySlot.transform.DOScale(Vector3.one, 0.05f)).SetEase(Ease.Linear);
                m_scaleSequence.OnComplete(() =>
                {
                    m_TunnelManager.ControlTunnel();
                    var spoolController = GetComponent<SpoolController>();
                    if (spoolController == null)
                    {
                        Debug.LogWarning($"[Thread] SpoolController component not found on Thread GameObject '{gameObject.name}'. Please add SpoolController component to Thread prefab.");
                    }
                    else
                    {
                        spoolController.TryStartUnravel(m_MySlot, m_Color);
                    }
                });
            },
            onUpdate:
            (progress) => { transform.eulerAngles = Vector3.Lerp(Vector3.zero, new Vector3(90f, 0f, 90f), progress); });
    }

    public class Factory : PlaceholderFactory<Thread>
    {
    }
}