using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadSceneOnClick : MonoBehaviour
{
    [Tooltip("Name of the scene to load (must be in Build Settings).")]
    public string sceneName = "Gaze Touch";

    void Start()
    {
        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(LoadTargetScene);
    }

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
