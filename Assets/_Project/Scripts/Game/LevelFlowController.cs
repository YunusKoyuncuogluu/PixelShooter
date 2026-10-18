using PixelShooter.Pooling;
using UnityEngine;
using Zenject;
using PixelShooter.UI;

namespace PixelShooter.Game
{
    public class LevelFlowController : MonoBehaviour
    {
        [Inject] private SlotManager slotManager;
        [Inject] private UIManager uiManager;
        [Inject] private global::Game.PlayerController playerController;
        [Inject] private GameAudio gameAudio;

        private bool resultShown;

        [InjectOptional] private WinFastForward winFastForward;

        public bool IsResultShown => resultShown;
        public event System.Action GameplayStopped;

        private void Start()
        {
            resultShown = false;

            if (slotManager != null)
                slotManager.OnShooterAreaStuck += OnShooterAreaStuck;

            if (uiManager != null)
                uiManager.ShowGame();
        }

        private void OnDestroy()
        {
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

            if (gameAudio != null)
                gameAudio.PlayLevelWin();

            StopGameplay(false);

            if (uiManager != null)
                uiManager.ShowCompleted();
        }

        private void OnShooterAreaStuck()
        {
            TriggerFail();
        }

        public void TriggerFail()
        {
            if (resultShown)
                return;

            resultShown = true;

            if (winFastForward != null)
                winFastForward.Stop();

            if (gameAudio != null)
                gameAudio.PlayLevelFail();

            StopGameplay(true);

            if (uiManager != null)
                uiManager.ShowFail();
        }

        private void StopGameplay(bool freezeTime)
        {
            if (playerController != null)
                playerController.SetActive(false);

            if (slotManager != null)
                slotManager.Stop();

            if (freezeTime)
                Time.timeScale = 0f;

            GameplayStopped?.Invoke();
        }
    }
}
