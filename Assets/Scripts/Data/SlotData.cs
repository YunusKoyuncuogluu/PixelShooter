using System;
using UnityEngine;

[Serializable]
public class SlotData
{
    [SerializeField] private SlotCount m_Count;
    public SlotCount Count => m_Count;
}

public enum SlotCount
{
    None,
    Five,
    Six,
    Seven
}