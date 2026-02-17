using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneHotkeyLoader : MonoBehaviour
{
    [Tooltip("Scene to load when the first hotkey is pressed (must be in Build Settings).")]
    public string sceneToLoad = "Gaze Only";
    [Tooltip("Load the scene when this key is pressed.")]
    public Key hotkey = Key.X;

    [Tooltip("Scene to load when the second hotkey is pressed.")]
    public string sceneToLoad2 = "Gaze Touch";
    [Tooltip("Load the second scene when this key is pressed.")]
    public Key hotkey2 = Key.Z;

    [Tooltip("Scene to load when the third hotkey is pressed (e.g. Touch Only).")]
    public string sceneToLoad3 = "Touch Only";
    [Tooltip("Load the third scene when this key is pressed.")]
    public Key hotkey3 = Key.V;

    void Update()
    {
        if (Keyboard.current == null) return;

        if (!string.IsNullOrEmpty(sceneToLoad) && Keyboard.current[hotkey].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad);
            return;
        }
        if (!string.IsNullOrEmpty(sceneToLoad2) && Keyboard.current[hotkey2].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad2);
            return;
        }
        if (!string.IsNullOrEmpty(sceneToLoad3) && Keyboard.current[hotkey3].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad3);
        }
    }
}
