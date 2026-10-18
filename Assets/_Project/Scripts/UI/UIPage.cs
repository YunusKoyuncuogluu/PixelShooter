using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace PixelShooter.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class UIPage : MonoBehaviour
    {
        [SerializeField] protected Transform basePageTransform;
        protected CanvasScaler canvasScaler;
        protected RectTransform pageRect;
        protected UIManager uiManager;

        public Action OnClose;

        [Inject]
        public void Construct(UIManager manager)
        {
            uiManager = manager;
        }

        protected virtual void Awake()
        {
            canvasScaler = GetComponent<CanvasScaler>();
            if (basePageTransform != null)
                pageRect = basePageTransform.GetComponent<RectTransform>();

            if (canvasScaler == null || pageRect == null)
                return;

            Vector2 referenceSize = canvasScaler.referenceResolution;
            pageRect.DOLocalMoveX(-referenceSize.x, 0f).SetUpdate(true);

            float aspect = (float)Screen.height / Screen.width;
            if (aspect < 1.6f)
                canvasScaler.matchWidthOrHeight = 1f;
        }

        protected virtual void Start()
        {
            if (pageRect == null)
                return;

            pageRect.DOLocalMoveX(0f, 0.2f).SetEase(Ease.OutFlash).SetUpdate(true);
        }

        public void OnTapClose() => Close();

        public virtual void Close()
        {
            if (canvasScaler == null || pageRect == null)
            {
                OnClose?.Invoke();
                Destroy(gameObject);
                return;
            }

            Vector2 referenceSize = canvasScaler.referenceResolution;
            pageRect.DOLocalMoveX(-referenceSize.x, 0.2f).SetEase(Ease.OutFlash).SetUpdate(true).OnComplete(() =>
            {
                OnClose?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
