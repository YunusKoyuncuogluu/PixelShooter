using UnityEngine;
using Zenject;

namespace RopeWrap.Core
{

    public class InitBootstrap : MonoBehaviour
    {
        [Inject] private SceneChanger sceneChanger;

        private void Start()
        {
            sceneChanger.LoadMainMenu();
        }
    }
}
