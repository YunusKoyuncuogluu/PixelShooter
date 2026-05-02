using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;
using Zenject;

public class SpoolController : MonoBehaviour
{
    private const int k_NodesPerSpool = 3;

    [Inject] private FabricManager m_FabricManager;
    [Inject] private SlotManager m_SlotManager;
    [Inject] private ProjectSettings m_ProjectSettings;

    [Header("Unravel")]
    [SerializeField] private Material m_ThreadMaterial;
    [SerializeField] [Min(0.3f)] private float m_WindingVisibleDuration = 2.2f;
    [SerializeField] [Range(0.5f, 4f)] private float m_WaveSpeed = 1.6f;
    [SerializeField] [Range(0.1f, 1f)] private float m_WaveAmplitude = 0.52f;

    [Header("Fill UI")]
    [SerializeField] private SpoolFillUI m_SpoolFillUI;

    [Header("Disappear")]
    [SerializeField] private float m_DisappearDuration = 0.35f;

    private GameObject m_SplinesRoot;
    private readonly List<SplineComputer> m_SplineComputers = new List<SplineComputer>();
    private readonly List<Vector3> m_BaseMidPoints = new List<Vector3>();
    private readonly List<Vector3> m_InitialEndPositions = new List<Vector3>();
    private readonly List<StitchNode> m_TargetNodes = new List<StitchNode>();
    private float m_WindingStartTime = -1f;
    private CancellationTokenSource m_WindingCts;
    private Slot m_CurrentUnravelSlot;
    private int m_FillCount;
    /// <summary> 0..k_NodesPerSpool. Sıralama için: yüksek progress önce sarılsın ki makara çabuk %100 olsun. </summary>
    public int FillCount => m_FillCount;
    private bool m_UnravelInProgress;
    public bool IsWinding => m_UnravelInProgress;
    private SpoolFillUI m_CachedFillUI;

    /// <summary>
    /// Slotlardaki makaralar sürekli bunu çağırır (SlotManager periyodik + OnFabricBottomRowChanged).
    /// En altta rengimle eşleşen ip varsa hemen sarar (1–3 sütun aynı anda). isRetry sadece kendi devam turumuz için.
    /// </summary>
    public void TryStartUnravel(Slot slot, GeneralColor spoolColor, bool isRetry = false)
    {
        if (slot == null || m_FabricManager == null)
            return;
        if (m_UnravelInProgress)
            return;

        int maxToTake = k_NodesPerSpool - m_FillCount;
        if (maxToTake <= 0)
            return;

        List<int> columns = m_FabricManager.GetColumnsWithColorAtBottomRow(spoolColor, maxToTake);
        if (columns == null || columns.Count == 0)
            return;

        m_UnravelInProgress = true;
        m_CurrentUnravelSlot = slot;
        // Sadece tamamen boş makarada (yeni oturum) doluluk ve UI sıfırlanır. Kısmen doluysa sayacı koruyoruz.
        if (m_FillCount == 0)
        {
            ShowFillUI(true);
            m_CachedFillUI = GetFillUI();
            if (m_CachedFillUI != null)
                m_CachedFillUI.SetTargetPercent(0f);
        }

        m_WindingCts?.Cancel();
        m_WindingCts = new CancellationTokenSource();
        StartWindingAsync(spoolColor, m_WindingCts.Token).Forget();
    }

    private async UniTaskVoid StartWindingAsync(GeneralColor spoolColor, CancellationToken ct)
    {
        if (m_FabricManager == null)
            return;

        int maxToTake = k_NodesPerSpool - m_FillCount;
        if (maxToTake <= 0) return;

        List<int> columns = m_FabricManager.GetColumnsWithColorAtBottomRow(spoolColor, maxToTake);
        if (columns == null || columns.Count == 0)
            return;

        var targetNodes = new List<StitchNode>();
        foreach (int col in columns)
        {
            StitchNode node = m_FabricManager.GetBottomNode(col);
            if (node != null && node.Color == spoolColor)
                targetNodes.Add(node);
        }
        if (targetNodes.Count == 0)
            return;

        try
        {
            foreach (int col in columns)
                m_FabricManager.NotifyUnravelStarted(col);

            CreateSplinesTo(targetNodes);

            int maxPieces = 10;
            foreach (var n in targetNodes)
            {
                if (n != null && n.TotalPieces > maxPieces)
                    maxPieces = n.TotalPieces;
            }
            float pieceDuration = m_ProjectSettings != null ? m_ProjectSettings.PieceConsumeDuration : 0.2f;
            float consumptionDuration = Mathf.Max(0.3f, maxPieces * pieceDuration);
            float targetPercent = ((m_FillCount + columns.Count) / (float)k_NodesPerSpool) * 100f;
            UpdateFillUI(targetPercent, consumptionDuration);

            var tasks = new List<UniTask<StitchNode>>();
            foreach (int col in columns)
                tasks.Add(m_FabricManager.ConsumeBottomNodeAsync(col));
            await UniTask.WhenAll(tasks);

            foreach (int col in columns)
                m_FabricManager.NotifyUnravelEnded(col);

            m_FillCount += columns.Count;

            if (m_FillCount >= k_NodesPerSpool)
            {
                await PlayDisappearAndDestroy();
                return;
            }
        }
        finally
        {
            if (m_FillCount < k_NodesPerSpool)
            {
                Slot slotToRetry = m_CurrentUnravelSlot;
                m_UnravelInProgress = false;
                DestroySplines();
                m_CurrentUnravelSlot = null;
                if (slotToRetry != null)
                    TryStartUnravel(slotToRetry, spoolColor, isRetry: true);
            }
        }
    }

    private SpoolFillUI GetFillUI()
    {
        if (m_CachedFillUI != null) return m_CachedFillUI;
        return m_SpoolFillUI != null ? m_SpoolFillUI : GetComponentInChildren<SpoolFillUI>(true);
    }

    private void ShowFillUI(bool show)
    {
        var ui = m_SpoolFillUI != null ? m_SpoolFillUI : GetComponentInChildren<SpoolFillUI>(true);
        if (ui != null)
        {
            ui.gameObject.SetActive(show);
            if (show) m_CachedFillUI = ui;
        }
    }

    private void UpdateFillUI(float targetPercent, float durationOverride = -1f)
    {
        var ui = GetFillUI();
        if (ui != null)
            ui.SetTargetPercent(targetPercent, durationOverride);
    }

    private async Cysharp.Threading.Tasks.UniTask PlayDisappearAndDestroy()
    {
        ShowFillUI(false);
        DestroySplines();

        Thread threadRef = GetComponent<Thread>();
        Slot slotToFree = m_CurrentUnravelSlot;
        SlotManager slotManagerRef = m_SlotManager;
        GameObject go = gameObject;

        float duration = Mathf.Max(0.01f, m_DisappearDuration);
        Tween disappearTween = transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);
        await disappearTween.AsyncWaitForCompletion().AsUniTask();

        if (slotManagerRef != null && threadRef != null)
            slotManagerRef.RemoveThread(threadRef);
        if (slotToFree != null)
            slotToFree.SetFull(false);

        if (go != null)
            Destroy(go);
    }

    private void CreateSplinesTo(List<StitchNode> targetNodes)
    {
        DestroySplines();
        if (targetNodes == null || targetNodes.Count == 0) return;

        m_TargetNodes.Clear();
        m_TargetNodes.AddRange(targetNodes);

        m_SplinesRoot = new GameObject("UnravelSplines");
        m_SplinesRoot.transform.SetParent(transform);

        Vector3 spoolPos = transform.position;
        for (int i = 0; i < targetNodes.Count; i++)
        {
            StitchNode target = targetNodes[i];
            if (target == null) continue;

            Vector3 endPos = target.transform.position;
            Vector3 mid = (spoolPos + endPos) * 0.5f;
            Vector3 dir = (endPos - spoolPos).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(dir, Vector3.forward).normalized;
            mid += perp * 0.2f;

            GameObject go = new GameObject("ThreadSpline_" + i);
            go.transform.SetParent(m_SplinesRoot.transform);

            var splineComputer = go.AddComponent<SplineComputer>();
            splineComputer.space = SplineComputer.Space.World;
            splineComputer.type = Spline.Type.BSpline;
            var points = new SplinePoint[3];
            points[0] = new SplinePoint(spoolPos);
            points[1] = new SplinePoint(mid);
            points[2] = new SplinePoint(endPos);
            splineComputer.SetPoints(points);

            var tube = go.AddComponent<TubeGenerator>();
            tube.spline = splineComputer;
            tube.size = 0.055f;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null && m_ThreadMaterial != null)
                meshRenderer.sharedMaterial = m_ThreadMaterial;

            m_SplineComputers.Add(splineComputer);
            m_BaseMidPoints.Add(mid);
            m_InitialEndPositions.Add(endPos);
        }
        m_WindingStartTime = Time.time;
    }

    private void LateUpdate()
    {
        if (m_SplineComputers.Count == 0 || m_WindingStartTime < 0f) return;

        Vector3 spoolPos = transform.position;
        float pieceDuration = m_ProjectSettings != null ? m_ProjectSettings.PieceConsumeDuration : 0.2f;
        float wave = Mathf.Sin(Time.time * m_WaveSpeed) * m_WaveAmplitude;

        for (int i = 0; i < m_SplineComputers.Count; i++)
        {
            var comp = m_SplineComputers[i];
            if (comp == null) continue;
            if (i >= m_InitialEndPositions.Count) continue;

            SplinePoint[] points = comp.GetPoints(SplineComputer.Space.World);
            if (points == null || points.Length < 3) continue;

            Vector3 start = spoolPos;
            Vector3 end = (i < m_TargetNodes.Count && m_TargetNodes[i] != null)
                ? m_TargetNodes[i].GetCurrentPiecePosition()
                : m_InitialEndPositions[i];
            Vector3 dir = (end - start).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(dir, Vector3.forward).normalized;

            float dist = Vector3.Distance(start, end);
            float waveAtMid = Mathf.Sin(Time.time * m_WaveSpeed) * m_WaveAmplitude;
            float waveAtSpool = Mathf.Sin(Time.time * m_WaveSpeed - dist * 2f) * m_WaveAmplitude * 0.5f;

            Vector3 mid = (start + end) * 0.5f + perp * (0.15f + waveAtMid);
            points[0].position = start + perp * waveAtSpool;
            points[1].position = mid;
            points[2].position = end;
            comp.SetPoints(points);
        }
    }

    private void DestroySplines()
    {
        m_SplineComputers.Clear();
        m_BaseMidPoints.Clear();
        m_InitialEndPositions.Clear();
        m_TargetNodes.Clear();
        m_WindingStartTime = -1f;
        if (m_SplinesRoot != null)
        {
            Destroy(m_SplinesRoot);
            m_SplinesRoot = null;
        }
    }

    private void OnDestroy()
    {
        m_WindingCts?.Cancel();
        DestroySplines();
    }
}
