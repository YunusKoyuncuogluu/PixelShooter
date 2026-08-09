using UnityEngine;
using Zenject;

namespace Game
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private LayerMask m_ThreadLayer;
        [SerializeField] private Camera m_Camera;
        [Inject] private CellManager m_CellManager;
        [Inject] private ThreadManager m_ThreadManager;

        private RaycastHit[] m_Hits;
        private bool m_IsActive;
        private Thread m_CurrentThread;

        public void Construct()
        {
            m_Hits = new RaycastHit[1];
            SetActive(true);
        }

        private void Update()
        {
            if (!m_IsActive)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            m_CurrentThread = CheckThread();
            if (m_CurrentThread == null || !m_CurrentThread.IsClickable || m_CurrentThread.IsBusy)
                return;

            m_CurrentThread.OnClick();
            m_CellManager.SetFull(m_CurrentThread.Coordinate, false);
            m_ThreadManager.ControlClickableThreads();
        }

        private Thread CheckThread()
        {
            Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            int collisionCount = Physics.RaycastNonAlloc(ray, m_Hits, 100f, m_ThreadLayer);

            for (int i = 0; i < collisionCount; i++)
            {
                if (m_Hits[i].transform.GetComponentInParent<Thread>() is { } thread)
                    return thread;
            }

            return null;
        }

        public void SetActive(bool isActive)
        {
            m_IsActive = isActive;
        }
    }
}
