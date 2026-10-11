using UnityEngine;

public class AudioPool : MonoBehaviour
{
    private const int VoiceCount = 16;

    private ComponentPool<PooledAudioVoice> voices;
    private PooledAudioVoice[] ring;
    private int ringIndex;
    private AudioClip shooterSelectClip;
    private AudioClip bulletFireClip;

    private void Awake()
    {
        ring = new PooledAudioVoice[VoiceCount];
        voices = new ComponentPool<PooledAudioVoice>(VoiceCount, transform, CreateVoice);

        for (int i = 0; i < VoiceCount; i++)
        {
            PooledAudioVoice voice = voices.Rent();
            voice.gameObject.SetActive(true);
            ring[i] = voice;
        }
    }

    public void AssignClips(AudioClip selectClip, AudioClip fireClip)
    {
        shooterSelectClip = selectClip;
        bulletFireClip = fireClip;
    }

    public void PlayShooterSelect()
    {
        PlayClip(shooterSelectClip);
    }

    public void PlayBulletFire()
    {
        PlayClip(bulletFireClip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || ring == null || ring.Length == 0)
            return;

        PooledAudioVoice voice = ring[ringIndex];
        ringIndex++;
        if (ringIndex >= ring.Length)
            ringIndex = 0;

        if (voice != null)
            voice.Play(clip);
    }

    private PooledAudioVoice CreateVoice(Transform parent)
    {
        GameObject voiceObject = new GameObject("AudioVoice");
        voiceObject.transform.SetParent(parent, false);
        PooledAudioVoice voice = voiceObject.AddComponent<PooledAudioVoice>();
        voice.Prepare();
        return voice;
    }
}
