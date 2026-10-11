using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace Game
{
    public class PlayerController : MonoBehaviour
    {
        [FormerlySerializedAs("m_ThreadLayer")]
        [SerializeField] private LayerMask shooterLayer;
        [FormerlySerializedAs("m_Camera")]
        [SerializeField] private Camera viewCamera;
        [Inject] private CellManager cellManager;
        [Inject] private ShooterManager shooterManager;

        private RaycastHit[] hits;
        private bool isActive;
        private Shooter currentShooter;

        public void Construct()
        {
            hits = new RaycastHit[1];
            SetActive(true);
        }

        private void Update()
        {
            if (!isActive)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            currentShooter = CheckShooter();
            if (currentShooter == null || !currentShooter.IsClickable || currentShooter.IsBusy)
                return;

            bool parkedInSlot = currentShooter.IsWaitingInSlot;
            currentShooter.OnClick();
            if (parkedInSlot)
                return;

            cellManager.SetFull(currentShooter.Coordinate, false);
            shooterManager.ControlClickableShooters();
        }

        private Shooter CheckShooter()
        {
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            int collisionCount = Physics.RaycastNonAlloc(ray, hits, 100f, shooterLayer);

            for (int i = 0; i < collisionCount; i++)
            {
                if (hits[i].transform.GetComponentInParent<Shooter>() is { } shooter)
                    return shooter;
            }

            return null;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }
    }
}
