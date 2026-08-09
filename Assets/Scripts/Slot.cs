using UnityEngine;
using Zenject;

public class Slot : MonoBehaviour
{
    [SerializeField] private Transform m_LockedTransform;

    private bool m_IsLocked;
    private bool m_IsFull;
    private int m_ColumnIndex;

    public bool IsLocked => m_IsLocked;
    public bool IsFull => m_IsFull;
    public int ColumnIndex => m_ColumnIndex;

    private Transform cachedTransform;

    public void Construct(Vector3 localPosition, int columnIndex)
    {
        cachedTransform = transform;
        m_ColumnIndex = columnIndex;
        cachedTransform.localPosition = localPosition;
        m_LockedTransform.gameObject.SetActive(m_IsLocked);
    }

    public void SetLocked(bool isLocked) => m_IsLocked = isLocked;
    public void SetFull(bool isFull) => m_IsFull = isFull;

    public class Factory : PlaceholderFactory<Slot>
    {
    }
}
