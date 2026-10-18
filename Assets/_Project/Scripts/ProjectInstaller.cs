using LevelManagement;
using PixelShooter.Pooling;
using PixelShooter.Save;
using PixelShooter.Settings;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class ProjectInstaller : MonoInstaller<ProjectInstaller>
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private ObjectPool objectPoolPrefab;

    public override void InstallBindings()
    {
        InstallSettings();
        InstallManagers();
    }

    private void InstallSettings()
    {
        Container.Bind<ProjectSettings>().FromScriptableObjectResource(ProjectSettings.ResourcePath).AsSingle();
    }

    private void InstallManagers()
    {
        Container.Bind<SceneChanger>().AsSingle();

        if (objectPoolPrefab != null)
            Container.Bind<ObjectPool>().FromComponentInNewPrefab(objectPoolPrefab).AsSingle().NonLazy();

        Container.Bind<LevelDatabase>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<AudioMixer>().FromInstance(masterMixer).AsSingle();
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<SettingsManager>().AsSingle();
        Container.Bind<LevelManager>().AsSingle();
    }
}
