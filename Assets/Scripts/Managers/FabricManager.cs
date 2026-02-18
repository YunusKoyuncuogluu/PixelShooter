using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Grids;
using LevelManagement;
using UnityEngine;
using Zenject;

public class FabricManager : MonoBehaviour, IInitializable
{
    /// <summary> Tüm örgü bittiğinde (hiç aktif stitch kalmadığında) tetiklenir. </summary>
    public event Action OnFabricCleared;

    /// <summary> En alt satır değiştiğinde tetiklenir (stitch tüketilip kayma bittikten sonra). Slotlardaki makaralar buna göre tekrar kontrol eder. </summary>
    public event Action OnFabricBottomRowChanged;
    [Inject] private LevelManager m_LevelManager;
    [Inject] private CellManager m_CellManager;
    [Inject] private ProjectSettings m_ProjectSettings;
    [Inject] private StitchNode.Factory m_StitchNodeFactory;

    [SerializeField] private Transform m_FabricAnchor;

    private int m_Width;
    private int m_Height;
    private StitchNode[,] m_Nodes;
    private Transform m_Anchor;
    private List<Vector3> m_PositionList;
    private List<Vector2Int> m_CoordinateList;
    private float[] m_ColumnCenterX;
    private HashSet<int> m_ColumnsBeingUnraveled = new HashSet<int>();

    public int Width => m_Width;
    public int Height => m_Height;
    public StitchNode[,] Nodes => m_Nodes;

    public void Initialize()
    {
        ILevelData currentLevelData = m_LevelManager.CurrentLevelData;
        LevelData levelData = currentLevelData as LevelData;

        if (levelData == null)
        {
            Debug.LogWarning("[FabricManager] Current level data is not LevelData. Skipping fabric setup.");
            return;
        }

        m_Width = levelData.StitchWidth;
        m_Height = levelData.StitchHeight;
        m_Anchor = m_FabricAnchor != null ? m_FabricAnchor : m_CellManager.CenterTransform;
        float spaceX = m_ProjectSettings.CellSpaceX;
        float spaceY = m_ProjectSettings.CellSpaceY;

        StitchCellData[,] stitchData = levelData.StitchData != null
            ? ReverseStitchData(levelData.StitchData, m_Width, m_Height)
            : CreateEmptyStitchData(m_Width, m_Height);

        BuildFabricPositionsBottomAnchored(spaceX, spaceY);

        m_ColumnCenterX = new float[m_Width];
        for (int x = 0; x < m_Width; x++)
            m_ColumnCenterX[x] = m_PositionList[x].x;

        m_Nodes = new StitchNode[m_Width, m_Height];

        var facadeProperties = m_ProjectSettings.GetOriginalFacadeProperties();

        for (int i = 0; i < m_PositionList.Count; i++)
        {
            int x = m_CoordinateList[i].x;
            int y = m_CoordinateList[i].y;
            int colorLevel = stitchData[x, y] != null ? stitchData[x, y].ColorLevel : 0;
            GeneralColor color = colorLevel > 0 ? (GeneralColor)colorLevel : GeneralColor.None;
            Vector3 position = m_PositionList[i];
            Vector2Int coordinate = m_CoordinateList[i];

            StitchNode node = m_StitchNodeFactory.Create();
            node.Construct(position, m_Anchor, color, coordinate);

            UnityEngine.Color visualColor = GetFacadeColorForStitch(color, facadeProperties);
            node.SetVisualColor(visualColor);

            m_Nodes[x, y] = node;
        }
    }

    private static UnityEngine.Color GetFacadeColorForStitch(GeneralColor color, System.Collections.Generic.List<FacadeProperty> facadeProperties)
    {
        if (color == GeneralColor.None || facadeProperties == null || facadeProperties.Count == 0)
            return UnityEngine.Color.white;

        int level = (int)color;
        var facade = facadeProperties.Find(f => f.Level == level);
        if (facade != null)
            return facade.Color;

        int index = level - 1;
        if (index >= 0 && index < facadeProperties.Count)
            return facadeProperties[index].Color;

        return UnityEngine.Color.white;
    }

    /// <summary>
    /// Slot'un dünya konumuna göre hangi fabric sütununun altında olduğunu döndürür.
    /// Böylece slot sırası ile fabric sütunları konumsal olarak eşleşir.
    /// </summary>
    public int GetColumnIndexForWorldPosition(Vector3 worldPosition)
    {
        if (m_ColumnCenterX == null || m_Width == 0)
            return 0;

        float x = worldPosition.x;
        int best = 0;
        float bestDist = Mathf.Abs(m_ColumnCenterX[0] - x);
        for (int i = 1; i < m_Width; i++)
        {
            float d = Mathf.Abs(m_ColumnCenterX[i] - x);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Tüm örgüdeki en alt satır indeksini döndürür (en küçük y değerine sahip aktif stitch'in satırı).
    /// Sütunlar aşağı kaydığı için her zaman "en alt satır" güncel kalır.
    /// </summary>
    public int GetBottomRowIndex()
    {
        if (m_Nodes == null || m_Width <= 0 || m_Height <= 0)
            return -1;
        int bottomRow = m_Height;
        for (int col = 0; col < m_Width; col++)
        {
            StitchNode b = GetBottomNode(col);
            if (b != null && b.Row < bottomRow)
                bottomRow = b.Row;
        }
        return bottomRow < m_Height ? bottomRow : -1;
    }

    /// <summary>
    /// Her zaman en alt satıra bakar; o satırda verilen renkte stitch olan ve sökülmeyen bir sütun döndürür.
    /// </summary>
    public int GetColumnWithColorAtBottomRow(GeneralColor color)
    {
        var list = GetColumnsWithColorAtBottomRow(color, 1);
        return list.Count > 0 ? list[0] : -1;
    }

    /// <summary>
    /// Sütunun en altındaki stitch verilen renkte olan tüm sütunları döndürür (en fazla maxCount).
    /// Aynı satır şartı yok: kaç sütunda altta bu renk varsa hepsi döner, hepsi aynı anda sarılır.
    /// </summary>
    public List<int> GetColumnsWithColorAtBottomRow(GeneralColor color, int maxCount)
    {
        var result = new List<int>();
        if (m_Nodes == null || m_Width <= 0 || color == GeneralColor.None || maxCount <= 0)
            return result;

        for (int col = 0; col < m_Width && result.Count < maxCount; col++)
        {
            if (m_ColumnsBeingUnraveled.Contains(col))
                continue;
            StitchNode bottom = GetBottomNode(col);
            if (bottom != null && bottom.Color == color)
                result.Add(col);
        }

        return result;
    }

    /// <summary>
    /// En altta verilen renge sahip ve şu an sökülmeyen bir sütun bulur (eski mantık, geriye dönük uyumluluk).
    /// </summary>
    public int GetColumnWithBottomColor(GeneralColor color)
    {
        return GetColumnWithColorAtBottomRow(color);
    }

    /// <summary> Bu sütun şu an başka bir makara tarafından sökülüyor mu? </summary>
    public bool IsColumnBeingUnraveled(int column)
    {
        return column >= 0 && m_ColumnsBeingUnraveled.Contains(column);
    }

    /// <summary> Bu sütun sökülmeye başlandığında SpoolController tarafından çağrılır. </summary>
    public void NotifyUnravelStarted(int column)
    {
        if (column >= 0 && column < m_Width)
            m_ColumnsBeingUnraveled.Add(column);
    }

    /// <summary> Bu sütunun sökülmesi bittiğinde SpoolController tarafından çağrılır. </summary>
    public void NotifyUnravelEnded(int column)
    {
        if (column >= 0)
            m_ColumnsBeingUnraveled.Remove(column);
    }

    /// <summary>
    /// Returns the bottommost active StitchNode in the given column (column = x, bottom = smallest y).
    /// </summary>
    public StitchNode GetBottomNode(int column)
    {
        if (m_Nodes == null || column < 0 || column >= m_Width)
            return null;

        for (int y = 0; y < m_Height; y++)
        {
            StitchNode node = m_Nodes[column, y];
            if (node != null && node.IsActive)
                return node;
        }

        return null;
    }

    /// <summary>
    /// Sütundaki en alttaki aktif node'u tüketir (devre dışı bırakır), sonra o sütundaki diğer node'ları bir sıra aşağı kaydırır (animasyonlu).
    /// </summary>
    /// <returns>Tüketilen node, yoksa null.</returns>
    public StitchNode ConsumeBottomNode(int column)
    {
        StitchNode bottom = GetBottomNode(column);
        if (bottom != null)
        {
            int consumedRow = bottom.Row;
            bottom.SetActive(false);
            ShiftColumnDownAsync(column, consumedRow).Forget();
            if (IsFabricCleared())
                OnFabricCleared?.Invoke();
        }
        return bottom;
    }

    /// <summary>
    /// Async: Sütundaki en alttaki node'u tüketir, sonra üsttekileri bir sıra aşağı kaydırır (Lerp/DOTween). Await edilirse sıra sonraki consume'a kayar.
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<StitchNode> ConsumeBottomNodeAsync(int column)
    {
        StitchNode bottom = GetBottomNode(column);
        if (bottom == null)
            return null;

        int consumedRow = bottom.Row;
        bottom.SetActive(false);
        await ShiftColumnDownAsync(column, consumedRow);
        if (IsFabricCleared())
            OnFabricCleared?.Invoke();
        OnFabricBottomRowChanged?.Invoke();
        return bottom;
    }

    /// <summary>
    /// Sütundaki node'ları tüketilen satırdan itibaren bir sıra aşağı kaydırır (ProjectSettings.StitchShiftDownDuration süresinde animasyon).
    /// </summary>
    private async Cysharp.Threading.Tasks.UniTask ShiftColumnDownAsync(int column, int consumedRow)
    {
        if (m_Nodes == null || m_PositionList == null || column < 0 || column >= m_Width || m_Height <= 1)
            return;

        float duration = (m_ProjectSettings != null) ? m_ProjectSettings.StitchShiftDownDuration : 0.25f;
        if (duration <= 0f)
        {
            ShiftColumnDownImmediate(column, consumedRow);
            return;
        }

        var tweens = new List<Tween>();
        for (int row = consumedRow; row < m_Height - 1; row++)
        {
            StitchNode nodeAbove = m_Nodes[column, row + 1];
            if (nodeAbove != null && nodeAbove.IsActive)
            {
                int targetRowIndex = row - consumedRow;
                Vector3 targetPos = m_PositionList[targetRowIndex * m_Width + column];
                Tween t = nodeAbove.transform.DOMove(targetPos, duration).SetEase(Ease.Linear);
                tweens.Add(t);
            }
        }

        if (tweens.Count == 0)
            return;

        var tasks = new List<Cysharp.Threading.Tasks.UniTask>();
        foreach (var t in tweens)
            tasks.Add(t.AsyncWaitForCompletion().AsUniTask());
        await Cysharp.Threading.Tasks.UniTask.WhenAll(tasks);
    }

    private void ShiftColumnDownImmediate(int column, int consumedRow)
    {
        if (m_Nodes == null || m_PositionList == null || column < 0 || column >= m_Width || m_Height <= 1)
            return;

        for (int row = consumedRow; row < m_Height - 1; row++)
        {
            StitchNode nodeAbove = m_Nodes[column, row + 1];
            if (nodeAbove != null && nodeAbove.IsActive)
            {
                int targetRowIndex = row - consumedRow;
                Vector3 targetPos = m_PositionList[targetRowIndex * m_Width + column];
                nodeAbove.transform.position = targetPos;
            }
        }
    }

    /// <summary> Hiç aktif stitch node kalmadıysa true. </summary>
    public bool IsFabricCleared()
    {
        if (m_Nodes == null) return true;
        for (int x = 0; x < m_Width; x++)
        for (int y = 0; y < m_Height; y++)
            if (m_Nodes[x, y] != null && m_Nodes[x, y].IsActive)
                return false;
        return true;
    }

    public StitchNode GetNode(int column, int row)
    {
        if (m_Nodes == null || column < 0 || column >= m_Width || row < 0 || row >= m_Height)
            return null;
        return m_Nodes[column, row];
    }

    /// <summary>
    /// Fabric anchor grid'in en alt ortasında kalır: y=0 alt satır, yukarı doğru artar. Yatayda ortalanır.
    /// </summary>
    private void BuildFabricPositionsBottomAnchored(float spaceX, float spaceY)
    {
        m_PositionList = new List<Vector3>(m_Width * m_Height);
        m_CoordinateList = new List<Vector2Int>(m_Width * m_Height);
        for (int y = 0; y < m_Height; y++)
        {
            for (int x = 0; x < m_Width; x++)
            {
                float localX = (x + spaceX * x) - (m_Width + spaceX * (m_Width - 1)) / 2f + 0.5f;
                float localZ = y + spaceY * y;
                Vector3 localPos = new Vector3(localX, 0f, localZ);
                m_PositionList.Add(m_Anchor.TransformPoint(localPos));
                m_CoordinateList.Add(new Vector2Int(x, y));
            }
        }
    }

    private static StitchCellData[,] ReverseStitchData(StitchCellData[,] stitchData, int width, int height)
    {
        var reversed = new StitchCellData[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                reversed[x, y] = stitchData[x, height - 1 - y];
        }
        return reversed;
    }

    /// <summary>
    /// StitchData yoksa (eski level) boş örgü grid'i – tüm hücreler ColorLevel 0.
    /// </summary>
    private static StitchCellData[,] CreateEmptyStitchData(int width, int height)
    {
        var data = new StitchCellData[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            data[x, y] = new StitchCellData();
        return data;
    }
}
