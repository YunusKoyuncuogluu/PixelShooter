using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(ProjectSettings), menuName = "RopeWrap" + "/" + nameof(ProjectSettings), order = 0)]
public class ProjectSettings : ScriptableObject
{
    public const string ResourcePath = "Settings/" + nameof(ProjectSettings);

    /// <summary>
    /// Editor / serialization context icin. Runtime DI uzerinden inject tercih edilir.
    /// Unity Resources cache kullanir; ekstra static instance tutulmaz.
    /// </summary>
    public static ProjectSettings Load()
    {
        return Resources.Load<ProjectSettings>(ResourcePath);
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
        if (facadeProperties == null)
            return clonedList;

        for (int i = 0; i < facadeProperties.Count; i++)
        {
            FacadeProperty property = facadeProperties[i];
            if (property != null)
                clonedList.Add((FacadeProperty)property.Clone());
        }

        return clonedList;
    }

    #endregion
}
