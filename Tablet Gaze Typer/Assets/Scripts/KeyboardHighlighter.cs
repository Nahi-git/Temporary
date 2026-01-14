using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class KeyboardHighlighter : MonoBehaviour
{
    [Header("References")]
    public UnityGazeCalibrator calibrator;
    public GazeWebSocketClient gazeSource;
    public GameObject keyboardPanel; 
    
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.8f, 0f, 1f); 
    public Color normalColor = Color.white;  
    
    private List<Button> keyboardButtons = new List<Button>();
    private Button currentlyHighlightedButton = null;
    private Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private bool externalHighlightActive = false;
    public Button CurrentlyHighlightedButton => currentlyHighlightedButton;
    
    void Start()
    {
        if (keyboardPanel != null)
        {
            CollectKeyboardButtons();
        }
        else
        {
            UnityEngine.Debug.LogWarning("KeyboardHighlighter: KeyboardPanel not assigned!");
        }
    }
    
    void CollectKeyboardButtons()
    {
        keyboardButtons.Clear();
        originalColors.Clear();
        
        Button[] buttons = keyboardPanel.GetComponentsInChildren<Button>();
        
        foreach (Button button in buttons)
        {
            keyboardButtons.Add(button);
            
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                originalColors[button] = buttonImage.color;
            }
        }
        
        UnityEngine.Debug.Log($"KeyboardHighlighter: Found {keyboardButtons.Count} buttons");
    }
    
    void Update()
    {
        //skip automatic highlighting if external highlight is active
        if (externalHighlightActive)
        {
            return;
        }
        
        if (keyboardButtons.Count == 0 || keyboardPanel == null || !keyboardPanel.activeSelf)
        {
            return;
        }
        
        Vector2 gazePosition = GetGazePosition();
        
        Button nearestButton = FindNearestButton(gazePosition);
        
        if (nearestButton != currentlyHighlightedButton)
        {
            if (currentlyHighlightedButton != null)
            {
                UnhighlightButton(currentlyHighlightedButton);
            }
            if (nearestButton != null)
            {
                HighlightButton(nearestButton);
            }
            
            currentlyHighlightedButton = nearestButton;
        }
    }
    
    Vector2 GetGazePosition()
    {
        if (calibrator != null && calibrator.calibrated)
        {
            return calibrator.calibratedGaze;
        }
        else if (gazeSource != null)
        {
            return gazeSource.rawGaze;
        }
        
        return Vector2.zero;
    }
    
    Button FindNearestButton(Vector2 screenPosition)
    {
        Button nearestButton = null;
        float nearestDistance = float.MaxValue;
        
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        
        // convert gaze screen position to canvas coordinates
        float clampedX = Mathf.Clamp(screenPosition.x, 0, Screen.width);
        float clampedY = Mathf.Clamp(screenPosition.y, 0, Screen.height);
        
        float flippedY = Screen.height - clampedY;
        
        Camera camera = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            camera = canvas.worldCamera;
        }
        
        Vector2 gazeCanvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            new Vector2(clampedX, flippedY),
            camera,
            out gazeCanvasPos);
        
        foreach (Button button in keyboardButtons)
        {
            if (button == null || !button.gameObject.activeSelf) continue;
            
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) continue;
            
            Vector2 buttonCenter = buttonRect.anchoredPosition;
            
            float distance = Vector2.Distance(gazeCanvasPos, buttonCenter);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestButton = button;
            }
        }
        
        return nearestButton;
    }
    
    void HighlightButton(Button button)
    {
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = highlightColor;
        }
    }
    
    void UnhighlightButton(Button button)
    {
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            if (originalColors.ContainsKey(button))
            {
                buttonImage.color = originalColors[button];
            }
            else
            {
                buttonImage.color = normalColor;
            }
        }
    }
    
    //public method to highlight a button from external scripts (this case is the thumb typing controller)
    public void HighlightButtonExternal(Button button)
    {
        if (button == null) return;
        
        externalHighlightActive = true;
        
        //unhighlight current button if different
        if (currentlyHighlightedButton != null && currentlyHighlightedButton != button)
        {
            UnhighlightButton(currentlyHighlightedButton);
        }
        
        //highlight new button
        HighlightButton(button);
        currentlyHighlightedButton = button;
    }
    
    //public method to resume automatic highlighting
    public void ResumeAutomaticHighlighting()
    {
        externalHighlightActive = false;
    }
    
    public void RefreshButtonList()
    {
        CollectKeyboardButtons();
    }
}

