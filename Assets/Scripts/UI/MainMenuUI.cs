using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using RopeWrap.Core;

namespace RopeWrap.UI
{
    /// <summary>
    /// Main Menu sahnesi UI. Play, Settings, Sound toggle vb.
    /// Canvas ve butonları Inspector'dan atayın veya runtime'da oluşturulur.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("References (optional - will create if null)")]
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private Button m_SoundButton;
        [SerializeField] private GameObject m_SettingsPanel;
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_TitleText;

        [Inject] private SceneChanger m_SceneChanger;
        [Inject] private LevelManager m_LevelManager;

        private bool m_SoundOn = true;
        private bool m_SettingsOpen;

        private void Awake()
        {
            if (m_PlayButton == null) TryFindOrCreatePlayButton();
            if (m_PlayButton != null) m_PlayButton.onClick.AddListener(OnPlayClicked);
            if (m_SettingsButton != null) m_SettingsButton.onClick.AddListener(OnSettingsClicked);
            if (m_SoundButton != null) m_SoundButton.onClick.AddListener(OnSoundClicked);
            if (m_SettingsPanel != null) m_SettingsPanel.SetActive(false);
            if (m_LevelText != null && m_LevelManager != null)
                m_LevelText.text = "Level " + (m_LevelManager.CurrentLevelIndex + 1);
        }

        private void OnPlayClicked()
        {
            m_SceneChanger.LoadGame();
        }

        private void OnSettingsClicked()
        {
            m_SettingsOpen = !m_SettingsOpen;
            if (m_SettingsPanel != null) m_SettingsPanel.SetActive(m_SettingsOpen);
        }

        private void OnSoundClicked()
        {
            m_SoundOn = !m_SoundOn;
            AudioListener.volume = m_SoundOn ? 1f : 0f;
            if (m_SoundButton != null && m_SoundButton.GetComponentInChildren<TextMeshProUGUI>(true) is { } t)
                t.text = m_SoundOn ? "Sound: ON" : "Sound: OFF";
        }

        private void TryFindOrCreatePlayButton()
        {
            var b = GetComponentInChildren<Button>();
            if (b != null) m_PlayButton = b;
        }
    }
}
