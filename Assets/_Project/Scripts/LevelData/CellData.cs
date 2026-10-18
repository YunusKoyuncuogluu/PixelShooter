using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Grids
{
    [Serializable]
    public class CellData
    {
        [FormerlySerializedAs("m_IsLocked")]
        [SerializeField] private bool isLocked;
        [FormerlySerializedAs("m_Direction")]
        [SerializeField] private Direction direction;

        [SerializeField]
        [FormerlySerializedAs("m_QuestionThread")]
        [OnValueChanged(nameof(SetQuestionShooter))]
        private bool isQuestionShooter;

        [SerializeField]
        [FormerlySerializedAs("m_IsTunnelThread")]
        [OnValueChanged(nameof(SetTunnelShooter))]
        private bool isTunnelShooter;

        [FormerlySerializedAs("m_FacadeProperty")]
        [SerializeField, HideInInspector] private FacadeProperty facadeProperty;

        [SerializeField]
        [OnValueChanged(nameof(OnFacadeLevelChanged))]
        [PropertyRange(0, nameof(GetFacadePropertyCount))]
        [FormerlySerializedAs("m_ColorLevel")]
        private int colorLevel = 0;

        [SerializeField]
        [OnValueChanged(nameof(OnDirectionLevelChanged))]
        [PropertyRange(0, nameof(GetDirectionPropertyCount))]
        [FormerlySerializedAs("m_DirectionLevel")]
        private int directionLevel = 0;

        [SerializeField]
        [PropertyRange(0, 400)]
        [LabelText("Shot Count")]
        [FormerlySerializedAs("m_ShotCount")]
        private int shotCount;

        [ButtonGroup("ShotStepper")]
        [Button("-10")]
        private void DecreaseShotCountByTen()
        {
            SetShotCount(shotCount - 10);
        }

        [ButtonGroup("ShotStepper")]
        [Button("-1")]
        private void DecreaseShotCount()
        {
            SetShotCount(shotCount - 1);
        }

        [ButtonGroup("ShotStepper")]
        [Button("+1")]
        private void IncreaseShotCount()
        {
            SetShotCount(shotCount + 1);
        }

        [ButtonGroup("ShotStepper")]
        [Button("+10")]
        private void IncreaseShotCountByTen()
        {
            SetShotCount(shotCount + 10);
        }

        private int tunnelIndex;
        private int questionIndex;

        public FacadeProperty FacadeProperty => facadeProperty;
        public bool IsTunnel => isTunnelShooter;
        public bool IsLocked => this.isLocked;
        public bool IsFull => colorLevel != 0;
        public bool IsQuestionShooter => isQuestionShooter;

        public int ColorLevel
        {
            get => colorLevel;
            set
            {
                if (value > GetFacadePropertyCount())
                    return;
                colorLevel = value;
            }
        }

        public int TunnelIndex
        {
            get => tunnelIndex;
            set => tunnelIndex = value;
        }

        public int QuestionIndex
        {
            get => questionIndex;
            set => questionIndex = value;
        }

        public int DirectionLevel
        {
            get => directionLevel;
            set => directionLevel = value;
        }

        public int ShotCount => shotCount;

        public void SetShotCount(int value)
        {
            shotCount = value > 0 ? value : 0;
        }

        private int GetFacadePropertyCount()
        {
            ProjectSettings settings = ProjectSettings.Load();

            if (settings == null)
                return 0;

            List<FacadeProperty> properties = settings.GetFacadeProperties();

            return properties != null ? properties.Count : 0;
        }

        public void ChangeLocked(bool isLocked) => this.isLocked = isLocked;
        public void SetQuestionShooter(bool value) => isQuestionShooter = value;
        public void SetTunnelShooter(bool value) => isTunnelShooter = value;

        public void OnColorLevelChanged() => OnFacadeLevelChanged();

        public int GetDirectionPropertyCount() => 4;

        public Direction Direction
        {
            get => direction;
            set => direction = value;
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
                if (facade != null && facade.Level == colorLevel)
                {
                    facadeProperty = facade;
                    return;
                }
            }
        }

        public void OnDirectionLevelChanged()
        {
            direction = (Direction)directionLevel;
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
