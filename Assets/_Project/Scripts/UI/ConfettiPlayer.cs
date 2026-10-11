using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace RopeWrap.UI
{
    public class ConfettiPlayer : MonoBehaviour
    {
        [FormerlySerializedAs("m_ConfettiParticles")]
        [SerializeField] private ParticleSystem confettiParticles;
        [FormerlySerializedAs("m_PlayDuration")]
        [SerializeField] private float playDuration = 2f;

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (confettiParticles != null)
            {
                confettiParticles.Clear();
                confettiParticles.Play();
                float duration = playDuration > 0f ? playDuration : confettiParticles.main.duration;
                await UniTask.Delay((int)(duration * 1000), cancellationToken: ct);
                return;
            }

            await UniTask.Delay(500, cancellationToken: ct);
        }
    }
}
