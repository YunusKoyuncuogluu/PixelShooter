using System;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using Zenject;

public class LevelManager : IInitializable
{
    private const string CurrentLevelPref = "CurrentLevel";
    private const string LevelDataPath = "Levels/";

    private ScriptableLevelBase[] scriptableLevelDataArray;
    private int currentLevelIndex;

    public int CurrentLevelIndex => currentLevelIndex;
    public int TotalLevelCount => scriptableLevelDataArray.Length;
    public ILevelData CurrentLevelData => scriptableLevelDataArray[ResolveClampedLevelIndex()];

    private int ResolveClampedLevelIndex()
    {
        if (scriptableLevelDataArray == null || scriptableLevelDataArray.Length == 0)
            return 0;

        if (currentLevelIndex < 0)
            return 0;

        if (currentLevelIndex < TotalLevelCount)
            return currentLevelIndex;

        return currentLevelIndex % TotalLevelCount;
    }

    void IInitializable.Initialize()
    {
        currentLevelIndex = LoadLevel();
        scriptableLevelDataArray = Resources.LoadAll<ScriptableLevelBase>(LevelDataPath);
        SortLevelsByIndex();

        int levelNumber = currentLevelIndex + 1;
        string levelKey = "LevelNumber" + levelNumber;
        if (PlayerPrefs.GetInt(levelKey, 0) == 0)
            PlayerPrefs.SetInt(levelKey, 1);
    }

    public void SetLevel(int levelIndex)
    {
        currentLevelIndex = levelIndex;
        SaveLevel();
    }

    private void SaveLevel() => PlayerPrefs.SetInt(CurrentLevelPref, currentLevelIndex);
    private int LoadLevel() => PlayerPrefs.GetInt(CurrentLevelPref, 0);

    private void SortLevelsByIndex()
    {
        if (scriptableLevelDataArray == null || scriptableLevelDataArray.Length == 0)
            return;

        List<ScriptableLevelBase> sorted = new List<ScriptableLevelBase>(scriptableLevelDataArray.Length);
        for (int i = 0; i < scriptableLevelDataArray.Length; i++)
            sorted.Add(scriptableLevelDataArray[i]);

        sorted.Sort((a, b) => ParseLevelIndex(a.name).CompareTo(ParseLevelIndex(b.name)));
        scriptableLevelDataArray = sorted.ToArray();
    }

    private int ParseLevelIndex(string levelName)
    {
        if (string.IsNullOrEmpty(levelName))
            return 0;

        const string prefix = "Level_";
        if (!levelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return 0;

        string num = levelName.Substring(prefix.Length);
        return int.TryParse(num, out int index) ? index : 0;
    }

    public void IncreaseLevel() => SetLevel(currentLevelIndex + 1);

    public void CompleteCurrentLevel()
    {
        ILevelData currentLevelData = CurrentLevelData;
        currentLevelData.Complete(0);
        IncreaseLevel();
        ILevelData nextLevelData = CurrentLevelData;
        nextLevelData.Unlock();
    }

    public void FailCurrentLevel()
    {
    }
}
