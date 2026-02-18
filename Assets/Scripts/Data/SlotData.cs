using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SlotData
{
    [SerializeField] private SlotCount m_Count;
    [SerializeField] private List<bool> m_LockedByIndex;

    public SlotCount Count => m_Count;

    /// <summary> İndeks bazlı kilit: m_LockedByIndex[i] == true ise i. slot kilitli. Null veya uyumsuz uzunlukta ise varsayılan zorluk kuralı kullanılır. </summary>
    public IReadOnlyList<bool> LockedByIndex => m_LockedByIndex;
}

public enum SlotCount
{
    None,
    Five,
    Six,
    Seven
}