using PixelShooter.Pooling;
using PixelShooter.UI;
using PixelShooter.Game;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class GameSceneInstaller : MonoInstaller<GameSceneInstaller>
{
    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_ThreadManager")]
    private ShooterManager shooterManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_TunnelManager")]
    private TunnelManager tunnelManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_CellManager")]
    private CellManager cellManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_PlayerController")]
    private Game.PlayerController playerController;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_SlotManager")]
    private SlotManager slotManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_ConveyorManager")]
    private ConveyorManager conveyorManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_PlateQueueManager")]
    private PlateQueueManager plateQueueManager;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("fabricGridRenderer")]
    private ImageGridRenderer imageGridRenderer;

    [SerializeField]
    [FoldoutGroup("Managers")]
    [FormerlySerializedAs("m_EnvironmentFrameBuilder")]
    private EnvironmentFrameBuilder environmentFrameBuilder;

    [SerializeField]
    [FoldoutGroup("UI")]
    private GameObject gamePagePrefab;

    [SerializeField]
    [FoldoutGroup("UI")]
    private GameObject failPagePrefab;

    [SerializeField]
    [FoldoutGroup("UI")]
    private GameObject completedPagePrefab;

    [SerializeField]
    [FoldoutGroup("UI")]
    private GameObject settingPopupPrefab;

    [SerializeField]
    [FoldoutGroup("UI")]
    [FormerlySerializedAs("m_LevelFlowController")]
    private LevelFlowController levelFlowController;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    [FormerlySerializedAs("m_ThreadFactory")]
    private Shooter shooterFactory;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    [FormerlySerializedAs("m_CellFactory")]
    private Cell cellFactory;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    [FormerlySerializedAs("m_SlotFactory")]
    private Slot slotFactory;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    [FormerlySerializedAs("m_TunnelFactory")]
    private Tunnel tunnelFactory;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    [FormerlySerializedAs("m_PlateFactory")]
    private Plate plateFactory;

    [SerializeField]
    [FoldoutGroup("Prefabs")]
    private ObjectPool objectPoolPrefab;

    [SerializeField]
    [FoldoutGroup("Audio")]
    private GameAudioCatalog gameAudioCatalog;

    public override void InstallBindings()
    {
        InstallManagers();
        InstallInstances();
    }

    public void InstallManagers()
    {
        Container.BindInterfacesAndSelfTo<RuntimeLevel>().AsSingle();
        Container.Bind<ImageManager>().AsSingle();
        Container.BindInstance(shooterManager).AsSingle();
        if (objectPoolPrefab != null)
        {
            Container.Bind<ObjectPool>().FromComponentInNewPrefab(objectPoolPrefab).AsSingle().NonLazy();
        }
        if (gameAudioCatalog != null)
        {
            Container.BindInstance(gameAudioCatalog).AsSingle();
        }

        Container.Bind<GameAudio>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        Container.Bind<UIManager>().FromNewComponentOnNewGameObject().AsSingle()
            .OnInstantiated<UIManager>((context, ui) => ui.Configure(gamePagePrefab, failPagePrefab, completedPagePrefab, settingPopupPrefab))
            .NonLazy();
        Container.BindInstance(cellManager).AsSingle();
        Container.BindInstance(slotManager).AsSingle();
        Container.BindInstance(playerController).AsSingle();
        Container.BindInstance(tunnelManager).AsSingle();

        if (conveyorManager != null)
        {
            Container.BindInstance(conveyorManager).AsSingle();
            Container.BindInterfacesAndSelfTo<WinFastForward>().AsSingle();
        }

        if (plateQueueManager != null)
            Container.BindInstance(plateQueueManager).AsSingle();

        if (levelFlowController != null)
            Container.BindInstance(levelFlowController).AsSingle();

        if (imageGridRenderer != null)
            Container.BindInterfacesAndSelfTo<ImageGridRenderer>().FromInstance(imageGridRenderer).AsSingle();

        if (environmentFrameBuilder != null)
            Container.BindInstance(environmentFrameBuilder).AsSingle();
    }

    public void InstallInstances()
    {
        Container.BindFactory<Cell, Cell.Factory>().FromComponentInNewPrefab(cellFactory);
        Container.BindFactory<Shooter, Shooter.Factory>().FromComponentInNewPrefab(shooterFactory);
        Container.BindFactory<Slot, Slot.Factory>().FromComponentInNewPrefab(slotFactory);
        Container.BindFactory<Tunnel, Tunnel.Factory>().FromComponentInNewPrefab(tunnelFactory);

        if (plateFactory != null)
            Container.BindFactory<Plate, Plate.Factory>().FromComponentInNewPrefab(plateFactory);
    }
}
