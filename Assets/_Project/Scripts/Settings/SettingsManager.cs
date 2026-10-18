using PixelShooter.Save;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace PixelShooter.Settings
{
    public class SettingsManager : ISettingsProvider, IInitializable
    {
        private const string MasterVolumeParameter = "MasterVolume";
        private const float AudibleDecibels = 0f;
        private const float MutedDecibels = -80f;

        private readonly SaveManager saveManager;
        private readonly AudioMixer masterMixer;
        private bool soundEnabled;
        private bool vibrationEnabled;

        public bool IsSoundEnabled => soundEnabled;
        public bool IsVibrationEnabled => vibrationEnabled;

        [Inject]
        public SettingsManager(SaveManager save, AudioMixer mixer)
        {
            saveManager = save;
            masterMixer = mixer;
        }

        public void Initialize()
        {
            soundEnabled = saveManager.Data.soundEnabled;
            vibrationEnabled = saveManager.Data.vibrationEnabled;
            ApplySound();
        }

        public void SetSound(bool value)
        {
            soundEnabled = value;
            saveManager.Data.soundEnabled = value;
            saveManager.SaveToDisk();
            ApplySound();
        }

        public void SetVibration(bool value)
        {
            vibrationEnabled = value;
            saveManager.Data.vibrationEnabled = value;
            saveManager.SaveToDisk();
        }

        private void ApplySound()
        {
            if (masterMixer == null)
                return;

            float volume = soundEnabled ? AudibleDecibels : MutedDecibels;
            masterMixer.SetFloat(MasterVolumeParameter, volume);
        }
    }
}
