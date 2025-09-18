using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        Container.Bind<ProjectSettings>().FromInstance(ProjectSettings.Load()).AsSingle();
    }

    private void InstallManagers()
    {
        Container.Bind<SceneChanger>().AsSingle();
        Container.BindInterfacesAndSelfTo<LevelManager>().AsSingle();
    }
}