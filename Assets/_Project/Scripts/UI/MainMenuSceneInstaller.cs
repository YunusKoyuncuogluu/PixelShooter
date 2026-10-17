using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

namespace PixelShooter.UI
{

    public class MainMenuSceneInstaller : MonoInstaller<MainMenuSceneInstaller>
    {
        [FormerlySerializedAs("m_MainMenuUI")]
        [SerializeField] private UIManager mainMenuUI;

        [SerializeField] private GameObject settingPopupPrefab;

        public override void InstallBindings()
        {
            if (mainMenuUI != null)
            {
                mainMenuUI.Configure(null, null, null, settingPopupPrefab);
                Container.BindInstance(mainMenuUI).AsSingle();
            }
            else
            {
                Container.Bind<UIManager>().FromComponentInHierarchy().AsSingle();
            }
        }
    }
}
