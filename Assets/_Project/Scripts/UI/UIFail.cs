using UnityEngine;
using Zenject;

namespace PixelShooter.UI
{
    public class UIFail : UIPage
    {
        private SceneChanger sceneChanger;

        [Inject]
        public void Construct(SceneChanger scenes)
        {
            sceneChanger = scenes;
        }

        public void OnTapPlayAgain()
        {
            Time.timeScale = 1f;
            sceneChanger.LoadGame();
        }

        public void OnTapHome()
        {
            Time.timeScale = 1f;
            sceneChanger.LoadMainMenu();
        }
    }
}
