using UnityEngine;

public class PooledAudioVoice : MonoBehaviour
{
    private AudioSource source;

    public void Prepare()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    public void Play(AudioClip clip)
    {
        if (source == null)
            return;

        source.clip = clip;
        if (clip == null)
            return;

        source.Play();
    }
}
