using DG.Tweening;
using LevelManagement;
using PixelShooter.Save;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace PixelShooter.UI
{
    [DefaultExecutionOrder(50)]
    public class LevelScrollController : MonoBehaviour
    {
        private const int PoolSize = 10;

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private LevelButtonUI levelNodePrefab;
        [SerializeField] private Button playButton;
        [SerializeField] private float spacing = 200f;
        [SerializeField] private float bottomOffset = 80f;
        [SerializeField] private Vector2 introStartPosition;
        [SerializeField] private Vector2 introEndPosition;
        [SerializeField] private float introDuration = 0.55f;

        [Inject] private LevelDatabase levelDatabase;
        [Inject] private SaveManager saveManager;
        [Inject] private LevelManager levelManager;
        [Inject] private SceneChanger sceneChanger;

        private readonly LevelButtonUI[] nodes = new LevelButtonUI[PoolSize];
        private readonly RectTransform[] nodeRects = new RectTransform[PoolSize];
        private readonly int[] shownLevels = new int[PoolSize];

        private int selectedLevel;
        private float itemHeight;
        private float rowStep;
        private float syncedContentY;
        private bool ready;
        private bool focused;
        private bool windowSynced;
        private bool introPlaying;
        private RectTransform panelRect;
        private Tween introTween;

        private void Awake()
        {
            panelRect = (RectTransform)transform;
            panelRect.anchoredPosition = introStartPosition;
            introPlaying = true;
        }

        private void Start()
        {
            if (scrollRect == null || content == null || levelNodePrefab == null)
            {
                PlayIntro();
                return;
            }

            selectedLevel = saveManager.Data.highestUnlockedLevel;
            if (selectedLevel < 1)
                selectedLevel = 1;

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

            itemHeight = ((RectTransform)levelNodePrefab.transform).rect.height;

            if (itemHeight < 1f)
                itemHeight = 160f;

            if (spacing < 0f)
                spacing = 0f;

            rowStep = itemHeight + spacing;

            for (int i = 0; i < PoolSize; i++)
            {
                LevelButtonUI levelButton = Instantiate(levelNodePrefab, content);

                levelButton.gameObject.SetActive(true);
                levelButton.Initialize(OnNodeClicked);

                RectTransform rect = levelButton.NodeRect;
                nodes[i] = levelButton;
                nodeRects[i] = rect;
                shownLevels[i] = 0;
            }

            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            ready = true;
            PlayIntro();
        }

        private void OnDestroy()
        {
            if (introTween != null)
            {
                introTween.Kill();
                introTween = null;
            }

            if (playButton != null)
                playButton.onClick.RemoveListener(OnPlayClicked);
        }

        private void PlayIntro()
        {
            if (panelRect == null)
                return;

            ShowSelectedLevel();

            if (scrollRect != null)
                scrollRect.enabled = false;

            if (introTween != null)
                introTween.Kill();

            introTween = panelRect
                .DOAnchorPos(introEndPosition, introDuration)
                .SetEase(Ease.InOutBounce)
                .OnComplete(FinishIntro)
                .SetLink(gameObject);
        }

        private void FinishIntro()
        {
            introPlaying = false;

            if (panelRect != null)
                panelRect.anchoredPosition = introEndPosition;

            if (scrollRect != null)
                scrollRect.enabled = true;

            ShowSelectedLevel();
        }

        private void ShowSelectedLevel()
        {
            if (!ready || scrollRect == null || scrollRect.viewport == null)
                return;

            float viewHeight = scrollRect.viewport.rect.height;

            if (viewHeight <= 0f)
            {
                focused = false;
                return;
            }

            for (int i = 0; i < PoolSize; i++)
                shownLevels[i] = 0;

            FocusSelected(viewHeight);
            RefreshWindow(viewHeight);
            syncedContentY = content.anchoredPosition.y;
            windowSynced = true;
        }


        private void LateUpdate()
        {
            if (introPlaying || !ready || scrollRect.viewport == null)
                return;

            float viewHeight = scrollRect.viewport.rect.height;
            if (viewHeight <= 0f)
                return;

            if (!focused)
            {
                ShowSelectedLevel();
                return;
            }

            ClampAtFirstLevel(viewHeight);

            if (IsScrollIdle())
                return;

            RefreshWindow(viewHeight);
            syncedContentY = content.anchoredPosition.y;
            windowSynced = true;
        }

        private void OnNodeClicked(LevelButtonUI node)
        {
            if (node.LevelNumber > HighestUnlocked())
                return;

            selectedLevel = node.LevelNumber;

            for (int i = 0; i < PoolSize; i++)
            {
                if (shownLevels[i] > 0)
                    LevelButtonSetInfo(nodes[i], shownLevels[i]);
            }
        }

        private void OnPlayClicked()
        {
            if (levelDatabase.GetLevelData(selectedLevel) == null)
                return;

            if (!levelManager.TrySelectLevel(selectedLevel))
                return;

            sceneChanger.LoadGame();
        }

        private void FocusSelected(float viewHeight)
        {
            if (viewHeight <= 0f)
                return;

            float limit = FirstLevelLimit(viewHeight);

            Vector2 anchored = content.anchoredPosition;
            anchored.y = limit - (selectedLevel - 1) * rowStep;

            content.anchoredPosition = anchored;

            Vector2 size = content.sizeDelta;
            size.y = viewHeight;
            content.sizeDelta = size;

            scrollRect.StopMovement();
            focused = true;
        }

        private void ClampAtFirstLevel(float viewHeight)
        {
            float limit = FirstLevelLimit(viewHeight);

            if (content.anchoredPosition.y <= limit)
                return;

            Vector2 anchored = content.anchoredPosition;
            anchored.y = limit;
            content.anchoredPosition = anchored;
            Vector2 velocity = scrollRect.velocity;

            if (velocity.y > 0f)
            {
                velocity.y = 0f;
                scrollRect.velocity = velocity;
            }
        }

        private void RefreshWindow(float viewHeight)
        {
            float viewTop = -content.anchoredPosition.y;
            float viewBottom = viewTop - viewHeight;
            float probe = (viewBottom - itemHeight * 0.5f) / rowStep;
            int lowest = Mathf.FloorToInt(probe) + 2;

            if (lowest < 1)
                lowest = 1;

            int first = lowest - 2;

            if (first < 1)
                first = 1;

            for (int i = 0; i < PoolSize; i++)
            {
                int level = first + i;
                int slot = (level - 1) % PoolSize;

                if (shownLevels[slot] == level)
                    continue;

                shownLevels[slot] = level;
                RectTransform rect = nodeRects[slot];
                float targetY = NodeAnchoredY(rect, level, viewHeight);
                Vector2 anchored = rect.anchoredPosition;

                if (anchored.y != targetY)
                {
                    anchored.x = 0f;
                    anchored.y = targetY;
                    rect.anchoredPosition = anchored;
                }

                LevelButtonSetInfo(nodes[slot], level);
            }
        }

        private void LevelButtonSetInfo(LevelButtonUI levelButton, int levelNumber)
        {
            bool locked = levelNumber > HighestUnlocked();
            bool selected = levelNumber == selectedLevel;

            levelButton.SetLevelInfo(levelNumber, locked, selected);
        }

        private int HighestUnlocked()
        {
            int highest = saveManager.Data.highestUnlockedLevel;
            if (highest < 1)
                return 1;

            return highest;
        }

        private float NodeAnchoredY(RectTransform node, int level, float contentHeight)
        {
            float centerY = (level - 1) * rowStep;
            float pivotY = centerY + (node.pivot.y - 0.5f) * itemHeight;
            float anchorY = (-content.pivot.y + node.anchorMin.y) * contentHeight;

            return pivotY - anchorY;
        }

        private float FirstLevelLimit(float viewHeight)
        {
            float offset = bottomOffset;
            if (offset < 0f)
                offset = 0f;

            return itemHeight * 0.5f - viewHeight + offset;
        }

        private bool IsScrollIdle()
        {
            if (!windowSynced)
                return false;

            if (Mathf.Abs(content.anchoredPosition.y - syncedContentY) > 0.01f)
                return false;

            if (scrollRect.velocity.sqrMagnitude >= 0.01f)
                return false;

            return true;
        }
    }
}
