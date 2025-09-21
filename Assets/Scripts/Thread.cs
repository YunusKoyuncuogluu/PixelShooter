using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

public class Thread : MonoBehaviour
{
    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private SlotManager m_SlotManager;

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

    private CancellationTokenSource m_mMoveCTS;


    private GeneralColor m_Color;
    private ThreadData m_ThreadData;
    private List<FacadeProperty> m_FacadeProperties;
    private Material[] m_Materials;
    private Material[] m_QuestionMaterials;
    private Transform m_Transform;
    private List<Vector3> m_Path;
    private float m_Speed;
    private int m_ColorLevel;

    public void Construct(ThreadData threadData)
    {
        m_mMoveCTS = new CancellationTokenSource();

        m_Speed = m_ProjectSettings.ThreadSpeed;
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
        m_QuestionMaterials = new[] { m_QuestionMaterial, m_QuestionDarkMaterial, m_Materials[2], m_Materials[3] };

        SetMaterial(m_IsQuestionThread);
    }

    public void SetMaterial(bool isQuestion) =>
        m_Renderer.sharedMaterials = isQuestion ? m_QuestionMaterials : m_Materials;

    public void SetClickable(bool isClickable) => m_IsClickable = isClickable;

    public void SetCoordinate(Vector2Int cordinate) => m_Coordinate = cordinate;

    public void SetOutline(bool open) => m_Materials[0].SetFloat("_OutlineWidth", open ? 5f : 0f);

    public void ClickableProcess(List<Vector3> path, float animationTime)
    {
        if (!m_IsClickable)
        {
            return;
        }

        if (m_IsQuestionThread)
        {
            m_IsQuestionThread = false;
            m_QuestionMark.gameObject.SetActive(false);
            SetMaterial(false);
        }

        m_Path = new List<Vector3>(path);
    }

    public void OnClick()
    {
        m_IsClickable = false;
        m_ThreadManager.RemoveThread(m_Coordinate);
        ExitGrid().Forget();
    }

    private async UniTask ExitGrid()
    {
        bool isArrived = false;
        int pathIndex = 0;

        while (!isArrived)
        {
            if (pathIndex < m_Path.Count)
            {
                Vector3 targetPoint = m_Path[pathIndex];
                //Rotate TO DO
                Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPoint, m_Speed * Time.deltaTime);
                m_Transform.position = nextPosition;

                if ((nextPosition - targetPoint).sqrMagnitude <= 0.02f)
                    pathIndex++;
            }
            else
                isArrived = true;

            await UniTask.Yield(PlayerLoopTiming.Update, m_mMoveCTS.Token);
        }
    }


    public class Factory : PlaceholderFactory<Thread>
    {
    }
}