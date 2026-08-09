using UnityEngine;
using Zenject;

namespace RopeWrap.Core
{
    /// <summary>
    /// InitializeScene'de calisir. ProjectContext yuklendikten sonra Main Menu'ye gecer.
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
