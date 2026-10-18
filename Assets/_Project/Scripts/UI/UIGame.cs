using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

namespace PixelShooter.UI
{
    public class UIGame : UIPage
    {
        [FormerlySerializedAs("lblLevel")]
        [SerializeField] private TextMeshProUGUI levelLabel;

        private LevelManager levelManager;
        private SceneChanger sceneChanger;

        [Inject]
        public void Construct(LevelManager levels, SceneChanger scenes)
        {
            levelManager = levels;
            sceneChanger = scenes;
        }

        protected override void Start()
        {
            if (levelLabel != null && levelManager != null)
                levelLabel.text = $"Level {levelManager.CurrentLevelNumber}";

            base.Start();
        }

        public void OnTapToRestart()
        {
            Time.timeScale = 1f;
            sceneChanger.LoadGame();
        }

        public void OnTapSettings()
        {
            if (uiManager != null)
                uiManager.ShowSettings();
        }

        public void LockInput()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = false;
        }
    }
}
