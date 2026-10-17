using PixelShooter.Settings;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

namespace PixelShooter.UI
{
    public class UISetting : UIPopup
    {
        [FormerlySerializedAs("imgSoundBtn")]
        [SerializeField] private Image soundButtonImage;
        [FormerlySerializedAs("imgHapticBtn")]
        [SerializeField] private Image hapticButtonImage;
        [FormerlySerializedAs("sprButton")]
        [SerializeField] private Sprite[] buttonSprites;

        private ISettingsProvider settingsProvider;

        [Inject]
        public void Construct(ISettingsProvider settings)
        {
            settingsProvider = settings;
        }

        protected override void Start()
        {
            base.Start();
            UpdateButtonSprites();
        }

        public void OnToggleSoundBtn()
        {
            settingsProvider.SetSound(!settingsProvider.IsSoundEnabled);
            UpdateButtonSprites();
        }

        public void OnToggleHapticBtn()
        {
            settingsProvider.SetVibration(!settingsProvider.IsVibrationEnabled);
            UpdateButtonSprites();
        }

        public override void OnTapClose()
        {
            Close();
        }

        public void OnTapHomeBtn()
        {
            Time.timeScale = 1f;
            Close();
            if (uiManager != null)
            {
                uiManager.GoHome();
            }
        }

        private void UpdateButtonSprites()
        {
            if (buttonSprites == null || buttonSprites.Length < 2 || settingsProvider == null)
            {
                return;
            }

            if (soundButtonImage != null)
            {
                soundButtonImage.sprite = buttonSprites[settingsProvider.IsSoundEnabled ? 1 : 0];
            }

            if (hapticButtonImage != null)
            {
                hapticButtonImage.sprite = buttonSprites[settingsProvider.IsVibrationEnabled ? 1 : 0];
            }
        }
    }
}
