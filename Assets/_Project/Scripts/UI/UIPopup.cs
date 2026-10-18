using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace PixelShooter.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class UIPopup : MonoBehaviour
    {
        public Action OnClose;

        [SerializeField] protected Transform popupTransform;
        [SerializeField] protected Transform backgroundTransform;
        protected Image backgroundImage;
        protected CanvasScaler canvasScaler;
        protected UIManager uiManager;

        [Inject]
        public void Construct(UIManager manager)
        {
            uiManager = manager;
        }

        protected virtual void Awake()
        {
            if (popupTransform != null)
                popupTransform.localScale = Vector3.zero;

            backgroundImage = backgroundTransform.GetComponent<Image>();
            canvasScaler = GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                float aspect = (float)Screen.height / Screen.width;
                if (aspect < 1.6f)
                    canvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        protected virtual void Start()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0f);
                backgroundImage.DOFade(0.78f, 0.2f).SetUpdate(true);
            }

            if (popupTransform == null)
                return;

            popupTransform.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutFlash).SetUpdate(true)
                .OnComplete(() => { popupTransform.DOScale(Vector3.one, 0.1f).SetUpdate(true); });
        }

        public virtual void OnTapClose()
        {
            Close();
        }

        protected virtual void Close()
        {
            if (backgroundImage != null)
                backgroundImage.DOFade(0f, 0.2f).SetUpdate(true);

            if (popupTransform == null)
            {
                FinishClose();
                return;
            }

            popupTransform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InFlash).SetUpdate(true).OnComplete(FinishClose);
        }

        private void FinishClose()
        {
            OnClose?.Invoke();
            if (uiManager != null)
                uiManager.NotifyPopupClosed();

            Destroy(gameObject);
        }
    }
}
