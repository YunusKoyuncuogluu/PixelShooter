using System;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using Zenject;

public class SlotManager : MonoBehaviour
{
    /// <summary> Tum slotlar dolu ve hicbir makara islem yapamiyorsa tetiklenir. </summary>
    public event Action OnSpoolAreaStuck;

    [Inject] private Slot.Factory m_SlotFactory;
    [SerializeField] private float m_SlotSpaceX;

    private SlotData m_SlotData;
    public SlotData SlotData => m_SlotData;

    private SlotCount m_SlotCount;
    public SlotCount SlotCount => m_SlotCount;

    private List<Slot> m_Slots;
    public List<Slot> Slots => m_Slots;

    private List<Thread> m_Threads;
    private List<Thread> m_WaitingThreads;

    /// <summary> Slottaki makara sayisi (level complete ancak 0 oldugunda). </summary>
    public int SpoolCount => m_Threads?.Count ?? 0;

    public void Construct(LevelData levelData)
    {
        m_WaitingThreads = new List<Thread>();
        m_Threads = new List<Thread>();
        m_SlotData = levelData.SlotData;
        m_SlotCount = m_SlotData.Count;
        CreateSlot();
    }

    private void Update()
    {
        if (m_WaitingThreads.Count > 0)
        {
            bool placed = PlacementThread(m_WaitingThreads[0]);
            if (placed)
                m_WaitingThreads.RemoveAt(0);
        }
    }

    private const int MaxSlotCount = 7;

    private int GetSlotCountFromEnum(SlotCount count)
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
        }

        m_Slots = new List<Slot>(FindNoneLockedSlots());
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

    /// <summary> Bos (unlocked + empty) slot. </summary>
    public Slot GetAvailableSlot()
    {
        if (m_Slots == null)
            return null;

        for (int i = 0; i < m_Slots.Count; i++)
        {
            Slot slot = m_Slots[i];
            if (slot != null && !slot.IsLocked && !slot.IsFull)
                return slot;
        }

        return null;
    }

    /// <summary> Thread'i bos bir slota yerlestirir. Slot yoksa kuyrukta kalir (false doner). </summary>
    private bool PlacementThread(Thread thread)
    {
        Slot slot = GetAvailableSlot();

        if (slot == null)
            return false;

        if (m_Threads.Count < m_Slots.Count)
            m_Threads.Add(thread);
        slot.SetFull(true);
        thread.DepartFromSlot(slot);
        return true;
    }

    public void SaveThread(Thread thread)
    {
        if (thread == null)
            return;

        if (m_WaitingThreads.Contains(thread))
            return;

        if (m_Threads != null && m_Threads.Contains(thread))
            return;

        m_WaitingThreads.Add(thread);
    }

    /// <summary> Makara dolup yok oldugunda listeden cikarilir. </summary>
    public void RemoveThread(Thread thread)
    {
        if (thread != null && m_Threads != null)
            m_Threads.Remove(thread);
    }

    /// <summary> Tum acik (unlocked) slotlar dolu mu? </summary>
    public bool IsAreaFull
    {
        get
        {
            if (m_Slots == null || m_Slots.Count == 0)
                return true;

            for (int i = 0; i < m_Slots.Count; i++)
            {
                Slot slot = m_Slots[i];
                if (slot != null && !slot.IsLocked && !slot.IsFull)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Conveyor'dan donen makarayi dogrudan slota yerlestirir (kuyruk / animasyon yok).
    /// </summary>
    public void ForcePlaceThread(Thread thread, Slot slot)
    {
        if (thread == null || slot == null)
            return;

        if (m_Threads != null && !m_Threads.Contains(thread))
            m_Threads.Add(thread);

        slot.SetFull(true);
        thread.BindToSlotImmediate(slot);
    }

    /// <summary> Level fail: slot alani kilitli / stuck. </summary>
    public void NotifySpoolAreaStuck()
    {
        OnSpoolAreaStuck?.Invoke();
    }
}
