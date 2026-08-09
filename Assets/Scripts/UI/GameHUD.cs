using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using RopeWrap.Game;

namespace RopeWrap.UI
{
    /// <summary>
    /// Oyun sahnesi HUD: level numarasi, pause butonu, vb.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private Button m_PauseButton;
        [SerializeField] private Button m_BackButton;
        [SerializeField] private Button m_GiveUpButton;
        [SerializeField] private GameObject m_PausePanel;

        [Inject] private LevelManager m_LevelManager;
        [Inject] private SceneChanger m_SceneChanger;
        [InjectOptional] private LevelFlowController m_LevelFlowController;

        private bool m_Paused;

        private void Awake()
        {
            if (m_LevelText != null && m_LevelManager != null)
                m_LevelText.text = "Level " + (m_LevelManager.CurrentLevelIndex + 1);
            if (m_PauseButton != null)
                m_PauseButton.onClick.AddListener(OnPauseClicked);
            if (m_BackButton != null)
                m_BackButton.onClick.AddListener(OnBackClicked);
            if (m_GiveUpButton != null)
                m_GiveUpButton.onClick.AddListener(OnGiveUpClicked);
            if (m_PausePanel != null)
                m_PausePanel.SetActive(false);
        }

        private void OnGiveUpClicked()
        {
            m_Paused = false;
            Time.timeScale = 1f;
            if (m_PausePanel != null)
                m_PausePanel.SetActive(false);
            m_LevelFlowController?.TriggerFail();
        }

        private void OnPauseClicked()
        {
            m_Paused = !m_Paused;
            Time.timeScale = m_Paused ? 0f : 1f;
            if (m_PausePanel != null)
                m_PausePanel.SetActive(m_Paused);
        }

        private void OnBackClicked()
        {
            Time.timeScale = 1f;
            m_SceneChanger.LoadMainMenu();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
