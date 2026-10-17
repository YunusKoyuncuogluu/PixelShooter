using System.Threading;
using Cysharp.Threading.Tasks;
using PixelShooter.Settings;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace PixelShooter.Pooling
{
    public class GameAudio : MonoBehaviour
    {
        private const float SelectVolume = 0.3f;
        private const float FireVolume = 0.2f;
        private const float ResultVolume = 0.3f;

        private ObjectPool objectPool;
        private ISettingsProvider settingsProvider;
        private AudioMixerGroup sfxGroup;
        private AudioClip shooterSelectClip;
        private AudioClip bulletFireClip;
        private AudioClip levelWinClip;
        private AudioClip levelFailClip;

        [Inject]
        public void Construct(ObjectPool pool, ISettingsProvider settings)
        {
            objectPool = pool;
            settingsProvider = settings;
        }

        public void AssignOutput(AudioMixerGroup outputGroup)
        {
            sfxGroup = outputGroup;
        }

        public void AssignClips(AudioClip selectClip, AudioClip fireClip, AudioClip winClip, AudioClip failClip)
        {
            shooterSelectClip = selectClip;
            bulletFireClip = fireClip;
            levelWinClip = winClip;
            levelFailClip = failClip;
        }

        public void PlayShooterSelect()
        {
            Play(shooterSelectClip, SelectVolume);
        }

        public void PlayBulletFire()
        {
            Play(bulletFireClip, FireVolume);
        }

        public void PlayLevelWin()
        {
            Play(levelWinClip, ResultVolume);
        }

        public void PlayLevelFail()
        {
            Play(levelFailClip, ResultVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || objectPool == null || settingsProvider == null || !settingsProvider.IsSoundEnabled)
            {
                return;
            }

            GameObject voiceObject = objectPool.SpawnFromPool(PoolTags.AudioVoice, Vector3.zero, Quaternion.identity);
            if (voiceObject == null)
            {
                return;
            }

            PooledAudioVoice voice = voiceObject.GetComponent<PooledAudioVoice>();
            if (voice == null)
            {
                objectPool.DestroyToPool(PoolTags.AudioVoice, voiceObject);
                return;
            }

            int playVersion = voice.Play(clip, volume, sfxGroup);
            ReturnWhenFinishedAsync(voiceObject, voice, playVersion, clip.length, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid ReturnWhenFinishedAsync(
            GameObject voiceObject,
            PooledAudioVoice voice,
            int playVersion,
            float length,
            CancellationToken cancellationToken)
        {
            int delayMs = (int)(length * 1000f);
            if (delayMs < 1)
            {
                delayMs = 1;
            }

            await UniTask.Delay(delayMs, ignoreTimeScale: true, cancellationToken: cancellationToken);
            if (objectPool == null || voiceObject == null || voice == null || !voice.IsCurrentPlay(playVersion))
            {
                return;
            }

            objectPool.DestroyToPool(PoolTags.AudioVoice, voiceObject);
        }
    }
}
