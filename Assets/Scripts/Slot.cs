using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class Slot : MonoBehaviour
{
    [SerializeField] private Transform m_LockedTransform;

    private bool m_IsLocked;
    private bool m_IsFull;

    public bool IsLocked => m_IsLocked;
    public bool IsFull => m_IsFull;

    private Transform m_Transform;


    public void Construct(Vector3 localPosition)
    {
        m_Transform = transform;
        m_Transform.localPosition = localPosition;
        m_LockedTransform.gameObject.SetActive(m_IsLocked);
    }

    public void SetLocked(bool isLocked) => m_IsLocked = isLocked;
    public void SetFull(bool isFull) => m_IsFull = isFull;

    public class Factory : PlaceholderFactory<Slot>
    {
    }
}