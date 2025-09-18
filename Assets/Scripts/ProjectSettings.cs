using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ProjectSettings), menuName = "RopeWrap" + "/" + nameof(ProjectSettings), order = 0)]
public class ProjectSettings : ScriptableObject
{
    private static ProjectSettings s_Instance;
    private const string Path = "Settings/" + nameof(ProjectSettings);

    public static ProjectSettings Load()
    {
        if (s_Instance == null)
            s_Instance = Resources.Load<ProjectSettings>(Path);

        return s_Instance;
    }

    #region Facade Settings

    [SerializeField, FoldoutGroup("Facade Settings")]
    private List<FacadeProperty> m_FacadeProperties;

    [SerializeField, FoldoutGroup("Facade Settings")]
    private List<FacadeProperty> m_OriginalFacadeProperties;


    public List<FacadeProperty> GetFacadeProperties()
    {
        return CloneFacadeProperties(m_FacadeProperties);
    }

    public List<FacadeProperty> GetOriginalFacadeProperties()
    {
        return CloneFacadeProperties(m_OriginalFacadeProperties);
    }

    private List<FacadeProperty> CloneFacadeProperties(List<FacadeProperty> facadeProperties)
    {
        List<FacadeProperty> clonedList = new List<FacadeProperty>();

        foreach (var vProperty in facadeProperties)
        {
            clonedList.Add((FacadeProperty)vProperty.Clone());
        }

        return clonedList;
    }

    #endregion

    #region GameSettings

    [SerializeField] [FoldoutGroup("Cell Settings")]
    private float m_CellSpaceX;

    public float CellSpaceX => m_CellSpaceX;

    [SerializeField] [FoldoutGroup("Cell Settings")]
    private float m_CellSpaceY;

    public float CellSpaceY => m_CellSpaceY;

    #endregion
}