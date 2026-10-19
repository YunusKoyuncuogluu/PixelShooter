using System.Threading;
using Cysharp.Threading.Tasks;
using PixelShooter.Settings;
using UnityEngine;
using Zenject;

namespace PixelShooter.Pooling
{
    public class GameAudio : MonoBehaviour
    {
        private const int VoiceSlotCapacity = 8;

        private ObjectPool objectPool;
        private ISettingsProvider settingsProvider;
        private GameAudioCatalog catalog;
        private CuePlayback shooterSelectPlayback;
        private CuePlayback bulletFirePlayback;
        private CuePlayback levelWinPlayback;
        private CuePlayback levelFailPlayback;

        [Inject]
        public void Construct(ObjectPool pool, ISettingsProvider settings, GameAudioCatalog audioCatalog)
        {
            objectPool = pool;
            settingsProvider = settings;
            catalog = audioCatalog;

            shooterSelectPlayback = new CuePlayback();
            bulletFirePlayback = new CuePlayback();
            levelWinPlayback = new CuePlayback();
            levelFailPlayback = new CuePlayback();
        }

        public void PlayShooterSelect() => Play(catalog != null ? catalog.ShooterSelect : null, shooterSelectPlayback);


        public void PlayBulletFire() => Play(catalog != null ? catalog.BulletFire : null, bulletFirePlayback);

        public void PlayLevelWin() => Play(catalog != null ? catalog.LevelWin : null, levelWinPlayback);

        public void PlayLevelFail() => Play(catalog != null ? catalog.LevelFail : null, levelFailPlayback);

        private void Play(SfxCue cue, CuePlayback playback)
        {
            if (cue == null || cue.Clip == null || playback == null || objectPool == null || settingsProvider == null || !settingsProvider.IsSoundEnabled)
                return;

            float now = Time.unscaledTime;
            if (now < playback.NextAllowedTime)
                return;

            int limit = cue.MaxVoices;
            if (limit < 1)
                limit = 1;

            if (limit > VoiceSlotCapacity)
                limit = VoiceSlotCapacity;

            int slot = playback.FindSlot(limit);
            PooledAudioVoice voice = playback.Voices[slot];
            GameObject voiceObject = playback.Objects[slot];

            if (voice == null)
            {
                voiceObject = objectPool.SpawnFromPool(PoolTags.AudioVoice, Vector3.zero, Quaternion.identity);
                if (voiceObject == null)
                    return;

                if (objectPool.TryGetPoolBehaviour(voiceObject, out IPoolObject behaviour))
                    voice = behaviour as PooledAudioVoice;
                if (voice == null)
                {
                    objectPool.DestroyToPool(PoolTags.AudioVoice, voiceObject);
                    return;
                }

                playback.Voices[slot] = voice;
                playback.Objects[slot] = voiceObject;
            }

            float pitch = ResolvePitch(cue);
            int playVersion = voice.Play(cue.Clip, cue.Volume, pitch, catalog.SfxGroup);
            playback.StartedAt[slot] = now;
            playback.NextAllowedTime = now + cue.MinRepeatInterval;
            float duration = cue.Clip.length;

            if (pitch > 0.01f)
                duration /= pitch;

            ReturnWhenFinishedAsync(playback, slot, voiceObject, voice, playVersion, duration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private float ResolvePitch(SfxCue cue)
        {
            float min = cue.PitchMin;
            float max = cue.PitchMax;

            if (max < min)
            {
                float swap = min;
                min = max;
                max = swap;
            }

            if (max <= min)
                return min;

            return Random.Range(min, max);
        }

        private async UniTaskVoid ReturnWhenFinishedAsync(
            CuePlayback playback,
            int slot,
            GameObject voiceObject,
            PooledAudioVoice voice,
            int playVersion,
            float length,
            CancellationToken cancellationToken)
        {
            int delayMs = (int)(length * 1000f);
            if (delayMs < 1)
                delayMs = 1;

            await UniTask.Delay(delayMs, ignoreTimeScale: true, cancellationToken: cancellationToken);
            if (objectPool == null || voiceObject == null || voice == null || !voice.IsCurrentPlay(playVersion))
                return;

            if (playback != null && slot >= 0 && slot < VoiceSlotCapacity && playback.Voices[slot] == voice)
            {
                playback.Voices[slot] = null;
                playback.Objects[slot] = null;
            }

            objectPool.DestroyToPool(PoolTags.AudioVoice, voiceObject);
        }

        private class CuePlayback
        {
            public readonly PooledAudioVoice[] Voices = new PooledAudioVoice[VoiceSlotCapacity];
            public readonly GameObject[] Objects = new GameObject[VoiceSlotCapacity];
            public readonly float[] StartedAt = new float[VoiceSlotCapacity];
            public float NextAllowedTime;

            public int FindSlot(int limit)
            {
                int oldest = 0;
                float oldestTime = float.MaxValue;
                for (int i = 0; i < limit; i++)
                {
                    if (Voices[i] == null)
                        return i;

                    if (StartedAt[i] < oldestTime)
                    {
                        oldestTime = StartedAt[i];
                        oldest = i;
                    }
                }

                return oldest;
            }
        }
    }
}
