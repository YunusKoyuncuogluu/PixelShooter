using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

public class GameSceneInstaller : MonoInstaller<GameSceneInstaller>
{
    [SerializeField] [FoldoutGroup("Managers")]
    private ThreadManager m_ThreadManager;

    [SerializeField] [FoldoutGroup("Managers")]
    private TunnelManager m_TunnelManager;

    [SerializeField] [FoldoutGroup("Managers")]
    private CellManager m_CellManager;

    [SerializeField] [FoldoutGroup("Managers")]
    private PlayerController m_PlayerController;

    [SerializeField] [FoldoutGroup("Managers")]
    private SlotManager m_SlotManager;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Thread m_ThreadFactory;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Cell m_CellFactory;

    public override void InstallBindings()
    {
        InstallManagers();
        InstallInstances();
    }

    public void InstallManagers()
    {
        Container.BindInstance(m_ThreadManager).AsSingle();
        Container.BindInstance(m_CellManager).AsSingle();
    }

    public void InstallInstances()
    {
        Container.BindFactory<Cell, Cell.Factory>().FromComponentInNewPrefab(m_CellFactory);
        Container.BindFactory<Thread, Thread.Factory>().FromComponentInNewPrefab(m_ThreadFactory);
    }
}