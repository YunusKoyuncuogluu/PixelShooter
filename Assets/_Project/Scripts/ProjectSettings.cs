using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = nameof(ProjectSettings), menuName = "PixelShooter" + "/" + nameof(ProjectSettings), order = 0)]
public class ProjectSettings : ScriptableObject
{
    public const string ResourcePath = "Settings/" + nameof(ProjectSettings);

    public static ProjectSettings Load()
    {
        return Resources.Load<ProjectSettings>(ResourcePath);
    }

    #region Facade Settings

    [SerializeField, FoldoutGroup("Facade Settings")]
    [FormerlySerializedAs("m_FacadeProperties")]
    private List<FacadeProperty> facadeProperties;

    [SerializeField, FoldoutGroup("Facade Settings")]
    [FormerlySerializedAs("m_OriginalFacadeProperties")]
    private List<FacadeProperty> originalFacadeProperties;

    public List<FacadeProperty> GetFacadeProperties()
    {
        return CloneFacadeProperties(this.facadeProperties);
    }

    public List<FacadeProperty> GetOriginalFacadeProperties()
    {
        return CloneFacadeProperties(originalFacadeProperties);
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
