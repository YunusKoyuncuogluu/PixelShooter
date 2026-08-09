using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;
using Zenject;

/// <summary>
/// Makara: kapali conveyor spline uzerinde DURMADAN gezer (octopus multi-wind).
/// Ayni anda birden fazla ip atabilir; kapasite asimini inProgressWinds ile onler.
/// </summary>
public class SpoolController : MonoBehaviour
{
    [Inject] private FabricManager fabricManager;
    [InjectOptional] private ConveyorManager conveyorManager;
    [InjectOptional] private PlateQueueManager plateQueueManager;
    [Inject] private SlotManager slotManager;

    [Header("Capacity")]
    [SerializeField] private int maxCapacity = 10;

    [Header("Search / Fire")]
    [SerializeField] private float searchRadius = 4f;
    [SerializeField] private float fireCooldown = 0.55f;
    [SerializeField] private float windDuration = 0.7f;
    [SerializeField] private float minOrbitDuration = 1.8f;
    [Tooltip("Hizali serit atlantiysa aradaki pikselleri yakalamak icin kisa bekleme.")]
    [SerializeField] private float laneCatchUpCooldown = 0.08f;

    [Header("Visual Thread")]
    [SerializeField] private Material threadMaterial;
    [SerializeField] private float threadWidth = 0.05f;

    [Header("Fill UI")]
    [SerializeField] private SpoolFillUI spoolFillUI;

    [Header("Disappear")]
    [SerializeField] private float disappearDuration = 0.35f;

    private Color32 spoolColor;
    private short spoolPaletteId = -1;
    private int currentFill;
    /// <summary>
    /// Ucusta olan ipler. Atis sarti: (currentFill + inProgressWinds) &lt; maxCapacity.
    /// </summary>
    private int inProgressWinds;
    private bool isOrbitRoutine;
    private bool orbitCompleted;
    private bool isOnConveyor;
    /// <summary> Slot'ta plate bekliyor veya boarding animasyonunda. </summary>
    private bool isAwaitingDeparture;
    private float conveyorEnterTime;
    private float nextFireTime;
    /// <summary> Son basarili claim sutun/satiri; atlanan seritleri doldurmak icin. </summary>
    private int lastClaimedLane = int.MinValue;
    private int lastApproachSide = int.MinValue;

    private SplineFollower cachedFollower;
    private CancellationTokenSource travelCts;
    private SpoolFillUI cachedFillUI;
    private GeneralColor generalColor;

    private LineRenderer[] windLinePool;
    private bool[] windLineInUse;
    private Material runtimeLineMaterial;

    public int MaxCapacity => Mathf.Max(1, maxCapacity);
    public SplineFollower CachedFollower => cachedFollower;

    /// <summary>
    /// Plate hazirsa hemen boarding baslatir (tiklama aninda senkron animasyon).
    /// Hazir degilse false — caller slot animasyonuna duser.
    /// </summary>
    public bool TryBeginDeparture(Slot slot, GeneralColor spoolGeneralColor)
    {
        if (isOrbitRoutine || isOnConveyor || isAwaitingDeparture)
            return false;

        if (fabricManager == null || conveyorManager == null)
            return false;

        if (plateQueueManager == null || !plateQueueManager.HasAvailablePlate)
            return false;

        generalColor = spoolGeneralColor;
        spoolColor = fabricManager.ResolveSpoolColor(spoolGeneralColor);
        spoolPaletteId = fabricManager.ResolvePaletteId(spoolGeneralColor);

        if (spoolPaletteId < 0)
            return false;

        if (currentFill <= 0)
        {
            currentFill = 0;
            inProgressWinds = 0;
            ShowFillUI(true);
            SpoolFillUI ui = GetFillUI();
            if (ui != null)
                ui.SetTargetPercent(0f);
        }

        isAwaitingDeparture = true;
        StartUnravelAsync(slot).Forget();
        return true;
    }

    /// <summary>
    /// Slot yerlesim animasyonu bitince Thread tarafindan cagirilir.
    /// Plate yoksa slotta bekler; plate gelince boarding + conveyor.
    /// </summary>
    public void TryStartUnravel(Slot slot, GeneralColor spoolGeneralColor)
    {
        if (isOrbitRoutine || isOnConveyor || isAwaitingDeparture)
            return;

        if (fabricManager == null || conveyorManager == null)
            return;

        generalColor = spoolGeneralColor;
        spoolColor = fabricManager.ResolveSpoolColor(spoolGeneralColor);
        spoolPaletteId = fabricManager.ResolvePaletteId(spoolGeneralColor);

        if (spoolPaletteId < 0)
        {
            Debug.LogWarning(
                $"[SpoolController] GeneralColor {spoolGeneralColor} facade palette'te yok. Sarma atlanir.");
            return;
        }

        if (fabricManager.OpaqueRemaining <= 0)
        {
            Debug.LogError(
                "[SpoolController] Fabric'de opak piksel yok. LevelData > Fabric Pixel bake edin.");
        }

        if (currentFill <= 0)
        {
            currentFill = 0;
            inProgressWinds = 0;
            ShowFillUI(true);
            SpoolFillUI ui = GetFillUI();
            if (ui != null)
                ui.SetTargetPercent(0f);
        }

        isAwaitingDeparture = true;
        StartUnravelAsync(slot).Forget();
    }

    private async UniTaskVoid StartUnravelAsync(Slot slot)
    {
        CancellationToken ct = this.GetCancellationTokenOnDestroy();

        try
        {
            if (plateQueueManager != null)
            {
                while (!ct.IsCancellationRequested)
                {
                    while (!plateQueueManager.HasAvailablePlate && !ct.IsCancellationRequested)
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);

                    if (ct.IsCancellationRequested || this == null)
                        return;

                    bool boarded = await plateQueueManager.TryBoardSpoolAsync(this, ct);
                    if (boarded)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            if (this == null || conveyorManager == null || ct.IsCancellationRequested)
                return;

            Thread thread = GetComponent<Thread>();
            if (thread != null)
                thread.ClearBusy();

            conveyorManager.SendSpoolToConveyor(this, slot);
        }
        finally
        {
            isAwaitingDeparture = false;
        }
    }

    public SplineFollower EnsureFollower()
    {
        if (cachedFollower == null)
            cachedFollower = GetComponent<SplineFollower>();
        if (cachedFollower == null)
            cachedFollower = gameObject.AddComponent<SplineFollower>();
        return cachedFollower;
    }

    public void SetTravelFollower(SplineFollower follower)
    {
        cachedFollower = follower;
    }

    public void ClearTravelFollower()
    {
        cachedFollower = null;
    }

    public void InvalidateFollowerCache()
    {
        cachedFollower = null;
    }

    public void ReleaseOriginSlot()
    {
        isOnConveyor = true;
    }

    /// <summary>
    /// Plate (veya fallback spool) follower baglandiktan sonra orbit + sarma.
    /// </summary>
    public async UniTaskVoid BeginConveyorTravel(ConveyorManager owner)
    {
        if (owner == null)
            return;

        travelCts?.Cancel();
        travelCts = new CancellationTokenSource();
        CancellationToken ct = travelCts.Token;

        orbitCompleted = false;
        isOrbitRoutine = true;
        isOnConveyor = true;
        conveyorEnterTime = Time.time;
        nextFireTime = 0f;

        EnsureWindLinePool();

        SplineFollower follower = cachedFollower;
        if (follower == null)
            follower = EnsureFollower();

        follower.onEndReached -= HandleOrbitEndReached;
        follower.onEndReached += HandleOrbitEndReached;
        follower.follow = true;

        bool finishedCleanly = false;
        try
        {
            await TravelOrbitRoutine(ct);
            finishedCleanly = !ct.IsCancellationRequested;
        }
        catch (System.OperationCanceledException)
        {
            finishedCleanly = false;
        }
        finally
        {
            isOrbitRoutine = false;
            isOnConveyor = false;
            if (follower != null)
                follower.follow = false;
            ReleaseAllWindLines();
        }

        if (!finishedCleanly || this == null || gameObject == null)
            return;

        bool completedFull = currentFill >= MaxCapacity ||
                             (fabricManager != null && fabricManager.IsFabricCleared());
        owner.HandleSpoolFinished(this, completedFull);
    }

    /// <summary>
    /// Conveyor uzerinde continuous orbit: kapasite dolana veya renk bitene kadar sarar.
    /// Follower durmaz; fireCooldown / laneCatchUp ile atis frekansi kontrol edilir.
    /// </summary>
    private async UniTask TravelOrbitRoutine(CancellationToken ct)
    {
        float radius = Mathf.Max(0.01f, searchRadius);
        float cooldown = fireCooldown > 0f ? fireCooldown : 0.1f;
        float catchUpCooldown = laneCatchUpCooldown > 0f ? laneCatchUpCooldown : 0.05f;
        int capacity = MaxCapacity;
        lastClaimedLane = int.MinValue;
        lastApproachSide = int.MinValue;

        while (!ct.IsCancellationRequested)
        {
            if (currentFill >= capacity)
                break;

            if (orbitCompleted && inProgressWinds <= 0)
                break;

            Vector3 spoolPos = transform.position;
            int approachSide = fabricManager.ResolveApproachSideIndex(spoolPos);
            if (approachSide != lastApproachSide)
            {
                lastApproachSide = approachSide;
                lastClaimedLane = int.MinValue;
            }

            int preferredLane = fabricManager.ResolvePreferredLane(spoolPos);
            bool canFire = !orbitCompleted &&
                           (currentFill + inProgressWinds) < capacity &&
                           Time.time >= nextFireTime;

            if (canFire)
            {
                if (fabricManager.TryClaimClosestPixelOfColor(
                        spoolPos,
                        spoolPaletteId,
                        radius,
                        lastClaimedLane,
                        out _,
                        out Vector3 pixelWorldPos,
                        out int claimedLane))
                {
                    lastClaimedLane = claimedLane;
                    inProgressWinds++;
                    bool catchingUp = claimedLane != preferredLane;
                    nextFireTime = Time.time + (catchingUp ? catchUpCooldown : cooldown);
                    UpdateFillUIPercent();
                    WindPixelAsync(pixelWorldPos, ct).Forget();
                }
                else if (inProgressWinds <= 0 &&
                         (Time.frameCount & 31) == 0 &&
                         !fabricManager.HasRemainingPixelsOfPaletteId(spoolPaletteId))
                {
                    orbitCompleted = true;
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        while (inProgressWinds > 0 && !ct.IsCancellationRequested)
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }

    /// <summary>
    /// Gorsel ip animasyonu (hareket eden spool -> sabit piksel).
    /// Piksel zaten claim edilmis; bitiste sadece sayaclari gunceller.
    /// </summary>
    private async UniTask WindPixelAsync(Vector3 pixelWorldPos, CancellationToken ct)
    {
        int lineIndex = -1;
        LineRenderer line = null;

        try
        {
            lineIndex = AcquireWindLine();
            if (lineIndex >= 0)
                line = windLinePool[lineIndex];

            float duration = Mathf.Max(0.01f, windDuration);
            float elapsed = 0f;

            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                if (line != null)
                {
                    line.SetPosition(0, transform.position);
                    line.SetPosition(1, pixelWorldPos);
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (ct.IsCancellationRequested)
                return;

            currentFill++;
            if (currentFill > MaxCapacity)
                currentFill = MaxCapacity;

            UpdateFillUIPercent();
        }
        finally
        {
            if (inProgressWinds > 0)
                inProgressWinds--;

            if (lineIndex >= 0)
                ReleaseWindLine(lineIndex);
        }
    }

    /// <summary> Dreamteck: her tam tur (Loop wrap). </summary>
    public void HandleOrbitEndReached(double _)
    {
        if (Time.time - conveyorEnterTime < minOrbitDuration)
            return;

        if (currentFill + inProgressWinds >= MaxCapacity)
        {
            orbitCompleted = true;
            return;
        }

        // Renk kaldiysa donmeye devam; yoksa turu bitir.
        if (fabricManager == null ||
            !fabricManager.HasRemainingPixelsOfPaletteId(spoolPaletteId))
        {
            orbitCompleted = true;
        }
    }

    public void OnReturnedToSlot(Slot slot)
    {
        isOnConveyor = false;
        TryReenterConveyorAsync(slot).Forget();
    }

    /// <summary>
    /// Slot'a dondikten sonra eslesen piksel varsa tekrar conveyor'a girer.
    /// </summary>
    private async UniTaskVoid TryReenterConveyorAsync(Slot slot)
    {
        await UniTask.Yield(PlayerLoopTiming.Update);
        if (this == null || slot == null || isOnConveyor || isOrbitRoutine || isAwaitingDeparture)
            return;

        if (currentFill >= MaxCapacity)
            return;

        if (fabricManager == null || !fabricManager.HasRemainingPixelsOfPaletteId(spoolPaletteId))
            return;

        TryStartUnravel(slot, generalColor);
    }

    public void NotifyReturnedWithoutSlot()
    {
        isOnConveyor = false;
        ShowFillUI(false);
    }

    public async UniTask PlayCompleteAndDestroyAsync()
    {
        ShowFillUI(false);
        ReleaseAllWindLines();

        Thread threadRef = GetComponent<Thread>();
        SlotManager slotManagerRef = slotManager;
        GameObject go = gameObject;

        float duration = Mathf.Max(0.01f, disappearDuration);
        Tween disappearTween = transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);
        await disappearTween.AsyncWaitForCompletion().AsUniTask();

        if (slotManagerRef != null && threadRef != null)
            slotManagerRef.RemoveThread(threadRef);

        if (go != null)
            Destroy(go);
    }

    private void UpdateFillUIPercent()
    {
        // UI: commit + rezervasyon (oyuncu 10/10'a ne kadar yakin oldugunu gorsun).
        float committed = currentFill + inProgressWinds;
        float percent = (committed / (float)MaxCapacity) * 100f;
        SpoolFillUI ui = GetFillUI();
        if (ui != null)
            ui.SetTargetPercent(percent, fireCooldown);
    }

    private SpoolFillUI GetFillUI()
    {
        if (cachedFillUI != null)
            return cachedFillUI;
        cachedFillUI = spoolFillUI != null ? spoolFillUI : GetComponentInChildren<SpoolFillUI>(true);
        return cachedFillUI;
    }

    private void ShowFillUI(bool show)
    {
        SpoolFillUI ui = GetFillUI();
        if (ui != null)
            ui.gameObject.SetActive(show);
    }

    private void EnsureWindLinePool()
    {
        int size = MaxCapacity;
        if (size < 1)
            size = 10;

        if (windLinePool != null && windLinePool.Length >= size)
            return;

        if (runtimeLineMaterial == null)
        {
            if (threadMaterial != null)
                runtimeLineMaterial = threadMaterial;
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    runtimeLineMaterial = new Material(shader);
            }
        }

        windLinePool = new LineRenderer[size];
        windLineInUse = new bool[size];

        for (int i = 0; i < size; i++)
        {
            GameObject lineGo = new GameObject("WindThreadLine_" + i);
            lineGo.transform.SetParent(transform, false);
            LineRenderer line = lineGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = threadWidth;
            line.endWidth = threadWidth * 0.7f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            if (runtimeLineMaterial != null)
                line.sharedMaterial = runtimeLineMaterial;

            Color c = spoolColor;
            line.startColor = c;
            line.endColor = c;
            line.enabled = false;
            windLinePool[i] = line;
            windLineInUse[i] = false;
        }
    }

    private int AcquireWindLine()
    {
        if (windLinePool == null)
            return -1;

        for (int i = 0; i < windLinePool.Length; i++)
        {
            if (windLineInUse[i])
                continue;

            windLineInUse[i] = true;
            LineRenderer line = windLinePool[i];
            line.enabled = true;
            Color c = spoolColor;
            line.startColor = c;
            line.endColor = c;
            return i;
        }

        return -1;
    }

    private void ReleaseWindLine(int index)
    {
        if (windLinePool == null || index < 0 || index >= windLinePool.Length)
            return;

        windLineInUse[index] = false;
        LineRenderer line = windLinePool[index];
        if (line != null)
            line.enabled = false;
    }

    private void ReleaseAllWindLines()
    {
        if (windLinePool == null)
            return;

        for (int i = 0; i < windLinePool.Length; i++)
            ReleaseWindLine(i);
    }

    private void OnDestroy()
    {
        travelCts?.Cancel();
        if (cachedFollower != null)
            cachedFollower.onEndReached -= HandleOrbitEndReached;
        ReleaseAllWindLines();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawSearchRadiusGizmo(selected: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawSearchRadiusGizmo(selected: true);
    }

    /// <summary>
    /// Arama XZ duzleminde; wire sphere + XZ daire.
    /// </summary>
    private void DrawSearchRadiusGizmo(bool selected)
    {
        float radius = Mathf.Max(0.01f, searchRadius);
        Vector3 center = transform.position;

        Color sphereColor = selected
            ? new Color(1f, 0.25f, 0.1f, 0.95f)
            : new Color(1f, 0.45f, 0.15f, 0.55f);
        Gizmos.color = sphereColor;
        Gizmos.DrawWireSphere(center, radius);

        Gizmos.color = selected
            ? new Color(1f, 0.85f, 0.15f, 1f)
            : new Color(1f, 0.75f, 0.2f, 0.7f);
        DrawGizmoCircleXZ(center, radius, 48);
    }

    private void DrawGizmoCircleXZ(Vector3 center, float radius, int segments)
    {
        if (segments < 8)
            segments = 8;

        float step = (Mathf.PI * 2f) / segments;
        Vector3 prev = new Vector3(center.x + radius, center.y, center.z);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector3 next = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y,
                center.z + Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}

