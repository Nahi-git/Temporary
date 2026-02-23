using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneHotkeyLoader : MonoBehaviour
{
    [Tooltip("Scene to load when the first hotkey is pressed.")]
    public string sceneToLoad = "Gaze Only";
    [Tooltip("Load the scene when this key is pressed.")]
    public Key hotkey = Key.S;

    [Tooltip("Scene to load when the second hotkey is pressed.")]
    public string sceneToLoad2 = "Gaze Touch";
    [Tooltip("Load the second scene when this key is pressed.")]
    public Key hotkey2 = Key.A;

    [Tooltip("Scene to load when the third hotkey is pressed.")]
    public string sceneToLoad3 = "Touch Only";
    [Tooltip("Load the third scene when this key is pressed.")]
    public Key hotkey3 = Key.D;

    [Tooltip("Scene to load when the fourth hotkey is pressed.")]
    public string sceneToLoad4 = "Gaze Only DEMO";
    [Tooltip("Load the fourth scene when this key is pressed.")]
    public Key hotkey4 = Key.W;

    [Tooltip("Scene to load when the fifth hotkey is pressed.")]
    public string sceneToLoad5 = "Gaze Touch DEMO";
    [Tooltip("Load the fifth scene when this key is pressed.")]
    public Key hotkey5 = Key.Q;

    [Tooltip("Scene to load when the sixth hotkey is pressed.")]
    public string sceneToLoad6 = "Touch Only DEMO";
    [Tooltip("Load the sixth scene when this key is pressed.")]
    public Key hotkey6 = Key.E;

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
            return;
        }
        if (!string.IsNullOrEmpty(sceneToLoad4) && Keyboard.current[hotkey4].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad4);
            return;
        }
        if (!string.IsNullOrEmpty(sceneToLoad5) && Keyboard.current[hotkey5].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad5);
            return;
        }
        if (!string.IsNullOrEmpty(sceneToLoad6) && Keyboard.current[hotkey6].wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneToLoad6);
        }
    }
}
