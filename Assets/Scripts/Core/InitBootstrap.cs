using UnityEngine;
using Zenject;

namespace RopeWrap.Core
{
    /// <summary>
    /// InitializeScene'de çalışır. ProjectContext yüklendikten sonra Main Menu'ye geçer.
    /// Bu script'i Init sahnesinde bir GameObject'e ekleyin.
    /// </summary>
    public class InitBootstrap : MonoBehaviour
    {
        [Inject] private SceneChanger m_SceneChanger;

        private void Start()
        {
            m_SceneChanger.LoadMainMenu();
        }
    }
}
