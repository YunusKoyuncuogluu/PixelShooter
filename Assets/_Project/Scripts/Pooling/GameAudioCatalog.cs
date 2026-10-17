using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace PixelShooter.Pooling
{
    [CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "PixelShooter/Game Audio Catalog")]
    public class GameAudioCatalog : ScriptableObject
    {
        [SerializeField]
        [FoldoutGroup("Output")]
        private AudioMixerGroup sfxGroup;

        [SerializeField]
        [FoldoutGroup("Cues")]
        private SfxCue shooterSelect;

        [SerializeField]
        [FoldoutGroup("Cues")]
        private SfxCue bulletFire;

        [SerializeField]
        [FoldoutGroup("Cues")]
        private SfxCue levelWin;

        [SerializeField]
        [FoldoutGroup("Cues")]
        private SfxCue levelFail;

        public AudioMixerGroup SfxGroup
        {
            get { return sfxGroup; }
        }

        public SfxCue ShooterSelect
        {
            get { return shooterSelect; }
        }

        public SfxCue BulletFire
        {
            get { return bulletFire; }
        }

        public SfxCue LevelWin
        {
            get { return levelWin; }
        }

        public SfxCue LevelFail
        {
            get { return levelFail; }
        }
    }
}
