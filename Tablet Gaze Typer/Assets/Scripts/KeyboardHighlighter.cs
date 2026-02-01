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
    [Tooltip("Color used when multiple keys 'pop out' on the keyboard")]
    public Color popoutColor = new Color(0.6f, 0.85f, 1f, 1f);
    public Color normalColor = Color.white;  
    
    private List<Button> keyboardButtons = new List<Button>();
    private Button currentlyHighlightedButton = null;
    private Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private bool externalHighlightActive = false;
    private bool highlightingEnabled = true;
    private List<Button> poppedOutButtons = new List<Button>();
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
        //skip automatic highlighting if highlighting is disabled or external highlight is active
        if (!highlightingEnabled || externalHighlightActive)
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
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float clampedX = Mathf.Clamp(screenPosition.x, 0, Screen.width);
        float clampedY = Mathf.Clamp(screenPosition.y, 0, Screen.height);
        float flippedY = Screen.height - clampedY;
        Vector2 screenPosFlipped = new Vector2(clampedX, flippedY);
        Camera camera = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            camera = canvas.worldCamera;
        }
        //convert screen position to canvas coordinates
        Vector2 gazeCanvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosFlipped,
            camera,
            out gazeCanvasPos);

        Vector3[] corners = new Vector3[4];
        foreach (Button button in keyboardButtons)
        {
            if (button == null || !button.gameObject.activeSelf) continue;
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) continue;

            buttonRect.GetWorldCorners(corners);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 screenCorner = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                Vector2 localCorner;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenCorner,
                    camera,
                    out localCorner);
                if (localCorner.x < minX) minX = localCorner.x;
                if (localCorner.x > maxX) maxX = localCorner.x;
                if (localCorner.y < minY) minY = localCorner.y;
                if (localCorner.y > maxY) maxY = localCorner.y;
            }

            float distance = DistanceFromPointToRect(gazeCanvasPos, minX, maxX, minY, maxY);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestButton = button;
            }
        }

        return nearestButton;
    }

    //rather than using the center of the button, we use the entire button area to find the nearest button
    static float DistanceFromPointToRect(Vector2 point, float minX, float maxX, float minY, float maxY)
    {
        if (point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY)
            return 0f;
        float dx = point.x < minX ? minX - point.x : (point.x > maxX ? point.x - maxX : 0f);
        float dy = point.y < minY ? minY - point.y : (point.y > maxY ? point.y - maxY : 0f);
        return Mathf.Sqrt(dx * dx + dy * dy);
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
        UnhighlightAllPoppedOut();
        if (currentlyHighlightedButton != null && currentlyHighlightedButton != button)
        {
            UnhighlightButton(currentlyHighlightedButton);
        }
        HighlightButton(button);
        currentlyHighlightedButton = button;
    }
    //highlight all thumb panel keys on the on-screen keyboard (users dont need to look bottom right for available keys)
    public void HighlightButtonsExternal(List<Button> popoutButtons, Button selectedButton)
    {
        if (popoutButtons == null || popoutButtons.Count == 0)
        {
            if (selectedButton != null)
                HighlightButtonExternal(selectedButton);
            return;
        }
        externalHighlightActive = true;
        UnhighlightAllPoppedOut();
        poppedOutButtons.Clear();
        foreach (Button b in popoutButtons)
        {
            if (b == null) continue;
            Image img = b.GetComponent<Image>();
            if (img != null)
            {
                img.color = (b == selectedButton) ? highlightColor : popoutColor;
                poppedOutButtons.Add(b);
            }
        }
        currentlyHighlightedButton = selectedButton;
    }

    public void SetSelectedPoppedButton(Button selectedButton)
    {
        if (poppedOutButtons.Count == 0) return;
        foreach (Button b in poppedOutButtons)
        {
            Image img = b.GetComponent<Image>();
            if (img != null)
                img.color = (b == selectedButton) ? highlightColor : popoutColor;
        }
        currentlyHighlightedButton = selectedButton;
    }

    void UnhighlightAllPoppedOut()
    {
        foreach (Button b in poppedOutButtons)
            UnhighlightButton(b);
        poppedOutButtons.Clear();
    }

    public void ResumeAutomaticHighlighting()
    {
        externalHighlightActive = false;
        UnhighlightAllPoppedOut();
        if (currentlyHighlightedButton != null)
        {
            UnhighlightButton(currentlyHighlightedButton);
            currentlyHighlightedButton = null;
        }
    }
    public void RefreshButtonList()
    {
        CollectKeyboardButtons();
    }
    
    public void SetHighlightingEnabled(bool enabled)
    {
        highlightingEnabled = enabled;
        if (!enabled && currentlyHighlightedButton != null && !externalHighlightActive)
        {
            UnhighlightButton(currentlyHighlightedButton);
            currentlyHighlightedButton = null;
        }
    }
    
    public bool GetHighlightingEnabled()
    {
        return highlightingEnabled;
    }

    public Button GetNearestButtonToGaze()
    {
        Vector2 gazePosition = GetGazePosition();
        return FindNearestButton(gazePosition);
    }
}

