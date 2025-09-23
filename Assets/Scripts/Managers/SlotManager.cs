using System;
using System.Collections;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using Zenject;

public class SlotManager : MonoBehaviour
{
    [Inject] private LevelManager m_LevelManager;

    //PlayerController
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

    public void Construct(LevelData levelData)
    {
        m_WaitingThreads = new();
        m_Threads = new();
        m_SlotState = SlotState.Available;
        m_SlotData = levelData.SlotData;
        m_SlotCount = m_SlotData.Count;
        CreateSlot();
    }

    private void Update()
    {
        if (m_WaitingThreads.Count > 0)
        {
            PlacementThread(m_WaitingThreads[0]);
            m_WaitingThreads.RemoveAt(0);
        }
    }

    public void CreateSlot()
    {
        m_Slots = new List<Slot>();

        for (int i = 0; i < 7; i++)
        {
            Slot slot = m_SlotFactory.Create();
            slot.transform.SetParent(transform);
            m_Slots.Add(slot);
        }

        SelectLocedSlots();

        List<Vector3> localPositions = CalculateLocalPositions();

        for (int i = 0; i < m_Slots.Count; i++)
        {
            Slot slot = m_Slots[i];
            slot.Construct(localPositions[i]);
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

        int slotCount = 7;
        float totalWidth = (slotCount - 1) * m_SlotSpaceX;
        float startX = -totalWidth / 2;

        for (int i = 0; i < slotCount; i++)
        {
            Vector3 localPosition = new Vector3(startX + i * m_SlotSpaceX, 0, 0);
            localPositions.Add(localPosition);
        }

        return localPositions;
    }

    private void SelectLocedSlots()
    {
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

    private void PlacementThread(Thread thread)
    {
        Slot slot = GetAvailableSlot();

        if (slot != null)
        {
            if (m_Threads.Count < m_Slots.Count)
                m_Threads.Add(thread);
            thread.PlacementSlotAnimation(slot);
            slot.SetFull(true);
        }
    }

    public void SaveThread(Thread thread)
    {
        m_WaitingThreads.Add(thread);
    }
}

public enum SlotState
{
    None,
    Available,
    Busy
}