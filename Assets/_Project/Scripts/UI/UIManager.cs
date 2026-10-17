using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace PixelShooter.UI
{
    public class UIManager : MonoBehaviour
    {
        private GameObject gamePagePrefab;
        private GameObject failPagePrefab;
        private GameObject completedPagePrefab;
        private GameObject settingPopupPrefab;

        private DiContainer container;
        private readonly List<GameObject> pages = new List<GameObject>(4);
        private readonly List<GameObject> popups = new List<GameObject>(2);
        private UIGame activeGamePage;
        private bool resultOpen;

        [Inject]
        public void Construct(DiContainer diContainer)
        {
            container = diContainer;
        }

        public void Configure(GameObject gamePage, GameObject failPage, GameObject completedPage, GameObject settingPopup)
        {
            gamePagePrefab = gamePage;
            failPagePrefab = failPage;
            completedPagePrefab = completedPage;
            settingPopupPrefab = settingPopup;
        }

        public UIGame ShowGame()
        {
            resultOpen = false;
            GameObject instance = Show(gamePagePrefab, true);
            activeGamePage = instance != null ? instance.GetComponent<UIGame>() : null;
            return activeGamePage;
        }

        public void ShowCompleted()
        {
            resultOpen = true;
            activeGamePage = null;
            Show(completedPagePrefab, true);
        }

        public void ShowFail()
        {
            resultOpen = true;
            activeGamePage = null;
            Show(failPagePrefab, true);
        }

        public void ShowSettings()
        {
            Time.timeScale = 0f;
            Transform parent = GetComponent<Canvas>() != null ? null : transform;
            Show(settingPopupPrefab, false, parent);
        }

        public void NotifyPopupClosed()
        {
            if (!resultOpen)
            {
                Time.timeScale = 1f;
            }
        }

        public void LockGameplay()
        {
            if (activeGamePage != null)
            {
                activeGamePage.LockInput();
            }
        }

        public void GoHome()
        {
            Time.timeScale = 1f;
            SceneChanger sceneChanger = container.Resolve<SceneChanger>();
            sceneChanger.LoadMainMenu();
        }

        private GameObject Show(GameObject prefab, bool isPage)
        {
            if (prefab == null || container == null)
            {
                return null;
            }

            if (isPage)
            {
                HideAll();
            }

            GameObject instance = Show(prefab, isPage, transform);
            return instance;
        }

        private GameObject Show(GameObject prefab, bool isPage, Transform parent)
        {
            if (prefab == null || container == null)
            {
                return null;
            }

            if (isPage)
            {
                HideAll();
            }

            GameObject instance = parent != null
                ? container.InstantiatePrefab(prefab, parent)
                : container.InstantiatePrefab(prefab);
            if (isPage)
            {
                pages.Add(instance);
            }
            else
            {
                popups.Add(instance);
            }

            return instance;
        }

        private void HideAll()
        {
            DestroyList(pages);
            DestroyList(popups);
        }

        private static void DestroyList(List<GameObject> instances)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null)
                {
                    Destroy(instances[i]);
                }
            }

            instances.Clear();
        }
    }
}
