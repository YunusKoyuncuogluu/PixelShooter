using UnityEngine;
using Zenject;

namespace PixelShooter.Core
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
