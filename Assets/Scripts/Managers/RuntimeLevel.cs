using System;
using System.Collections;
using System.Collections.Generic;
using LevelManagement;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

public class RuntimeLevel : IInitializable, IDisposable
{
    public event UnityAction<ILevelData> Initialized;

    [Inject] LevelManager m_LevelManager;
    [Inject] CellManager m_CellManager;
    [Inject] SlotManager m_SlotManager;
    [Inject] ThreadManager m_ThreadManager;

    public void Dispose()
    {
    }

    void IInitializable.Initialize()
    {
        ILevelData currentLevelData = m_LevelManager.CurrentLevelData;
        LevelData levelData = currentLevelData as LevelData;

        if (currentLevelData == null)
            Debug.Log("<color=red>Cannot find current level data!</color>");

        Initialized?.Invoke(currentLevelData);
        
        m_CellManager.Construct(levelData);
        m_SlotManager.Construct(levelData);
        m_ThreadManager.Construct(levelData);
    }
}