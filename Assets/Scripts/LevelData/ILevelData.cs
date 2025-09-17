namespace LevelManagement
{
    public interface ILevelData
    {
        int MoveCount { get; }
        int Stars { get; }

        float Time { get; }

        bool IsUnlocked { get; }
        bool IsCompleted { get; }
        bool IsBossLevel { get; }
        bool IsBonusLevel { get; }
        bool HasMoveCount => MoveCount > 0;
        bool HasTiime => Time > 0;

        void Complete(int? starsCount);
        void Unlock();
    }
}