using System;
using DG.Tweening;
using Grids;
using TMPro;
using UnityEngine;

    public class TunnelVisual : MonoBehaviour
    {
        [SerializeField] private Transform[] m_TunnelTransform;
        [SerializeField] private TextMeshPro m_CountTMP;

        private int m_CountValue;
        private Direction m_CurrentDirection;
        private Transform m_CurrentTunnelTransform;
        private Sequence m_Sequence;
        private Transform m_Transform;

        public void Construct(Direction direction, int count)
        {
            m_Transform = transform;
            m_CurrentDirection = direction;
            m_CountValue = count;
            SetVisual();
            SetCount(count);
        }

        private void OnDestroy()
        {
            
        }

        private void SetVisual()
        {
            foreach (var tunnelTransform in m_TunnelTransform)
                tunnelTransform.gameObject.SetActive(false);

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

        public void Closed()
        {
           
        }
    }
