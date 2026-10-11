using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

namespace RopeWrap.Core
{

    public class InitSceneInstaller : MonoInstaller<InitSceneInstaller>
    {
        [FormerlySerializedAs("m_InitBootstrap")]
        [SerializeField] private InitBootstrap initBootstrap;

        public override void InstallBindings()
        {
            if (initBootstrap != null)
                Container.BindInstance(initBootstrap).AsSingle();
            else
                Container.Bind<InitBootstrap>().FromComponentInHierarchy().AsSingle();
        }
    }
}
