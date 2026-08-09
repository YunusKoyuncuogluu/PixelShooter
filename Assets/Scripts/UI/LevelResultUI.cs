using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;

namespace RopeWrap.UI
{
    /// <summary>
    /// Level basari / basarisizlik ekrani. Next, Retry, Home butonlari.
    /// </summary>
    public class LevelResultUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject m_SuccessPanel;
        [SerializeField] private GameObject m_FailPanel;

        [Header("Success")]
        [SerializeField] private Button m_NextLevelButton;
        [SerializeField] private Button m_HomeFromSuccessButton;
        [SerializeField] private TextMeshProUGUI m_SuccessLevelText;
        [SerializeField] private TextMeshProUGUI m_StarsText;

        [Header("Fail")]
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_HomeFromFailButton;
        [SerializeField] private TextMeshProUGUI m_FailLevelText;

        [Inject] private LevelManager m_LevelManager;
        [Inject] private SceneChanger m_SceneChanger;

        private void Awake()
        {
            if (m_NextLevelButton != null)
                m_NextLevelButton.onClick.AddListener(OnNextLevel);
            if (m_HomeFromSuccessButton != null)
                m_HomeFromSuccessButton.onClick.AddListener(OnHome);
            if (m_RetryButton != null)
                m_RetryButton.onClick.AddListener(OnRetry);
            if (m_HomeFromFailButton != null)
                m_HomeFromFailButton.onClick.AddListener(OnHome);
            HideAll();
        }

        public void ShowSuccess(int stars = 1)
        {
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(true);
            if (m_FailPanel != null)
                m_FailPanel.SetActive(false);
            if (m_SuccessLevelText != null && m_LevelManager != null)
                m_SuccessLevelText.text = "Level " + (m_LevelManager.CurrentLevelIndex + 1) + " Complete!";
            if (m_StarsText != null)
                m_StarsText.text = stars + " Star(s)";
        }

        public void ShowFail()
        {
            if (m_FailPanel != null)
                m_FailPanel.SetActive(true);
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(false);
            if (m_FailLevelText != null && m_LevelManager != null)
                m_FailLevelText.text = "Level " + (m_LevelManager.CurrentLevelIndex + 1) + " Failed";
        }

        public void HideAll()
        {
            if (m_SuccessPanel != null)
                m_SuccessPanel.SetActive(false);
            if (m_FailPanel != null)
                m_FailPanel.SetActive(false);
        }

        private void OnNextLevel()
        {
            m_LevelManager.CompleteCurrentLevel();
            HideAll();
            m_SceneChanger.LoadGame();
        }

        private void OnRetry()
        {
            HideAll();
            m_SceneChanger.LoadGame();
        }

        private void OnHome()
        {
            HideAll();
            m_SceneChanger.LoadMainMenu();
        }
    }
}
