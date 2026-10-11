using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using UnityEngine.Serialization;

namespace RopeWrap.UI
{

    public class LevelResultUI : MonoBehaviour
    {
        [Header("Panels")]
        [FormerlySerializedAs("m_SuccessPanel")]
        [SerializeField] private GameObject successPanel;
        [FormerlySerializedAs("m_FailPanel")]
        [SerializeField] private GameObject failPanel;

        [Header("Success")]
        [FormerlySerializedAs("m_NextLevelButton")]
        [SerializeField] private Button nextLevelButton;
        [FormerlySerializedAs("m_HomeFromSuccessButton")]
        [SerializeField] private Button homeFromSuccessButton;
        [FormerlySerializedAs("m_SuccessLevelText")]
        [SerializeField] private TextMeshProUGUI successLevelText;
        [FormerlySerializedAs("m_StarsText")]
        [SerializeField] private TextMeshProUGUI starsText;

        [Header("Fail")]
        [FormerlySerializedAs("m_RetryButton")]
        [SerializeField] private Button retryButton;
        [FormerlySerializedAs("m_HomeFromFailButton")]
        [SerializeField] private Button homeFromFailButton;
        [FormerlySerializedAs("m_FailLevelText")]
        [SerializeField] private TextMeshProUGUI failLevelText;

        [FormerlySerializedAs("m_LevelManager")]
        [Inject] private LevelManager levelManager;
        [FormerlySerializedAs("m_SceneChanger")]
        [Inject] private SceneChanger sceneChanger;

        private void Awake()
        {
            if (nextLevelButton != null)
                nextLevelButton.onClick.AddListener(OnNextLevel);
            if (homeFromSuccessButton != null)
                homeFromSuccessButton.onClick.AddListener(OnHome);
            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetry);
            if (homeFromFailButton != null)
                homeFromFailButton.onClick.AddListener(OnHome);
            HideAll();
        }

        public void ShowSuccess(int stars = 1)
        {
            if (successPanel != null)
                successPanel.SetActive(true);
            if (failPanel != null)
                failPanel.SetActive(false);
            if (successLevelText != null && levelManager != null)
                successLevelText.text = "Level " + (levelManager.CurrentLevelIndex + 1) + " Complete!";
            if (starsText != null)
                starsText.text = stars + " Star(s)";
        }

        public void ShowFail()
        {
            if (failPanel != null)
                failPanel.SetActive(true);
            if (successPanel != null)
                successPanel.SetActive(false);
            if (failLevelText != null && levelManager != null)
                failLevelText.text = "Level " + (levelManager.CurrentLevelIndex + 1) + " Failed";
        }

        public void HideAll()
        {
            if (successPanel != null)
                successPanel.SetActive(false);
            if (failPanel != null)
                failPanel.SetActive(false);
        }

        private void OnNextLevel()
        {
            levelManager.CompleteCurrentLevel();
            HideAll();
            sceneChanger.LoadGame();
        }

        private void OnRetry()
        {
            HideAll();
            sceneChanger.LoadGame();
        }

        private void OnHome()
        {
            HideAll();
            sceneChanger.LoadMainMenu();
        }
    }
}
