using Sirenix.OdinInspector;
using UnityEngine;

namespace LevelManagement
{
    [HideMonoScript]
    public abstract class ScriptableLevelBase : SerializedScriptableObject, ILevelData
    {
        [SerializeField, FoldoutGroup("Base Level Data")]
        private bool m_IsUnlocked;

        [SerializeField, FoldoutGroup("Base Level Data")]
        private int m_MoveCount;

        [SerializeField, FoldoutGroup("Base Level Data")]
        private float m_Time;

        [SerializeField, FoldoutGroup("Base Level Data"), HideIf(nameof(m_IsBonusLevel))]
        private bool m_IsBossLevel;

        [SerializeField, FoldoutGroup("Base Level Data"), HideIf(nameof(m_IsBossLevel))]
        private bool m_IsBonusLevel;

        private bool m_IsCompleted;
        private int m_Stars;

        bool ILevelData.IsUnlocked => m_IsUnlocked;
        bool ILevelData.IsCompleted => m_IsCompleted;
        bool ILevelData.IsBossLevel => m_IsBossLevel;
        bool ILevelData.IsBonusLevel => m_IsBonusLevel;
        int ILevelData.Stars => m_Stars;
        int ILevelData.MoveCount => m_MoveCount;
        float ILevelData.Time => m_Time;

        void ILevelData.Complete(int? starsCount)
        {
            if (starsCount > m_Stars)
                m_Stars = starsCount.Value;

            m_IsCompleted = true;
        }

        void ILevelData.Unlock() => m_IsUnlocked = true;
    }
}
