using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Grids
{
    /// <summary>
    /// Fabric/Stitch grid cell - sadece renk (Grid, Tunnel, Question ile aynı FacadeProperty paleti).
    /// </summary>
    [Serializable]
    public class StitchCellData
    {
        [SerializeField, HideInInspector]
        private FacadeProperty m_FacadeProperty;

        [SerializeField]
        [OnValueChanged(nameof(OnColorLevelChanged))]
        [PropertyRange(0, nameof(GetFacadePropertyCount))]
        private int m_ColorLevel;

        public FacadeProperty FacadeProperty => m_FacadeProperty;

        /// <summary> -1 = duvar (siyah), 0 = boş, 1+ = renk. </summary>
        public int ColorLevel
        {
            get => m_ColorLevel;
            set
            {
                int max = GetFacadePropertyCount();
                if (value < -1 || (value > 0 && value > max))
                    return;
                m_ColorLevel = value;
                OnColorLevelChanged();
            }
        }

        public bool HasColor => m_ColorLevel > 0;
        public bool IsWall => m_ColorLevel == -1;

        private static int GetFacadePropertyCount() => FacadeProperty.GetFacadeProperties().Count();

        public void OnColorLevelChanged()
        {
            if (m_ColorLevel <= 0) return;
            var list = FacadeProperty.GetFacadeProperties();
            var match = list.FirstOrDefault(f => f.Level == m_ColorLevel);
            if (match != null)
                m_FacadeProperty = match;
        }
    }
}
