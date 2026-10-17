using PixelShooter.Pooling;
using UnityEngine;
using UnityEngine.Audio;

public class PooledAudioVoice : MonoBehaviour, IPoolObject
{
    private AudioSource source;
    private int playVersion;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    public int Play(AudioClip clip, float volume, AudioMixerGroup outputGroup)
    {
        playVersion++;
        if (source == null || clip == null)
        {
            return playVersion;
        }

        source.outputAudioMixerGroup = outputGroup;
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f;
        source.loop = false;
        source.ignoreListenerPause = true;
        source.Play();
        return playVersion;
    }

    public bool IsCurrentPlay(int version)
    {
        return playVersion == version;
    }

    public void Reset()
    {
        playVersion++;
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
    }
}
