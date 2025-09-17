using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FacadeProperty : ICloneable
{
    [SerializeField] private int m_Level;
    [SerializeField] private string m_Name;
    [SerializeField] private Color m_Color;
    [SerializeField] private Material m_SplashMaterial;
    [SerializeField] private Material[] m_ThreadMaterials;
    [SerializeField] private Material m_KnitMaterials;
    
    public int Level => m_Level;
    public string Name => m_Name;
    public Color Color => m_Color;
    public Material SplashMaterial => m_SplashMaterial;
    public Material[] ThreadMaterials => m_ThreadMaterials;
    public Material KnitMaterials => m_KnitMaterials;

    public static List<FacadeProperty> GetFacadeProperties()
    {
        return ProjectSettings.Load().GetFacadeProperties();
    }

    public static List<FacadeProperty> GetOriginalFacadeProperties()
    {
        return ProjectSettings.Load().GetOriginalFacadeProperties();
    }
    
    public object Clone()
    {
        return this.MemberwiseClone();
    }
}
