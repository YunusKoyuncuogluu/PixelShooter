using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RopeWrap.UI
{
    /// <summary>
    /// Win aninda konfeti oynatir. Inspector'da ParticleSystem atanabilir; yoksa kisa bekleme yapar.
    /// </summary>
    public class ConfettiPlayer : MonoBehaviour
    {
        [SerializeField] private ParticleSystem m_ConfettiParticles;
        [SerializeField] private float m_PlayDuration = 2f;

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (m_ConfettiParticles != null)
            {
                m_ConfettiParticles.Clear();
                m_ConfettiParticles.Play();
                float duration = m_PlayDuration > 0f ? m_PlayDuration : m_ConfettiParticles.main.duration;
                await UniTask.Delay((int)(duration * 1000), cancellationToken: ct);
                return;
            }

            await UniTask.Delay(500, cancellationToken: ct);
        }
    }
}
