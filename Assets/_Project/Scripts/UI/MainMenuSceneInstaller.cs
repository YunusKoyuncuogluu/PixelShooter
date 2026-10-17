using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

namespace PixelShooter.UI
{

    public class MainMenuSceneInstaller : MonoInstaller<MainMenuSceneInstaller>
    {
        [FormerlySerializedAs("m_MainMenuUI")]
        [SerializeField] private MainMenuUI mainMenuUI;

        public override void InstallBindings()
        {
            if (mainMenuUI != null)
                Container.BindInstance(mainMenuUI).AsSingle();
            else
                Container.Bind<MainMenuUI>().FromComponentInHierarchy().AsSingle();
        }
    }
}
