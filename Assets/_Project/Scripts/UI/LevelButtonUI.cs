using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelShooter.UI
{
    public class LevelButtonUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject lockedState;
        [SerializeField] private GameObject unlockedState;
        [SerializeField] private GameObject selectedState;

        private Action<LevelButtonUI> clicked;

        public int LevelNumber { get; private set; }
        public RectTransform NodeRect { get; private set; }

        private void Awake()
        {
            NodeRect = (RectTransform)transform;

            if (button != null)
                button.onClick.AddListener(OnClicked);
        }

        public void Initialize(Action<LevelButtonUI> onClicked)
        {
            clicked = onClicked;
        }

        public void SetLevelInfo(int levelNumber, bool locked, bool selected)
        {
            LevelNumber = levelNumber;

            if (levelText != null)
            {
                levelText.gameObject.SetActive(!locked);
                levelText.SetText(levelNumber.ToString());
            }

            if (unlockedState != null)
                unlockedState.SetActive(true);

            if (lockedState != null)
                lockedState.SetActive(locked);

            if (selectedState != null)
                selectedState.SetActive(selected && !locked);

            if (button != null)
                button.interactable = !locked;
        }

        private void OnClicked()
        {
            if (clicked != null)
                clicked.Invoke(this);
        }
    }
}
