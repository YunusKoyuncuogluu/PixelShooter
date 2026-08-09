using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using RopeWrap.UI;

namespace RopeWrap.Game
{
    /// <summary>
    /// Level kazanma/kaybetme akisini yonetir.
    /// Spool area stuck -> Fail panel.
    /// Fabric temizlenince ConveyorManager uzerinden Win tetiklenir.
    /// </summary>
    public class LevelFlowController : MonoBehaviour
    {
        [Inject] private LevelManager m_LevelManager;
        [Inject] private SlotManager m_SlotManager;
        [Inject] private LevelResultUI m_LevelResultUI;
        [InjectOptional] private ConfettiPlayer m_ConfettiPlayer;

        private bool m_ResultShown;
        private CancellationTokenSource m_FlowCts;

        private void Start()
        {
            m_ResultShown = false;
            m_FlowCts = new CancellationTokenSource();

            if (m_SlotManager != null)
                m_SlotManager.OnSpoolAreaStuck += OnSpoolAreaStuck;
        }

        private void OnDestroy()
        {
            m_FlowCts?.Cancel();
            if (m_SlotManager != null)
                m_SlotManager.OnSpoolAreaStuck -= OnSpoolAreaStuck;
        }

        /// <summary> Fabric clear + conveyor win kosulu saglandiginda cagirilir. </summary>
        public void TriggerWin()
        {
            if (m_ResultShown) return;
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

        public void TriggerFail()
        {
            if (m_ResultShown) return;
            m_ResultShown = true;
            m_LevelManager.FailCurrentLevel();
            m_LevelResultUI.ShowFail();
        }
    }
}
