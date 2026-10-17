using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using PixelShooter.Game;
using UnityEngine.Serialization;

namespace PixelShooter.UI
{

    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [FormerlySerializedAs("m_LevelText")]
        [SerializeField] private TextMeshProUGUI levelText;
        [FormerlySerializedAs("m_PauseButton")]
        [SerializeField] private Button pauseButton;
        [FormerlySerializedAs("m_BackButton")]
        [SerializeField] private Button backButton;
        [FormerlySerializedAs("m_GiveUpButton")]
        [SerializeField] private Button giveUpButton;
        [FormerlySerializedAs("m_PausePanel")]
        [SerializeField] private GameObject pausePanel;

        [FormerlySerializedAs("m_LevelManager")]
        [Inject] private LevelManager levelManager;
        [FormerlySerializedAs("m_SceneChanger")]
        [Inject] private SceneChanger sceneChanger;
        [FormerlySerializedAs("m_LevelFlowController")]
        [InjectOptional] private LevelFlowController levelFlowController;

        private bool paused;
        private bool inputLocked;

        private void Awake()
        {
            if (levelText != null && levelManager != null)
                levelText.text = "Level " + (levelManager.CurrentLevelIndex + 1);
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseClicked);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (giveUpButton != null)
                giveUpButton.onClick.AddListener(OnGiveUpClicked);
            if (pausePanel != null)
                pausePanel.SetActive(false);
        }

        public void LockForFastForward()
        {
            inputLocked = true;
            paused = false;
            if (pausePanel != null)
                pausePanel.SetActive(false);
            if (pauseButton != null)
                pauseButton.interactable = false;
            if (backButton != null)
                backButton.interactable = false;
            if (giveUpButton != null)
                giveUpButton.interactable = false;
        }

        private void OnGiveUpClicked()
        {
            if (inputLocked)
                return;

            paused = false;
            Time.timeScale = 1f;
            if (pausePanel != null)
                pausePanel.SetActive(false);
            levelFlowController?.TriggerFail();
        }

        private void OnPauseClicked()
        {
            if (inputLocked)
                return;

            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            if (pausePanel != null)
                pausePanel.SetActive(paused);
        }

        private void OnBackClicked()
        {
            if (inputLocked)
                return;

            Time.timeScale = 1f;
            sceneChanger.LoadMainMenu();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
