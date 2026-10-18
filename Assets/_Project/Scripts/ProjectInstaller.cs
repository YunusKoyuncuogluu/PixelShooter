using LevelManagement;
using PixelShooter.Save;
using PixelShooter.Settings;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

public class ProjectInstaller : MonoInstaller<ProjectInstaller>
{
    [SerializeField] private LevelDatabase levelDatabase;
    [SerializeField] private AudioMixer masterMixer;

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
        Container.Bind<LevelDatabase>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<AudioMixer>().FromInstance(masterMixer).AsSingle();
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle();
        Container.BindInterfacesAndSelfTo<SettingsManager>().AsSingle();
        Container.Bind<LevelManager>().AsSingle();
    }
}
