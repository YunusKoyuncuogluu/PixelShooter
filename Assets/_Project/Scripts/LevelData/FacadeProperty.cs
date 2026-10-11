using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class FacadeProperty : ICloneable
{
    [FormerlySerializedAs("m_Level")]
    [SerializeField] private int level;
    [FormerlySerializedAs("m_Name")]
    [SerializeField] private string facadeName;
    [FormerlySerializedAs("m_Color")]
    [SerializeField] private Color color;
    [FormerlySerializedAs("m_ThreadMaterials")]
    [SerializeField] private Material[] shooterMaterials;

    public int Level => level;
    public string Name => facadeName;
    public Color Color => color;
    public Material[] ShooterMaterials => shooterMaterials;

    public object Clone()
    {
        return MemberwiseClone();
    }
}
