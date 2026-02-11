using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

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
    [Header("Dimming (thumb 3x3)")]
    [Tooltip("Brightness multiplier for keys outside the 3x3 when thumb controller is active. Lower = darker / less selectable.")]
    [Range(0.1f, 1f)]
    public float nonSelectableDimFactor = 0.35f;  
    
    private List<Button> keyboardButtons = new List<Button>();
    private Button currentlyHighlightedButton = null;
    private Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private bool externalHighlightActive = false;
    private bool highlightingEnabled = true;
    private List<Button> poppedOutButtons = new List<Button>();
    public Button CurrentlyHighlightedButton => currentlyHighlightedButton;
    
    public string GetNearestKeyLabel()
    {
        if (currentlyHighlightedButton == null) return "";
        return GetButtonLabel(currentlyHighlightedButton);
    }
    
    static string GetButtonLabel(Button button)
    {
        if (button == null) return "";
        var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text.Trim();
        var text = button.GetComponentInChildren<UnityEngine.UI.Text>();
        if (text != null && !string.IsNullOrEmpty(text.text)) return text.text.Trim();
        return "";
    }
    
    void Start()
    {
        // Auto-find references if not assigned
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
            if (calibrator == null)
                UnityEngine.Debug.LogWarning("KeyboardHighlighter: Could not find UnityGazeCalibrator!");
        }
        
        if (gazeSource == null)
        {
            gazeSource = FindObjectOfType<GazeWebSocketClient>();
            if (gazeSource == null)
                UnityEngine.Debug.LogWarning("KeyboardHighlighter: Could not find GazeWebSocketClient!");
        }
        
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
        // Try to find references if they're missing (in case scene switched)
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
        }
        
        if (gazeSource == null)
        {
            gazeSource = FindObjectOfType<GazeWebSocketClient>();
        }
        
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
        
        // Debug logging (occasionally)
        if (Time.frameCount % 120 == 0) // Every ~2 seconds at 60fps
        {
            UnityEngine.Debug.Log($"KeyboardHighlighter: gazePosition=({gazePosition.x:F1}, {gazePosition.y:F1}), calibrated={(calibrator != null && calibrator.calibrated)}, buttons={keyboardButtons.Count}");
        }
        
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
    public Vector2 GetGazePositionForExternal()
    {
        return GetGazePosition();
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
        RestoreAllButtonsToOriginal();
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
        //make all other keys dimmed 
        foreach (Button b in keyboardButtons)
        {
            if (b == null || poppedOutButtons.Contains(b)) continue;
            Image img = b.GetComponent<Image>();
            if (img != null)
            {
                Color orig = originalColors.TryGetValue(b, out Color c) ? c : normalColor;
                img.color = new Color(orig.r * nonSelectableDimFactor, orig.g * nonSelectableDimFactor, orig.b * nonSelectableDimFactor, orig.a);
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

    void RestoreAllButtonsToOriginal()
    {
        foreach (Button b in keyboardButtons)
            UnhighlightButton(b);
    }

    public void ResumeAutomaticHighlighting()
    {
        externalHighlightActive = false;
        RestoreAllButtonsToOriginal();
        poppedOutButtons.Clear();
        currentlyHighlightedButton = null;
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

