using System.Collections;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using Zenject;

public class LevelManager : IInitializable
{
    private const string m_CurrentLevelPref = "CurrentLevel";
    private ScriptableLevelBase[] m_ScriptableLevelDataArray;
    private int m_CurrentLevelIndex;

    [Inject] private ProjectSettings m_ProjectSettings;

    public int CurrentLevelIndex => m_CurrentLevelIndex;
    public string CurrentLevelName => $"Level Index ===>> {m_CurrentLevelIndex + 1}";
    public int TotalLevelCount => m_ScriptableLevelDataArray.Length;
    public ILevelData CurrentLevelData => m_ScriptableLevelDataArray[GetCurrentLevelIndex()];
    public ILevelData[] AllLevelData => m_ScriptableLevelDataArray;
    public static LevelManager Instance;

    private int GetCurrentLevelIndex()
    {
        if (m_CurrentLevelIndex < 0)
            return 0;

        if (m_CurrentLevelIndex < TotalLevelCount)
            return m_CurrentLevelIndex;

        if ((TotalLevelCount - 1) >= TotalLevelCount)
            return m_CurrentLevelIndex % TotalLevelCount;

        int modIndex = m_CurrentLevelIndex % TotalLevelCount + (TotalLevelCount - 1);

        if (modIndex < 0)
            return TotalLevelCount + modIndex;

        if (modIndex < TotalLevelCount)
            return modIndex;

        if (modIndex >= TotalLevelCount)
            return modIndex - TotalLevelCount;

        return modIndex;
    }

    void IInitializable.Initialize()
    {
        int levelIndex = CurrentLevelIndex + 1;

        if (PlayerPrefs.GetInt("LevelNumber" + levelIndex, 0) == 0)
        {
            PlayerPrefs.SetInt("LevelNumber" + levelIndex, 1);
            Debug.Log($"<color=green> Level {levelIndex} initialized. </color>");
        }

        m_CurrentLevelIndex = LoadLevel();
        m_ScriptableLevelDataArray = ScriptableLevelBase.LoadAll();
        Instance = this;
    }

    public void SetLevel(int levelIndex)
    {
        m_CurrentLevelIndex = levelIndex;
        SaveLevel();
    }

    private void SaveLevel() => PlayerPrefs.SetInt(m_CurrentLevelPref, m_CurrentLevelIndex);
    private static int LoadLevel() => PlayerPrefs.GetInt(m_CurrentLevelPref, 0);

    public void IncreaseLevel() => SetLevel(m_CurrentLevelIndex + 1);
    public void DecreaseLevel() => SetLevel(m_CurrentLevelIndex - 1);

    public void CompleteCurrentLevel()
    {
        int levelIndex = m_CurrentLevelIndex + 1;
        Debug.Log($"<color=green> Win Level {levelIndex}. </color>");
        ILevelData currentLevelData = CurrentLevelData;
        currentLevelData.Complete(0);
        IncreaseLevel();
        ILevelData nextLevelData = currentLevelData;
        nextLevelData.Unlock();
    }

    public void FailCurrentLevel()
    {
        int levelIndex = m_CurrentLevelIndex + 1;
        Debug.Log($"<color=red>Fail Level {levelIndex}. </color>");
    }
}