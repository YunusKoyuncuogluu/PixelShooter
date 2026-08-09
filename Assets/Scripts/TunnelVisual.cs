using Grids;
using TMPro;
using UnityEngine;

public class TunnelVisual : MonoBehaviour
{
    [SerializeField] private Transform[] m_TunnelTransform;
    [SerializeField] private TextMeshPro m_CountTMP;

    private Direction m_CurrentDirection;
    private Transform m_CurrentTunnelTransform;

    public void Construct(Direction direction, int count)
    {
        m_CurrentDirection = direction;
        SetVisual();
        SetCount(count);
    }

    private void SetVisual()
    {
        for (int i = 0; i < m_TunnelTransform.Length; i++)
            m_TunnelTransform[i].gameObject.SetActive(false);

        if (m_CurrentDirection == Direction.None || m_TunnelTransform.Length == 0)
            return;

        int index = (int)m_CurrentDirection - 1;
        if (index < 0 || index >= m_TunnelTransform.Length)
            return;

        m_CurrentTunnelTransform = m_TunnelTransform[index];
        m_CurrentTunnelTransform.gameObject.SetActive(true);
        m_CountTMP.gameObject.SetActive(true);
    }

    public void SetCount(int count)
    {
        m_CountTMP.text = count.ToString();
    }
}
