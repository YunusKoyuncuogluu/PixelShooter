using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using RopeWrap.UI;

namespace RopeWrap.Game
{
    /// <summary>
    /// Level kazanma/kaybetme akışını yönetir. Fabric cleared -> Win (konfeti + success panel).
    /// Spool area stuck -> Fail panel.
    /// </summary>
    public class LevelFlowController : MonoBehaviour
    {
        [Inject] private LevelManager m_LevelManager;
        [Inject] private FabricManager m_FabricManager;
        [Inject] private SlotManager m_SlotManager;
        [Inject] private LevelResultUI m_LevelResultUI;
        [InjectOptional] private ConfettiPlayer m_ConfettiPlayer;

        private bool m_ResultShown;
        private CancellationTokenSource m_FlowCts;

        private void Start()
        {
            m_ResultShown = false;
            m_FlowCts = new CancellationTokenSource();

            if (m_FabricManager != null)
                m_FabricManager.OnFabricCleared += OnFabricCleared;
            if (m_SlotManager != null)
                m_SlotManager.OnSpoolAreaStuck += OnSpoolAreaStuck;
        }

        private void OnDestroy()
        {
            m_FlowCts?.Cancel();
            if (m_FabricManager != null)
                m_FabricManager.OnFabricCleared -= OnFabricCleared;
            if (m_SlotManager != null)
                m_SlotManager.OnSpoolAreaStuck -= OnSpoolAreaStuck;
        }

        private void OnFabricCleared()
        {
            if (m_ResultShown) return;
            TryShowWinWhenFabricAndSpoolsDone().Forget();
        }

        /// <summary> İp (fabric) bittiğinde hemen win gösterme; makaralar da bitene kadar bekle (1 makara = 3 ip, hepsi aynı anda bitmeli). </summary>
        private async UniTaskVoid TryShowWinWhenFabricAndSpoolsDone()
        {
            const int maxWaitMs = 3000;
            const int checkIntervalMs = 150;
            int elapsed = 0;
            while (elapsed < maxWaitMs)
            {
                if (m_SlotManager != null && m_SlotManager.SpoolCount == 0)
                    break;
                await UniTask.Delay(checkIntervalMs);
                elapsed += checkIntervalMs;
            }
            if (m_ResultShown) return;
            if (m_SlotManager != null && m_SlotManager.SpoolCount > 0)
                return;
            m_ResultShown = true;
            PlayWinSequenceAsync(m_FlowCts.Token).Forget();
        }

        private async UniTaskVoid PlayWinSequenceAsync(CancellationToken ct)
        {
            if (m_ConfettiPlayer != null)
            {
                await m_ConfettiPlayer.PlayAsync(ct);
            }
            else
            {
                await UniTask.Delay(500, cancellationToken: ct);
            }

            if (ct.IsCancellationRequested) return;
            m_LevelResultUI.ShowSuccess(1);
        }

        private void OnSpoolAreaStuck()
        {
            TriggerFail();
        }

        /// <summary>
        /// Fail (Give Up veya SpoolArea stuck) çağrıldığında.
        /// </summary>
        public void TriggerFail()
        {
            if (m_ResultShown) return;
            m_ResultShown = true;
            m_LevelManager.FailCurrentLevel();
            m_LevelResultUI.ShowFail();
        }
    }
}
