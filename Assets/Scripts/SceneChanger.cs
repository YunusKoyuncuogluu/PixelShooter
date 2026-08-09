using UnityEngine;
using UnityEngine.SceneManagement;
using RopeWrap.Core;

public class SceneChanger
{
    public void LoadInit() => LoadScene(SceneNames.Init);
    public void LoadMainMenu() => LoadScene(SceneNames.MainMenu);
    public void LoadGame() => LoadScene(SceneNames.Game);

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneAsync(string sceneName, System.Action onComplete = null)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            onComplete?.Invoke();
            return;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (onComplete != null)
            operation.completed += _ => onComplete.Invoke();
    }
}
