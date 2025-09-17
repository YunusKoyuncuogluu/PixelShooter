using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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
        [PropertyRange(0, nameof(GetDirectionProprotyCount))]
        private int m_DirectionLevel = 0;

        private List<FacadeProperty> m_FacadeProperties;

        private int preDirectionLevel = 0;
        private int m_tunnelIndex;
        private int m_QuestionIndex;
        private List<FacadeProperty> FacadeProperties => m_FacadeProperties;

        public FacadeProperty FacadeProperty => m_FacadeProperty;
        public bool IsTunnel => m_IsTunnelThread;
        public bool IsLocked => m_IsLocked;
        public bool IsFull => m_ColorLevel != 0;
        public bool IsQuestionThread => m_QuestionThread;

        public int ColorLevel
        {
            get => m_ColorLevel;
            set => m_ColorLevel = value;
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
            get
            {
                preDirectionLevel = m_DirectionLevel;
                return m_DirectionLevel;
            }
            set => m_DirectionLevel = value;
        }

        private int GetFacadePropertyCount() => FacadeProperty.GetFacadeProperties().Count();

        public void ChangeLocked(bool isLocked) => m_IsLocked = isLocked;
        public void ChangeQuestionThread(bool isQuestionThread) => m_QuestionThread = isQuestionThread;
        public void ChangeTunnelThread(bool isTunnelThread) => m_IsTunnelThread = isTunnelThread;

        public void OnColorLevelChanged() => OnFacadeLevelChanged();

        public int GetDirectionProprotyCount() => 4;

        public Direction Direction
        {
            get => m_Direction;
            set => m_Direction = value;
        }

        private void InitializeFacadeProperties()
        {
            m_FacadeProperties = new List<FacadeProperty>(global::FacadeProperty.GetFacadeProperties());
            if (m_FacadeProperties.Count > 0)
            {
                m_FacadeProperty = m_FacadeProperties.First();
            }
        }

        public void OnFacadeLevelChanged()
        {
            List<FacadeProperty> facadeProperties = FacadeProperty.GetFacadeProperties().ToList();
            FacadeProperty matchingFacadeProperty =
                facadeProperties.FirstOrDefault(facade => facade.Level == m_ColorLevel);

            if (matchingFacadeProperty != null)
                m_FacadeProperty = matchingFacadeProperty;
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