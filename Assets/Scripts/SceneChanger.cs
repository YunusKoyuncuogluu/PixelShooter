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
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            onComplete?.Invoke();
            return;
        }
        if (onComplete != null)
        {
            op.completed += _ => onComplete.Invoke();
        }
    }
}
