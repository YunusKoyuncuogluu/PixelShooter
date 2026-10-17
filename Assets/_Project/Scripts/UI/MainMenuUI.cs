using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;
using UnityEngine.Serialization;

namespace PixelShooter.UI
{

    public class MainMenuUI : MonoBehaviour
    {
        [Header("References (optional - will create if null)")]
        [FormerlySerializedAs("m_PlayButton")]
        [SerializeField] private Button playButton;
        [FormerlySerializedAs("m_SettingsButton")]
        [SerializeField] private Button settingsButton;
        [FormerlySerializedAs("m_SoundButton")]
        [SerializeField] private Button soundButton;
        [FormerlySerializedAs("m_SettingsPanel")]
        [SerializeField] private GameObject settingsPanel;
        [FormerlySerializedAs("m_LevelText")]
        [SerializeField] private TextMeshProUGUI levelText;
        [FormerlySerializedAs("m_TitleText")]
        [SerializeField] private TextMeshProUGUI titleText;

        [FormerlySerializedAs("m_SceneChanger")]
        [Inject] private SceneChanger sceneChanger;
        [FormerlySerializedAs("m_LevelManager")]
        [Inject] private LevelManager levelManager;

        private bool soundOn = true;
        private bool settingsOpen;

        private void Awake()
        {
            if (playButton == null)
                TryFindOrCreatePlayButton();
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (soundButton != null)
                soundButton.onClick.AddListener(OnSoundClicked);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (levelText != null && levelManager != null)
                levelText.text = "Level " + (levelManager.CurrentLevelIndex + 1);
        }

        private void OnPlayClicked()
        {
            sceneChanger.LoadGame();
        }

        private void OnSettingsClicked()
        {
            settingsOpen = !settingsOpen;
            if (settingsPanel != null)
                settingsPanel.SetActive(settingsOpen);
        }

        private void OnSoundClicked()
        {
            soundOn = !soundOn;
            AudioListener.volume = soundOn ? 1f : 0f;
            if (soundButton != null && soundButton.GetComponentInChildren<TextMeshProUGUI>(true) is { } label)
                label.text = soundOn ? "Sound: ON" : "Sound: OFF";
        }

        private void TryFindOrCreatePlayButton()
        {
            Button button = GetComponentInChildren<Button>();
            if (button != null)
                playButton = button;
        }
    }
}
