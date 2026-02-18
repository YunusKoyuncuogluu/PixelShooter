using System;
using System.Collections;
using System.Collections.Generic;
using LevelManagement;
using RopeWrap.Game;
using UnityEngine;
using Zenject;

public class SlotManager : MonoBehaviour
{
    /// <summary> Tüm slotlar dolu ve hiçbir makara söküm yapamıyorsa (hepsinin üstündeki renk fabric altında yok) tetiklenir. </summary>
    public event Action OnSpoolAreaStuck;
    [Inject] private LevelManager m_LevelManager;
    [InjectOptional] private FabricManager m_FabricManager;
    [InjectOptional] private LevelFlowController m_LevelFlowController;

    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private Slot.Factory m_SlotFactory;
    [SerializeField] private float m_SlotSpaceX;

    private SlotState m_SlotState;
    public SlotState SlotState => m_SlotState;

    private SlotData m_SlotData;
    public SlotData SlotData => m_SlotData;

    private SlotCount m_SlotCount;
    public SlotCount SlotCount => m_SlotCount;

    private List<Slot> m_Slots;
    public List<Slot> Slots => m_Slots;

    private List<Thread> m_Threads;
    private List<Thread> m_WaitingThreads;

    /// <summary> Slottaki makara sayısı (ip bitince hepsi yok olmalı; level complete ancak 0 olduğunda). </summary>
    public int SpoolCount => m_Threads?.Count ?? 0;

    [Tooltip("En alt satırla eşleşen makaraları bu sıklıkta kontrol eder (saniye).")]
    [SerializeField] private float m_SpoolMatchCheckInterval = 0.25f;
    private float m_NextSpoolMatchCheckTime;

    public void Construct(LevelData levelData)
    {
        m_WaitingThreads = new();
        m_Threads = new();
        m_SlotState = SlotState.Available;
        m_SlotData = levelData.SlotData;
        m_SlotCount = m_SlotData.Count;
        CreateSlot();
        if (m_FabricManager != null)
            m_FabricManager.OnFabricBottomRowChanged += TryStartUnravelForAllSlottedSpools;
    }

    private void OnDestroy()
    {
        if (m_FabricManager != null)
            m_FabricManager.OnFabricBottomRowChanged -= TryStartUnravelForAllSlottedSpools;
    }

    /// <summary> En alt satır değişince slotlardaki makaraları kontrol eder; rengi altta varsa otomatik sarma başlatır. Aynı renkte birden fazla makara varsa önce progress'i yüksek olan sarılır (önce %100 olup çıksın). </summary>
    private void TryStartUnravelForAllSlottedSpools()
    {
        if (m_Threads == null || m_FabricManager == null) return;
        var withSpool = new List<(Thread thread, SpoolController spool)>();
        foreach (Thread t in m_Threads)
        {
            if (t == null || t.MySlot == null) continue;
            var spool = t.GetComponent<SpoolController>();
            if (spool != null)
                withSpool.Add((t, spool));
        }
        withSpool.Sort((a, b) => b.spool.FillCount.CompareTo(a.spool.FillCount));
        foreach (var pair in withSpool)
            pair.spool.TryStartUnravel(pair.thread.MySlot, pair.thread.Color);
        CheckSpoolAreaStuck();
    }

    private void Update()
    {
        if (m_WaitingThreads.Count > 0)
        {
            bool placed = PlacementThread(m_WaitingThreads[0]);
            if (placed)
                m_WaitingThreads.RemoveAt(0);
        }

        if (m_FabricManager != null && m_Threads != null && m_Threads.Count > 0 && Time.time >= m_NextSpoolMatchCheckTime)
        {
            m_NextSpoolMatchCheckTime = Time.time + m_SpoolMatchCheckInterval;
            TryStartUnravelForAllSlottedSpools();
        }
    }

    private const int MaxSlotCount = 7;

    private static int GetSlotCountFromEnum(SlotCount count)
    {
        return count switch
        {
            SlotCount.Five => 5,
            SlotCount.Six => 6,
            SlotCount.Seven => 7,
            _ => MaxSlotCount
        };
    }

    public void CreateSlot()
    {
        m_Slots = new List<Slot>();
        int slotCount = GetSlotCountFromEnum(m_SlotCount);

        for (int i = 0; i < slotCount; i++)
        {
            Slot slot = m_SlotFactory.Create();
            slot.transform.SetParent(transform);
            m_Slots.Add(slot);
        }

        SelectLockedSlots();

        List<Vector3> localPositions = CalculateLocalPositions();

        for (int i = 0; i < m_Slots.Count; i++)
        {
            Slot slot = m_Slots[i];
            slot.Construct(localPositions[i], i);
            Debug.Log($"[SlotManager] Created slot {i} at position {localPositions[i]}, ColumnIndex: {i}, Locked: {slot.IsLocked}");
        }

        m_Slots = new List<Slot>(FindNoneLockedSlots());
        Debug.Log($"[SlotManager] After filtering locked slots, {m_Slots.Count} available slots remain");
    }

    private List<Slot> FindNoneLockedSlots()
    {
        List<Slot> availableSlots = new List<Slot>();
        foreach (Slot slot in m_Slots)
        {
            if (!slot.IsLocked)
                availableSlots.Add(slot);
        }

        return availableSlots;
    }

    private List<Vector3> CalculateLocalPositions()
    {
        List<Vector3> localPositions = new List<Vector3>();

        int slotCount = m_Slots?.Count ?? GetSlotCountFromEnum(m_SlotCount);
        float totalWidth = (slotCount - 1) * m_SlotSpaceX;
        float startX = -totalWidth / 2;

        for (int i = 0; i < slotCount; i++)
        {
            Vector3 localPosition = new Vector3(startX + i * m_SlotSpaceX, 0, 0);
            localPositions.Add(localPosition);
        }

        return localPositions;
    }

    private void SelectLockedSlots()
    {
        if (m_SlotData?.LockedByIndex != null && m_SlotData.LockedByIndex.Count == m_Slots.Count)
        {
            for (int i = 0; i < m_Slots.Count; i++)
                m_Slots[i].SetLocked(m_SlotData.LockedByIndex[i]);
            return;
        }
        switch (m_SlotCount)
        {
            case SlotCount.Five:
                m_Slots[0].SetLocked(true);
                m_Slots[^1].SetLocked(true);
                break;
            case SlotCount.Six:
                m_Slots[0].SetLocked(true);
                break;
            case SlotCount.Seven:
                break;
            default:
                break;
        }
    }

    private Slot GetAvailableSlot()
    {
        Slot slot = m_Slots.Find(s => s.IsLocked == false && s.IsFull == false);
        return slot;
    }

    /// <summary> Thread'i boş bir slota yerleştirir. Slot yoksa kuyrukta kalır (false döner). </summary>
    private bool PlacementThread(Thread thread)
    {
        Slot slot = GetAvailableSlot();

        if (slot == null)
            return false;

        if (m_Threads.Count < m_Slots.Count)
            m_Threads.Add(thread);
        thread.PlacementSlotAnimation(slot);
        slot.SetFull(true);
        CheckSpoolAreaStuck();
        return true;
    }

    /// <summary> Tüm slotlar dolu, hiçbir makara şu an sarmıyor ve hiçbir makaranın rengi fabric altında yoksa Fail (OnSpoolAreaStuck) tetiklenir. </summary>
    private void CheckSpoolAreaStuck()
    {
        if (m_Slots == null || m_Threads == null || m_FabricManager == null) return;
        bool allFull = true;
        foreach (Slot s in m_Slots)
        {
            if (!s.IsFull) { allFull = false; break; }
        }
        if (!allFull) return;

        foreach (Thread t in m_Threads)
        {
            if (t == null) continue;
            var spool = t.GetComponent<SpoolController>();
            if (spool != null && spool.IsWinding)
                return;
        }

        foreach (Thread t in m_Threads)
        {
            if (t == null || t.MySlot == null || !t.MySlot.IsFull) continue;
            if (m_FabricManager.GetColumnWithBottomColor(t.Color) >= 0)
                return;
        }
        OnSpoolAreaStuck?.Invoke();
    }

    public void SaveThread(Thread thread)
    {
        m_WaitingThreads.Add(thread);
    }

    /// <summary> Makara dolup yok olduğunda listeden çıkarılır. </summary>
    public void RemoveThread(Thread thread)
    {
        if (thread != null && m_Threads != null)
            m_Threads.Remove(thread);
    }
}

public enum SlotState
{
    None,
    Available,
    Busy
}