using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TunnelData
{
    [SerializeField] private List<ThreadData> m_Threads;
    public List<ThreadData> Threads => m_Threads;
}