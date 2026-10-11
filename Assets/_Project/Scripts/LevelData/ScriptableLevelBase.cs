using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace LevelManagement
{
    [HideMonoScript]
    public abstract class ScriptableLevelBase : SerializedScriptableObject, ILevelData
    {
        [SerializeField, FoldoutGroup("Base Level Data")]
        [FormerlySerializedAs("m_IsUnlocked")]
        private bool isUnlocked;

        [SerializeField, FoldoutGroup("Base Level Data")]
        [FormerlySerializedAs("m_MoveCount")]
        private int moveCount;

        [SerializeField, FoldoutGroup("Base Level Data")]
        [FormerlySerializedAs("m_Time")]
        private float time;

        [SerializeField, FoldoutGroup("Base Level Data"), HideIf(nameof(isBonusLevel))]
        [FormerlySerializedAs("m_IsBossLevel")]
        private bool isBossLevel;

        [SerializeField, FoldoutGroup("Base Level Data"), HideIf(nameof(isBossLevel))]
        [FormerlySerializedAs("m_IsBonusLevel")]
        private bool isBonusLevel;

        private bool isCompleted;
        private int stars;

        bool ILevelData.IsUnlocked => isUnlocked;
        bool ILevelData.IsCompleted => isCompleted;
        bool ILevelData.IsBossLevel => isBossLevel;
        bool ILevelData.IsBonusLevel => isBonusLevel;
        int ILevelData.Stars => stars;
        int ILevelData.MoveCount => moveCount;
        float ILevelData.Time => time;

        void ILevelData.Complete(int? starsCount)
        {
            if (starsCount > stars)
                stars = starsCount.Value;

            isCompleted = true;
        }

        void ILevelData.Unlock() => isUnlocked = true;
    }
}
