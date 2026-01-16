using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GazeVisibilityToggle : MonoBehaviour
{
    [Header("References")]
    public GazeCursor gazeCursor;
    public KeyboardHighlighter keyboardHighlighter;
    
    [Header("UI Elements")]
    public Button toggleButton;
    public TextMeshProUGUI buttonText;
    
    [Header("Button Text")]
    public string showGazeText = "Show Gaze";
    public string hideGazeText = "Hide Gaze";
    
    private bool isGazeVisible = true;
    
    void Start()
    {
        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }
        if (buttonText == null && toggleButton != null)
        {
            buttonText = toggleButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleGazeVisibility);
        }
        UpdateButtonText();
    }
    
    public void ToggleGazeVisibility()
    {
        isGazeVisible = !isGazeVisible;
        if (gazeCursor != null)
        {
            gazeCursor.SetVisibility(isGazeVisible);
        }
        if (keyboardHighlighter != null)
        {
            keyboardHighlighter.SetHighlightingEnabled(isGazeVisible);
        }
        UpdateButtonText();      
        UnityEngine.Debug.Log($"Gaze visibility toggled: {(isGazeVisible ? "Visible" : "Hidden")}");
    }
    
    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = isGazeVisible ? hideGazeText : showGazeText;
        }
    }
    
    public void SetGazeVisible(bool visible)
    {
        if (isGazeVisible != visible)
        {
            ToggleGazeVisibility();
        }
    }
    
    public bool IsGazeVisible()
    {
        return isGazeVisible;
    }
}
