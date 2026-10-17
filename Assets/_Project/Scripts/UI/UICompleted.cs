using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace PixelShooter.UI
{
    public class UICompleted : UIPage
    {
        private const float PageDelaySeconds = 1f;

        [SerializeField] private ParticleSystem[] confettis;
        [SerializeField] private GameObject goPage;
        [SerializeField] private GameObject goButton;

        private LevelManager levelManager;
        private SceneChanger sceneChanger;
        private CancellationTokenSource pageCts;

        [Inject]
        public void Construct(LevelManager levels, SceneChanger scenes)
        {
            levelManager = levels;
            sceneChanger = scenes;
        }

        protected override void Awake()
        {
            if (goPage != null)
            {
                goPage.SetActive(false);
            }

            base.Awake();
        }

        protected override void Start()
        {
            if (confettis != null)
            {
                for (int i = 0; i < confettis.Length; i++)
                {
                    if (confettis[i] != null)
                    {
                        confettis[i].Play();
                    }
                }
            }

            pageCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RevealPageAsync(pageCts.Token).Forget();
        }

        private void OnDestroy()
        {
            if (pageCts == null)
            {
                return;
            }

            pageCts.Cancel();
            pageCts.Dispose();
            pageCts = null;
        }

        public void OnTapContinue()
        {
            if (goButton != null)
            {
                goButton.SetActive(false);
            }

            Time.timeScale = 1f;
            if (levelManager != null)
            {
                levelManager.CompleteCurrentLevel();
            }

            sceneChanger.LoadGame();
        }

        private async UniTaskVoid RevealPageAsync(CancellationToken cancellationToken)
        {
            await UniTask.Delay((int)(PageDelaySeconds * 1000f), ignoreTimeScale: true, cancellationToken: cancellationToken);
            if (goPage != null)
            {
                goPage.SetActive(true);
            }

            base.Start();
        }
    }
}
