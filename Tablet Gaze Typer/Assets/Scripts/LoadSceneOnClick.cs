using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnClick : MonoBehaviour
{
    [Tooltip("Name of the scene to load (must be in Build Settings).")]
    public string sceneName = "Gaze Typer";

    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            UnityEngine.Debug.LogError("LoadSceneOnClick: sceneName is not set.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
