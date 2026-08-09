using UnityEngine;
using Zenject;

namespace RopeWrap.UI
{
    /// <summary>
    /// MainMenuScene Zenject installer. SceneContext'e ekleyin.
    /// </summary>
    public class MainMenuSceneInstaller : MonoInstaller<MainMenuSceneInstaller>
    {
        [SerializeField] private MainMenuUI m_MainMenuUI;

        public override void InstallBindings()
        {
            if (m_MainMenuUI != null)
                Container.BindInstance(m_MainMenuUI).AsSingle();
            else
                Container.Bind<MainMenuUI>().FromComponentInHierarchy().AsSingle();
        }
    }
}
