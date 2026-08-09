using UnityEngine;
using Zenject;

public class Cell : MonoBehaviour
{
    [SerializeField] private Transform m_LockedTransform;
    [SerializeField] private Transform m_CellVisual;

    private Vector2Int m_Coordinate;
    public Vector2Int Coordinate => m_Coordinate;

    private bool m_IsLocked;
    public bool IsLocked => m_IsLocked;

    private bool m_IsFull;
    public bool IsFull => m_IsFull;

    private Transform cachedTransform;

    public void Construct(Vector3 localPosition, Vector2Int coordinate, Transform parent, bool isLocked)
    {
        SetLocked(isLocked);
        m_Coordinate = coordinate;
        cachedTransform = transform;
        cachedTransform.SetParent(parent);
        cachedTransform.localPosition = localPosition;
    }

    private void SetLocked(bool isLocked)
    {
        m_IsLocked = isLocked;
        m_IsFull = isLocked || m_IsFull;
        m_LockedTransform.gameObject.SetActive(isLocked);
        m_CellVisual.gameObject.SetActive(!isLocked);
    }

    public void SetFull(bool isFull)
    {
        m_IsFull = isFull;
    }

    public class Factory : PlaceholderFactory<Cell>
    {
    }
}
