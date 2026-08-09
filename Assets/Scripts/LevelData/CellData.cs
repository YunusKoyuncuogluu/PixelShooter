using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Grids
{
    [Serializable]
    public class CellData
    {
        [SerializeField] private bool m_IsLocked;
        [SerializeField] private Direction m_Direction;

        [SerializeField] [OnValueChanged(nameof(ChangeQuestionThread))]
        private bool m_QuestionThread;

        [SerializeField] [OnValueChanged(nameof(ChangeTunnelThread))]
        private bool m_IsTunnelThread;

        [SerializeField, HideInInspector] private FacadeProperty m_FacadeProperty;

        [SerializeField]
        [OnValueChanged(nameof(OnFacadeLevelChanged))]
        [PropertyRange(0, nameof(GetFacadePropertyCount))]
        private int m_ColorLevel = 0;

        [SerializeField]
        [OnValueChanged(nameof(OnDirectionLevelChanged))]
        [PropertyRange(0, nameof(GetDirectionPropertyCount))]
        private int m_DirectionLevel = 0;

        private int m_tunnelIndex;
        private int m_QuestionIndex;

        public FacadeProperty FacadeProperty => m_FacadeProperty;
        public bool IsTunnel => m_IsTunnelThread;
        public bool IsLocked => m_IsLocked;
        public bool IsFull => m_ColorLevel != 0;
        public bool IsQuestionThread => m_QuestionThread;

        public int ColorLevel
        {
            get => m_ColorLevel;
            set
            {
                if (value > GetFacadePropertyCount())
                    return;
                m_ColorLevel = value;
            }
        }

        public int TunnelIndex
        {
            get => m_tunnelIndex;
            set => m_tunnelIndex = value;
        }

        public int QuestionIndex
        {
            get => m_QuestionIndex;
            set => m_QuestionIndex = value;
        }

        public int DirectionLevel
        {
            get => m_DirectionLevel;
            set => m_DirectionLevel = value;
        }

        private int GetFacadePropertyCount()
        {
            ProjectSettings settings = ProjectSettings.Load();
            if (settings == null)
                return 0;

            List<FacadeProperty> properties = settings.GetFacadeProperties();
            return properties != null ? properties.Count : 0;
        }

        public void ChangeLocked(bool isLocked) => m_IsLocked = isLocked;
        public void ChangeQuestionThread(bool isQuestionThread) => m_QuestionThread = isQuestionThread;
        public void ChangeTunnelThread(bool isTunnelThread) => m_IsTunnelThread = isTunnelThread;

        public void OnColorLevelChanged() => OnFacadeLevelChanged();

        public int GetDirectionPropertyCount() => 4;

        public Direction Direction
        {
            get => m_Direction;
            set => m_Direction = value;
        }

        public void OnFacadeLevelChanged()
        {
            ProjectSettings settings = ProjectSettings.Load();
            if (settings == null)
                return;

            List<FacadeProperty> facadeProperties = settings.GetFacadeProperties();
            if (facadeProperties == null)
                return;

            for (int i = 0; i < facadeProperties.Count; i++)
            {
                FacadeProperty facade = facadeProperties[i];
                if (facade != null && facade.Level == m_ColorLevel)
                {
                    m_FacadeProperty = facade;
                    return;
                }
            }
        }

        public void OnDirectionLevelChanged()
        {
            m_Direction = (Direction)m_DirectionLevel;
        }
    }

    public enum Direction
    {
        None,
        Left,
        Right,
        Up,
        Down,
    }
}
