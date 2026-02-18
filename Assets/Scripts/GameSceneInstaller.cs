using System.Collections;
using System.Collections.Generic;
using RopeWrap.UI;
using RopeWrap.Game;
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
    private Game.PlayerController m_PlayerController;

    [SerializeField] [FoldoutGroup("Managers")]
    private SlotManager m_SlotManager;

    [SerializeField] [FoldoutGroup("Managers")]
    private FabricManager m_FabricManager;

    [SerializeField] [FoldoutGroup("UI")]
    private LevelResultUI m_LevelResultUI;

    [SerializeField] [FoldoutGroup("UI")]
    private GameHUD m_GameHUD;

    [SerializeField] [FoldoutGroup("UI")]
    private LevelFlowController m_LevelFlowController;

    [SerializeField] [FoldoutGroup("UI")]
    private RopeWrap.UI.ConfettiPlayer m_ConfettiPlayer;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Thread m_ThreadFactory;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Cell m_CellFactory;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Slot m_SlotFactory;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private Tunnel m_TunnelFactory;

    [SerializeField] [FoldoutGroup("Prefabs")]
    private StitchNode m_StitchNodeFactory;

    public override void InstallBindings()
    {
        InstallManagers();
        InstallInstances();
    }

    public void InstallManagers()
    {
        Container.BindInterfacesAndSelfTo<RuntimeLevel>().AsSingle();
        Container.BindInstance(m_ThreadManager).AsSingle();
        Container.BindInstance(m_CellManager).AsSingle();
        Container.BindInstance(m_SlotManager).AsSingle();
        Container.BindInstance(m_PlayerController).AsSingle();
        Container.BindInstance(m_TunnelManager).AsSingle();
        if (m_FabricManager != null) Container.BindInterfacesAndSelfTo<FabricManager>().FromInstance(m_FabricManager).AsSingle();
        if (m_LevelResultUI != null) Container.BindInstance(m_LevelResultUI).AsSingle();
        if (m_GameHUD != null) Container.BindInstance(m_GameHUD).AsSingle();
        if (m_LevelFlowController != null) Container.BindInstance(m_LevelFlowController).AsSingle();
        if (m_ConfettiPlayer != null) Container.BindInstance(m_ConfettiPlayer).AsSingle();
    }

    public void InstallInstances()
    {
        Container.BindFactory<Cell, Cell.Factory>().FromComponentInNewPrefab(m_CellFactory);
        Container.BindFactory<Thread, Thread.Factory>().FromComponentInNewPrefab(m_ThreadFactory);
        Container.BindFactory<Slot, Slot.Factory>().FromComponentInNewPrefab(m_SlotFactory);
        Container.BindFactory<Tunnel, Tunnel.Factory>().FromComponentInNewPrefab(m_TunnelFactory);
        if (m_StitchNodeFactory != null)
            Container.BindFactory<StitchNode, StitchNode.Factory>().FromComponentInNewPrefab(m_StitchNodeFactory);
    }
}