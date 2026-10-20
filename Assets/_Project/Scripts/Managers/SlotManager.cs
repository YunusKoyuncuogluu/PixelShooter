using System;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class SlotManager : MonoBehaviour
{
    private const int MaxSlotCount = 7;

    public event Action OnShooterAreaStuck;

    [Inject] private Slot.Factory slotFactory;
    [FormerlySerializedAs("m_SlotSpaceX")]
    [SerializeField] private float slotSpaceX;

    private SlotData slotData;
    private SlotCount slotCount;
    private List<Slot> slots;

    private List<Shooter> shooters;
    private List<Shooter> waitingShooters;
    private bool stopped;
    private bool stuckWarning;
    private bool wrongWarningBusy;

    public bool IsHalted => stopped;

    public void Stop() => stopped = true;

    public int ShooterCount => shooters != null ? shooters.Count : 0;
    public int OccupantCount => shooters != null ? shooters.Count : 0;
    public int QueuedCount => waitingShooters != null ? waitingShooters.Count : 0;

    public int CountFreeSlots()
    {
        if (slots == null)
            return 0;

        int free = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot != null && !slot.IsLocked && !slot.IsFull)
                free++;
        }

        int queued = waitingShooters != null ? waitingShooters.Count : 0;
        if (queued >= free)
            return 0;

        return free - queued;
    }

    public Shooter GetOccupant(int index) => shooters[index];

    public Shooter GetQueued(int index) => waitingShooters[index];

    public void Construct(LevelData levelData)
    {
        waitingShooters = new List<Shooter>();
        shooters = new List<Shooter>();
        slotData = levelData.SlotData;
        slotCount = slotData.Count;

        CreateSlot();
    }

    private void Update()
    {
        if (!stopped && waitingShooters != null && waitingShooters.Count > 0)
        {
            bool placed = PlaceShooter(waitingShooters[0]);
            if (placed)
                waitingShooters.RemoveAt(0);
        }

        UpdateWrongWarnings();
    }

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
        slots = new List<Slot>();
        int count = GetSlotCountFromEnum(slotCount);

        for (int i = 0; i < count; i++)
        {
            Slot slot = slotFactory.Create();
            slot.transform.SetParent(transform);
            slots.Add(slot);
        }

        SelectLockedSlots();

        List<Vector3> localPositions = CalculateLocalPositions();

        for (int i = 0; i < slots.Count; i++)
            slots[i].Construct(localPositions[i]);

        slots = new List<Slot>(FindUnlockedSlots());
    }

    private List<Slot> FindUnlockedSlots()
    {
        List<Slot> availableSlots = new List<Slot>();

        foreach (Slot slot in slots)
        {
            if (!slot.IsLocked)
                availableSlots.Add(slot);
        }

        return availableSlots;
    }

    private List<Vector3> CalculateLocalPositions()
    {
        List<Vector3> localPositions = new List<Vector3>();

        int count = slots != null ? slots.Count : GetSlotCountFromEnum(slotCount);
        float totalWidth = (count - 1) * slotSpaceX;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < count; i++)
            localPositions.Add(new Vector3(startX + i * slotSpaceX, 0f, 0f));

        return localPositions;
    }

    private void SelectLockedSlots()
    {
        if (slotData != null && slotData.LockedByIndex != null && slotData.LockedByIndex.Count == slots.Count)
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetLocked(slotData.LockedByIndex[i]);

            return;
        }

        switch (slotCount)
        {
            case SlotCount.Five:

                slots[0].SetLocked(true);
                slots[^1].SetLocked(true);

                break;
            case SlotCount.Six:

                slots[0].SetLocked(true);

                break;
            case SlotCount.Seven:
                break;
        }
    }

    private void UpdateWrongWarnings()
    {
        if (slots == null)
            return;

        bool danger = stuckWarning || (!stopped && slots.Count > 0 && IsAreaFull);
        if (!danger && !wrongWarningBusy)
            return;

        float deltaTime = Time.deltaTime;
        bool busy = false;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot == null)
                continue;

            slot.SetWrongWarning(danger);
            if (!slot.WrongWarningBusy)
                continue;

            slot.TickWrongWarning(deltaTime);
            busy = true;
        }

        wrongWarningBusy = busy;
    }

    public Slot GetAvailableSlot()
    {
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot != null && !slot.IsLocked && !slot.IsFull)
                return slot;
        }

        return null;
    }

    private bool PlaceShooter(Shooter shooter)
    {
        Slot slot = GetAvailableSlot();

        if (slot == null)
            return false;

        if (shooters.Count < slots.Count)
            shooters.Add(shooter);

        slot.SetFull(true);
        shooter.DepartFromSlot(slot);

        return true;
    }

    public void SaveShooter(Shooter shooter)
    {
        if (shooter == null)
            return;

        if (waitingShooters.Contains(shooter))
            return;

        if (shooters != null && shooters.Contains(shooter))
            return;

        waitingShooters.Add(shooter);
    }

    public void RemoveShooter(Shooter shooter)
    {
        if (shooter != null && shooters != null)
            shooters.Remove(shooter);
    }

    public bool IsAreaFull
    {
        get
        {
            if (slots == null || slots.Count == 0)
                return true;

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot != null && !slot.IsLocked && !slot.IsFull)
                    return false;
            }

            return true;
        }
    }

    public void ForcePlaceShooter(Shooter shooter, Slot slot)
    {
        if (shooter == null || slot == null)
            return;

        if (shooters != null && !shooters.Contains(shooter))
            shooters.Add(shooter);

        slot.SetFull(true);
        shooter.ReturnToSlot(slot);
    }

    public void NotifyShooterAreaStuck()
    {
        stuckWarning = true;
        UpdateWrongWarnings();
        OnShooterAreaStuck?.Invoke();
    }
}
