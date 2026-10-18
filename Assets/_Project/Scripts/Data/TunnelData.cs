using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class TunnelData
{
    [FormerlySerializedAs("m_Threads")]
    [SerializeField] private List<ShooterData> shooters;
    public List<ShooterData> Shooters => shooters;

    public void InitializeDefault()
    {
        shooters = new List<ShooterData>
        {
            new ShooterData(GeneralColor.Red, false, Vector2Int.zero, Vector3.zero)
        };
    }

    public void EnsureShooters()
    {
        if (shooters == null)
        {
            shooters = new List<ShooterData>();
        }
    }

    public void SetShooters(List<ShooterData> value)
    {
        shooters = value != null ? value : new List<ShooterData>();
    }
}
