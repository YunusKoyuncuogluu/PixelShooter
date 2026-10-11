using Zenject;

public class ProjectInstaller : MonoInstaller<ProjectInstaller>
{
    public override void InstallBindings()
    {
        InstallSettings();
        InstallManagers();
    }

    private void InstallSettings()
    {
        Container.Bind<ProjectSettings>()
            .FromScriptableObjectResource(ProjectSettings.ResourcePath)
            .AsSingle();
    }

    private void InstallManagers()
    {
        Container.Bind<SceneChanger>().AsSingle();
        Container.BindInterfacesAndSelfTo<LevelManager>().AsSingle();
    }
}
