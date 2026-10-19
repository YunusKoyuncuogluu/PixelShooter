using System;
using PixelShooter.UI;
using UnityEngine;
using Zenject;

namespace PixelShooter.Game
{
    public interface IFastForwardGate
    {
        bool IsActive { get; }
    }

    public class WinFastForward : ITickable, IDisposable, IFastForwardGate
    {
        private const int PaletteCapacity = 32;
        private const int SlotReserve = 2;
        private const float StartTimeScale = 1f;
        private const float MaxTimeScale = 4f;
        private const float RampDuration = 8f;

        private readonly int[] pixelCounts = new int[PaletteCapacity];
        private readonly int[] ammoCounts = new int[PaletteCapacity];

        [Inject] private LazyInject<ConveyorManager> conveyorManager;
        [Inject] private ImageManager imageManager;
        [Inject] private ShooterManager shooterManager;
        [Inject] private SlotManager slotManager;
        [Inject] private TunnelManager tunnelManager;
        [Inject] private global::Game.PlayerController playerController;
        [InjectOptional] private LevelFlowController levelFlowController;
        [InjectOptional] private UIManager uiManager;

        private bool active;
        private float rampElapsed;

        public bool IsActive => active;

        public void Tick()
        {
            if (levelFlowController != null && levelFlowController.IsResultShown)
            {
                Stop();
                return;
            }

            if (active)
            {
                float step = Time.unscaledDeltaTime;
                if (step > 0.05f)
                    step = 0.05f;

                rampElapsed += step;
                ApplySpeed();
                DispatchParkedShooters();
                DispatchGridShooters();
                return;
            }

            if (!IsGuaranteed())
                return;

            Begin();
        }

        public void Stop()
        {
            if (!active)
                return;

            active = false;
            rampElapsed = 0f;

            if (levelFlowController == null || !levelFlowController.IsResultShown)
                Time.timeScale = 1f;
        }

        public void Dispose()
        {
            Stop();
        }

        private void Begin()
        {
            active = true;
            rampElapsed = 0f;

            if (playerController != null)
                playerController.SetActive(false);

            if (uiManager != null)
                uiManager.LockGameplay();

            ApplySpeed();
            DispatchParkedShooters();
            DispatchGridShooters();
        }

        private void ApplySpeed()
        {
            float progress = RampDuration > 0f ? rampElapsed / RampDuration : 1f;
            if (progress > 1f)
                progress = 1f;

            float shaped = progress * progress;
            Time.timeScale = Mathf.Lerp(StartTimeScale, MaxTimeScale, shaped);
        }

        private bool IsGuaranteed()
        {
            ConveyorManager conveyor = conveyorManager.Value;

            if (conveyor == null || imageManager == null || imageManager.IsImageCleared())
                return false;

            int total = imageManager.CountUnreservedPixels(pixelCounts);

            if (total < 0)
                return false;

            ClearAmmo();
            AddConveyorAmmo(conveyor);

            bool beltCovers = conveyor.ActiveShooterCount > 0 && CoversPixels();

            bool tunnelsPending = tunnelManager != null && tunnelManager.HasPendingShooters();
            int room = slotManager != null ? slotManager.CountFreeSlots() : 0;
            int gridCount = shooterManager != null ? shooterManager.CountGridShooters() : 0;
            bool gridFits = shooterManager != null && !tunnelsPending && gridCount > 0 && gridCount + SlotReserve <= room;

            if (gridFits)
                AddGridShooterAmmo();

            if (slotManager != null)
            {
                AddOccupantAmmo(slotManager.OccupantCount, true);
                AddOccupantAmmo(slotManager.QueuedCount, false);
            }

            if (beltCovers)
                return true;

            if (gridFits && CoversPixels())
                return true;

            if (gridCount > 0 || tunnelsPending)
                return false;

            int committed = conveyor.ActiveShooterCount;

            if (slotManager != null)
                committed += slotManager.OccupantCount + slotManager.QueuedCount;

            if (committed <= 0)
                return false;

            return CoversPixels();
        }

        private void ClearAmmo()
        {
            int bucketCount = ammoCounts.Length;
            for (int i = 0; i < bucketCount; i++)
                ammoCounts[i] = 0;
        }

        private void AddConveyorAmmo(ConveyorManager conveyor)
        {
            int shooterCount = conveyor.ActiveShooterCount;
            for (int i = 0; i < shooterCount; i++)
            {
                ShooterController controller = conveyor.GetActiveShooter(i);
                Shooter shooter = controller != null ? controller.Owner : null;
                GeneralColor fallback = shooter != null ? shooter.Color : GeneralColor.None;
                AddControllerAmmo(controller, fallback);
            }
        }

        private void AddOccupantAmmo(int count, bool occupants)
        {
            for (int i = 0; i < count; i++)
            {
                Shooter shooter = occupants ? slotManager.GetOccupant(i) : slotManager.GetQueued(i);

                if (shooter == null)
                    continue;

                AddControllerAmmo(shooter.Controller, shooter.Color);
            }
        }

        private void AddControllerAmmo(ShooterController shooter, GeneralColor fallbackColor)
        {
            if (shooter == null)
                return;

            int shots = shooter.RemainingShots;
            int paletteId = shooter.PaletteId;
            if (paletteId <= 0 && imageManager != null)
                paletteId = imageManager.ResolvePaletteId(fallbackColor);

            if (shots <= 0 || paletteId <= 0 || paletteId >= ammoCounts.Length)
                return;

            ammoCounts[paletteId] += shots;
        }

        private bool CoversPixels()
        {
            int bucketCount = ammoCounts.Length;

            for (int i = 0; i < bucketCount; i++)
            {
                if (pixelCounts[i] > ammoCounts[i])
                    return false;
            }

            return true;
        }

        private void AddGridShooterAmmo()
        {
            int gridWidth = shooterManager.GridWidth;
            int gridHeight = shooterManager.GridHeight;
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Shooter shooter = shooterManager.ShooterAt(new Vector2Int(x, y));

                    if (shooter == null)
                        continue;

                    ShooterController controller = shooter.Controller;
                    int shots = controller != null ? controller.RemainingShots : 0;
                    int paletteId = imageManager.ResolvePaletteId(shooter.Color);

                    if (shots <= 0 || paletteId <= 0 || paletteId >= ammoCounts.Length)
                        continue;

                    ammoCounts[paletteId] += shots;
                }
            }
        }

        private void DispatchGridShooters()
        {
            if (shooterManager == null || slotManager == null)
                return;

            if (tunnelManager != null && tunnelManager.HasPendingShooters())
                return;

            int room = slotManager.CountFreeSlots();
            int gridCount = shooterManager.CountGridShooters();

            if (gridCount <= 0 || gridCount + SlotReserve > room)
                return;

            shooterManager.SendFittingShooters(room - SlotReserve);
        }

        private void DispatchParkedShooters()
        {
            if (slotManager == null || imageManager == null)
                return;

            int count = slotManager.OccupantCount;

            for (int i = 0; i < count; i++)
            {
                Shooter shooter = slotManager.GetOccupant(i);
                if (shooter == null || !shooter.IsHeldInSlot)
                    continue;

                ShooterController controller = shooter.Controller;
                if (controller == null || controller.RemainingShots <= 0)
                    continue;

                short paletteId = controller.PaletteId;
                if (paletteId <= 0)
                    paletteId = imageManager.ResolvePaletteId(shooter.Color);

                if (!imageManager.HasClaimablePixel(paletteId))
                    continue;

                controller.BindPalette(shooter.Color);
                shooter.ContinueFromSlot();
            }
        }
    }
}
