namespace PixelShooter.Settings
{
    public interface ISettingsProvider
    {
        bool IsSoundEnabled { get; }
        bool IsVibrationEnabled { get; }
        void SetSound(bool value);
        void SetVibration(bool value);
    }
}
