using LevelManagement;
using UnityEngine;
using Zenject;

public class RuntimeLevel : IInitializable
{
    [Inject] private LevelManager levelManager;
    [Inject] private CellManager cellManager;
    [Inject] private SlotManager slotManager;
    [Inject] private ShooterManager shooterManager;
    [Inject] private TunnelManager tunnelManager;
    [Inject] private Game.PlayerController playerController;
    [Inject] private ImageManager imageManager;
    [InjectOptional] private ImageGridRenderer imageGridRenderer;
    [InjectOptional] private EnvironmentFrameBuilder environmentFrameBuilder;
    [InjectOptional] private PlateQueueManager plateQueueManager;

    void IInitializable.Initialize()
    {
        ILevelData currentLevelData = levelManager.CurrentLevelData;
        if (currentLevelData == null)
        {
            return;
        }

        LevelData levelData = currentLevelData as LevelData;

        cellManager.Construct(levelData);
        slotManager.Construct(levelData);
        shooterManager.Construct(levelData);
        tunnelManager.Construct(levelData);
        playerController.Construct();

        if (plateQueueManager != null)
            plateQueueManager.Construct(levelData);

        if (environmentFrameBuilder != null)
            environmentFrameBuilder.Rebuild(cellManager);

        if (levelData != null)
            levelData.UndoPixelYMigrateIfNeeded();

        if (imageGridRenderer != null && levelData != null)
            imageGridRenderer.BuildBatches(levelData);

        if (imageManager != null && levelData != null)
            imageManager.Construct(levelData);
    }
}
