using UnityEngine.SceneManagement;
using RopeWrap.Core;

public class SceneChanger
{
    public void LoadMainMenu() => LoadScene(SceneNames.MainMenu);
    public void LoadGame() => LoadScene(SceneNames.Game);

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        SceneManager.LoadScene(sceneName);
    }
}
