using System;

namespace PixelShooter.Save
{
    [Serializable]
    public class PlayerData
    {
        public int highestUnlockedLevel = 1;
        public int coins;
        public int settingsVersion;
        public bool soundEnabled = true;
        public bool vibrationEnabled = true;
    }
}
