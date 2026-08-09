using LevelManagement;
using UnityEngine;
using Zenject;

public class RuntimeLevel : IInitializable
{
    [Inject] private LevelManager m_LevelManager;
    [Inject] private CellManager m_CellManager;
    [Inject] private SlotManager m_SlotManager;
    [Inject] private ThreadManager m_ThreadManager;
    [Inject] private TunnelManager m_TunnelManager;
    [Inject] private Game.PlayerController m_PlayerController;
    [Inject] private FabricManager m_FabricManager;
    [InjectOptional] private FabricGridRenderer m_FabricGridRenderer;
    [InjectOptional] private EnvironmentFrameBuilder m_EnvironmentFrameBuilder;
    [InjectOptional] private PlateQueueManager plateQueueManager;

    void IInitializable.Initialize()
    {
        ILevelData currentLevelData = m_LevelManager.CurrentLevelData;
        if (currentLevelData == null)
        {
            Debug.LogError("[RuntimeLevel] Current level data yok.");
            return;
        }

        LevelData levelData = currentLevelData as LevelData;

        m_CellManager.Construct(levelData);
        m_SlotManager.Construct(levelData);
        m_ThreadManager.Construct(levelData);
        m_TunnelManager.Construct(levelData);
        m_PlayerController.Construct();

        if (plateQueueManager != null)
            plateQueueManager.Construct(levelData);

        if (m_EnvironmentFrameBuilder != null)
            m_EnvironmentFrameBuilder.Rebuild(m_CellManager);

        if (levelData != null)
            levelData.UndoPixelYMigrateIfNeeded();

        if (m_FabricGridRenderer != null && levelData != null)
            m_FabricGridRenderer.BuildBatches(levelData);

        if (m_FabricManager != null && levelData != null)
            m_FabricManager.Construct(levelData);
    }
}
