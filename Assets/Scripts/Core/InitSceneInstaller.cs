using UnityEngine;
using Zenject;

namespace RopeWrap.Core
{
    /// <summary>
    /// InitializeScene SceneContext installer. InitBootstrap'i hierarchy'den baglar.
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
