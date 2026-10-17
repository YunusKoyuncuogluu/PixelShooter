using LevelManagement;
using PixelShooter.Save;
using Zenject;

public enum LevelProgressState
{
    Locked = 0,
    Current = 1,
    Completed = 2
}

public class LevelManager
{
    private readonly LevelDatabase levelDatabase;
    private readonly SaveManager saveManager;
    private int currentLevelNumber;

    [Inject]
    public LevelManager(LevelDatabase levelDatabase, SaveManager saveManager)
    {
        this.levelDatabase = levelDatabase;
        this.saveManager = saveManager;
        currentLevelNumber = saveManager.Data.highestUnlockedLevel;
        if (currentLevelNumber < 1)
            currentLevelNumber = 1;
    }

    public int CurrentLevelNumber => currentLevelNumber;
    public int CurrentLevelIndex => currentLevelNumber - 1;
    public int HighestUnlockedLevel => saveManager.Data.highestUnlockedLevel;
    public int Coins => saveManager.Data.coins;
    public ILevelData CurrentLevelData => levelDatabase.GetLevelData(currentLevelNumber);

    public LevelProgressState GetProgressState(int levelNumber)
    {
        int highest = saveManager.Data.highestUnlockedLevel;
        if (levelNumber > highest)
            return LevelProgressState.Locked;

        if (levelNumber == highest)
            return LevelProgressState.Current;

        return LevelProgressState.Completed;
    }

    public bool TrySelectLevel(int levelNumber)
    {
        if (levelNumber < 1)
            return false;

        if (GetProgressState(levelNumber) == LevelProgressState.Locked)
            return false;

        currentLevelNumber = levelNumber;
        return true;
    }

    public void CompleteCurrentLevel()
    {
        int completedLevel = currentLevelNumber;

        PlayerData data = saveManager.Data;

        if (completedLevel >= data.highestUnlockedLevel)
            data.highestUnlockedLevel = completedLevel + 1;

        currentLevelNumber = completedLevel + 1;
    }
}
