using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class SlotData
{
    [FormerlySerializedAs("m_Count")]
    [SerializeField] private SlotCount count;
    [FormerlySerializedAs("m_LockedByIndex")]
    [SerializeField] private List<bool> lockedByIndex;

    public SlotCount Count => count;

    public IReadOnlyList<bool> LockedByIndex => lockedByIndex;
}

public enum SlotCount
{
    None,
    Five,
    Six,
    Seven
}
