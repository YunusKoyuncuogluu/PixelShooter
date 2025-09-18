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

    public void Dispose()
    {
    }

    void IInitializable.Initialize()
    {
        ILevelData currentLevelData = m_LevelManager.CurrentLevelData;
        LevelData levelData = currentLevelData as LevelData;

        if (currentLevelData == null)
            Debug.Log("<color=red>Cannot find current level data!</color>");

        m_CellManager.Construct(levelData);
    }
}