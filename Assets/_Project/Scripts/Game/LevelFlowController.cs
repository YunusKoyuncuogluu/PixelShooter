using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using RopeWrap.UI;

namespace RopeWrap.Game
{
    public class LevelFlowController : MonoBehaviour
    {
        [Inject] private LevelManager levelManager;
        [Inject] private SlotManager slotManager;
        [Inject] private LevelResultUI levelResultUI;
        [InjectOptional] private ConfettiPlayer confettiPlayer;

        private bool resultShown;
        private CancellationTokenSource flowCts;

        [InjectOptional] private WinFastForward winFastForward;

        public bool IsResultShown => resultShown;

        private void Start()
        {
            resultShown = false;
            flowCts = new CancellationTokenSource();

            if (slotManager != null)
                slotManager.OnShooterAreaStuck += OnShooterAreaStuck;
        }

        private void OnDestroy()
        {
            flowCts?.Cancel();
            if (slotManager != null)
                slotManager.OnShooterAreaStuck -= OnShooterAreaStuck;
        }

        public void TriggerWin()
        {
            if (resultShown)
                return;

            if (winFastForward != null)
                winFastForward.Stop();

            resultShown = true;
            PlayWinSequenceAsync(flowCts.Token).Forget();
        }

        private async UniTaskVoid PlayWinSequenceAsync(CancellationToken ct)
        {
            if (confettiPlayer != null)
            {
                await confettiPlayer.PlayAsync(ct);
            }
            else
            {
                await UniTask.Delay(500, cancellationToken: ct);
            }

            if (ct.IsCancellationRequested)
                return;
            levelResultUI.ShowSuccess(1);
        }

        private void OnShooterAreaStuck()
        {
            TriggerFail();
        }

        public void TriggerFail()
        {
            if (resultShown)
                return;

            if (winFastForward != null)
                winFastForward.Stop();

            resultShown = true;
            levelManager.FailCurrentLevel();
            levelResultUI.ShowFail();
        }
    }
}
