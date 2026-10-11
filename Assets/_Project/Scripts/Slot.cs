using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

public class Slot : MonoBehaviour
{
    [FormerlySerializedAs("m_LockedTransform")]
    [SerializeField] private Transform lockedTransform;

    private bool isLocked;
    private bool isFull;

    public bool IsLocked => this.isLocked;
    public bool IsFull => this.isFull;

    private Transform cachedTransform;

    public void Construct(Vector3 localPosition)
    {
        cachedTransform = transform;
        cachedTransform.localPosition = localPosition;
        lockedTransform.gameObject.SetActive(this.isLocked);
    }

    public void SetLocked(bool isLocked) => this.isLocked = isLocked;
    public void SetFull(bool isFull) => this.isFull = isFull;

    public class Factory : PlaceholderFactory<Slot>
    {
    }
}
