using UnityEngine;
using Zenject;

namespace RopeWrap.Core
{
    /// <summary>
    /// InitializeScene'de SceneContext'e ekleyin. InitBootstrap'ı hierarchy'den bağlar.
    /// </summary>
    public class InitSceneInstaller : MonoInstaller<InitSceneInstaller>
    {
        [SerializeField] private InitBootstrap m_InitBootstrap;

        public override void InstallBindings()
        {
            if (m_InitBootstrap != null)
                Container.BindInstance(m_InitBootstrap).AsSingle();
            else
                Container.Bind<InitBootstrap>().FromComponentInHierarchy().AsSingle();
        }
    }
}
