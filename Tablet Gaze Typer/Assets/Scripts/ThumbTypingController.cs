using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

public class ThumbTypingController : MonoBehaviour
{
    [Header("References")]
    public KeyboardHighlighter keyboardHighlighter;
    public GameObject keyboardPanel;
    
    [Header("Touch Detection")]
    [Range(0f, 1f)]
    public float bottomRightCornerSize = 0.2f; 
    public float holdTimeThreshold = 0.1f; 
    
    [Header("Surrounding Keys Display")]
    public GameObject surroundingKeysPanelPrefab;
    public Color surroundingKeyColor = new Color(0.5f, 0.8f, 1f, 0.8f);
    public Color selectedKeyColor = new Color(1f, 0.5f, 0f, 1f);
    public float keySpacing = 10f;
    
    [Header("Input Field")]
    public TMP_InputField targetInputField; 
    
    private bool isHolding = false;
    private float holdStartTime = 0f;
    private Vector2 holdStartPosition;
    private GameObject surroundingKeysPanel;
    private List<GameObject> surroundingKeyObjects = new List<GameObject>();
    private Button centerKeyButton = null;
    private Dictionary<GameObject, Button> keyObjectToButtonMap = new Dictionary<GameObject, Button>();
    private Button selectedKey = null;
    
    void Start()
    {
        if (surroundingKeysPanelPrefab == null)
        {
            CreateSurroundingKeysPanel();
        }
    }
    
    void Update()
    {
        HandleTouchInput();
        
        if (isHolding)
        {
            UpdateThumbSelection();
        }
    }
    
    void HandleTouchInput()
    {
        //check for touch
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
        {
            UnityEngine.InputSystem.Controls.TouchControl primaryTouch = touchscreen.touches[0];
            Vector2 touchPosition = primaryTouch.position.ReadValue();
            
            //check if touch is in bottom right corner
            bool inBottomRight = IsInBottomRightCorner(touchPosition);
            
            if (primaryTouch.press.wasPressedThisFrame && inBottomRight)
            {
                //start holding
                isHolding = true;
                holdStartTime = Time.time;
                holdStartPosition = touchPosition;
            }
            else if (primaryTouch.press.wasReleasedThisFrame)
            {
                if (isHolding)
                {
                    //check if we held long enough
                    if (Time.time - holdStartTime >= holdTimeThreshold)
                    {
                        TypeSelectedKey();
                    }
                    //clean up
                    HideSurroundingKeys();
                    isHolding = false;
                }
            }
            else if (primaryTouch.press.isPressed && isHolding)
            {
                holdStartPosition = touchPosition;
            }
        }
        else
        {
            //also support mouse for testing using new Input System
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    if (IsInBottomRightCorner(mousePos))
                    {
                        isHolding = true;
                        holdStartTime = Time.time;
                        holdStartPosition = mousePos;
                    }
                }
                else if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (isHolding)
                    {
                        if (Time.time - holdStartTime >= holdTimeThreshold)
                        {
                            TypeSelectedKey();
                        }
                        HideSurroundingKeys();
                        isHolding = false;
                    }
                }
                else if (mouse.leftButton.isPressed && isHolding)
                {
                    holdStartPosition = mouse.position.ReadValue();
                }
            }
        }
        
        //show surrounding keys after hold threshold
        if (isHolding && Time.time - holdStartTime >= holdTimeThreshold)
        {
            if (surroundingKeysPanel == null || !surroundingKeysPanel.activeSelf)
            {
                ShowSurroundingKeys();
            }
        }
    }
    
    bool IsInBottomRightCorner(Vector2 position)
    {
        float cornerWidth = Screen.width * bottomRightCornerSize;
        float cornerHeight = Screen.height * bottomRightCornerSize;
        
        return position.x >= Screen.width - cornerWidth && 
               position.y <= cornerHeight;
    }
    
    void ShowSurroundingKeys()
    {
        if (keyboardHighlighter == null || keyboardHighlighter.CurrentlyHighlightedButton == null)
        {
            return;
        }
        
        centerKeyButton = keyboardHighlighter.CurrentlyHighlightedButton;
        
        //get surrounding buttons
        List<Button> surroundingButtons = GetSurroundingButtons(centerKeyButton);
        
        if (surroundingButtons.Count == 0)
        {
            return;
        }
        
        //create or activate panel
        if (surroundingKeysPanel == null)
        {
            CreateSurroundingKeysPanel();
        }   
        surroundingKeysPanel.SetActive(true);
        ClearSurroundingKeys();
        
        //position panel in bottom right corner of screen
        RectTransform panelRect = surroundingKeysPanel.GetComponent<RectTransform>();
        
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Camera camera = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                camera = canvas.worldCamera;
            }
            
            //calculate bottom right position with padding
            float padding = 20f;
            Vector2 panelScreenPos = new Vector2(Screen.width - padding, padding);
            
            Vector2 panelLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                panelScreenPos,
                camera,
                out panelLocalPos);
            
            //set anchor to bottom right and position
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-padding, padding);
        }
        CreateSurroundingKeyVisuals(surroundingButtons, centerKeyButton);
    }
    
    void CreateSurroundingKeysPanel()
    {
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        GameObject panel = new GameObject("SurroundingKeysPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(300, 200);
        
        //anchor to bottom right
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);     
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.3f);
        
        surroundingKeysPanel = panel;
    }
    
    void CreateSurroundingKeyVisuals(List<Button> buttons, Button centerButton)
    {
        if (surroundingKeysPanel == null || buttons.Count == 0) return;
        
        //get center button position on keyboard
        RectTransform centerRect = centerButton.GetComponent<RectTransform>();
        Vector2 centerPos = centerRect.anchoredPosition;
        Vector2 centerSize = centerRect.rect.size;
        
        //separate center from surrounding buttons
        List<Button> surroundingButtons = new List<Button>();
        foreach (Button btn in buttons)
        {
            if (btn != centerButton)
            {
                surroundingButtons.Add(btn);
            }
        }
        
        //map buttons to 3x3 grid positions based on their relative positions
        Dictionary<Vector2Int, Button> gridMap = new Dictionary<Vector2Int, Button>();
        gridMap[new Vector2Int(0, 0)] = centerButton;
        
        //calculate average button size for consistent grid sizing
        float avgButtonWidth = centerSize.x;
        float avgButtonHeight = centerSize.y;
        if (surroundingButtons.Count > 0)
        {
            float totalWidth = centerSize.x;
            float totalHeight = centerSize.y;
            foreach (Button btn in surroundingButtons)
            {
                RectTransform btnRect = btn.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    totalWidth += btnRect.rect.width;
                    totalHeight += btnRect.rect.height;
                }
            }
            avgButtonWidth = totalWidth / (surroundingButtons.Count + 1);
            avgButtonHeight = totalHeight / (surroundingButtons.Count + 1);
        }
        
        //assign surrounding buttons to grid positions based on relative position
        foreach (Button button in surroundingButtons)
        {
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) continue;
            
            Vector2 buttonPos = buttonRect.anchoredPosition;
            Vector2 relativePos = buttonPos - centerPos;
            
            int gridX = 0;
            int gridY = 0;
            
            if (relativePos.x < -avgButtonWidth * 0.5f)
                gridX = -1; //left
            else if (relativePos.x > avgButtonWidth * 0.5f)
                gridX = 1; //right
            
            if (relativePos.y > avgButtonHeight * 0.5f)
                gridY = 1; //up (Y increases upward in Unity UI)
            else if (relativePos.y < -avgButtonHeight * 0.5f)
                gridY = -1; //down
            
            Vector2Int gridPos = new Vector2Int(gridX, gridY);
            
            //if position is already taken, find nearest empty position
            if (gridMap.ContainsKey(gridPos))
            {
                //find nearest empty position
                float minDist = float.MaxValue;
                Vector2Int bestPos = gridPos;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (!gridMap.ContainsKey(pos))
                        {
                            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(gridX, gridY));
                            if (dist < minDist)
                            {
                                minDist = dist;
                                bestPos = pos;
                            }
                        }
                    }
                }
                gridPos = bestPos;
            }
            
            gridMap[gridPos] = button;
        }
        
        //create 3x3 grid layout
        int gridSize = 3;
        float keySize = 50f;
        float totalSize = gridSize * keySize + (gridSize - 1) * keySpacing;
        float startOffset = -totalSize / 2f + keySize / 2f;
        
        //update panel size to fit grid
        RectTransform panelRect = surroundingKeysPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(totalSize, totalSize);
        
        //create visual key objects in grid positions
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                //convert row/col to grid coordinates (-1 to 1)
                int gridX = col - 1; // -1, 0, 1
                int gridY = 1 - row; // 1, 0, -1 
                Vector2Int gridPos = new Vector2Int(gridX, gridY);
                
                if (!gridMap.ContainsKey(gridPos))
                {
                    continue; //skip empty positions
                }
                
                Button button = gridMap[gridPos];
                
                //create visual key object
                GameObject keyObj = new GameObject($"SurroundingKey_{row}_{col}");
                keyObj.transform.SetParent(surroundingKeysPanel.transform, false);
                
                RectTransform keyRect = keyObj.AddComponent<RectTransform>();
                keyRect.sizeDelta = new Vector2(keySize, keySize);
                
                //position in grid
                float x = startOffset + col * (keySize + keySpacing);
                float y = startOffset + (gridSize - 1 - row) * (keySize + keySpacing);
                keyRect.anchoredPosition = new Vector2(x, y);
                
                Image keyImage = keyObj.AddComponent<Image>();
                keyImage.color = (button == centerButton) ? selectedKeyColor : surroundingKeyColor;
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(keyObj.transform, false);               
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;                
                TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = GetButtonText(button);
                text.fontSize = 20;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                
                surroundingKeyObjects.Add(keyObj);
                keyObjectToButtonMap[keyObj] = button;
            }
        }
        
        selectedKey = centerButton;
    }
    
    string GetButtonText(Button button)
    {
        TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null)
        {
            return tmpText.text;
        }        
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            return text.text;
        }        
        return "?";
    }
    
    List<Button> GetSurroundingButtons(Button centerButton)
    {
        List<Button> surrounding = new List<Button>();
        
        if (centerButton == null || keyboardHighlighter == null) return surrounding;
        
        RectTransform centerRect = centerButton.GetComponent<RectTransform>();
        if (centerRect == null) return surrounding;

        Vector2 centerPos = centerRect.anchoredPosition;
        float searchRadius = Mathf.Max(centerRect.rect.width, centerRect.rect.height) * 2.5f;
        Button[] allButtons = keyboardPanel.GetComponentsInChildren<Button>();    
        foreach (Button button in allButtons)
        {
            if (button == centerButton) continue;
            
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) continue;
            
            Vector2 buttonPos = buttonRect.anchoredPosition;
            float distance = Vector2.Distance(centerPos, buttonPos);
            
            if (distance <= searchRadius)
            {
                surrounding.Add(button);
            }
        }
        
        //sort by distance and take closest 8 (3x3 grid minus center)
        surrounding.Sort((a, b) =>
        {
            float distA = Vector2.Distance(centerPos, a.GetComponent<RectTransform>().anchoredPosition);
            float distB = Vector2.Distance(centerPos, b.GetComponent<RectTransform>().anchoredPosition);
            return distA.CompareTo(distB);
        });
        
        //limit to 8 surrounding keys for 3x3 grid
        if (surrounding.Count > 8)
        {
            surrounding = surrounding.GetRange(0, 8);
        }
        surrounding.Add(centerButton);
        return surrounding;
    }
    
    void UpdateThumbSelection()
    {
        if (surroundingKeysPanel == null || !surroundingKeysPanel.activeSelf)
        {
            return;
        }
        
        //get current touch/mouse position
        Vector2 currentPosition = holdStartPosition;
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
        {
            currentPosition = touchscreen.touches[0].position.ReadValue();
        }
        else
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                currentPosition = mouse.position.ReadValue();
            }
        }
        
        //convert thumb position to canvas coordinates
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        Camera camera = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            camera = canvas.worldCamera;
        }
        
        Vector2 thumbCanvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            currentPosition,
            camera,
            out thumbCanvasPos);
        
        //find which key is closest to thumb position
        Button nearestKey = null;
        float nearestDistance = float.MaxValue;
        
        //convert panel position to canvas space
        RectTransform panelRect = surroundingKeysPanel.GetComponent<RectTransform>();
        Vector2 panelWorldPos = panelRect.position;
        Vector2 panelScreenPos = RectTransformUtility.WorldToScreenPoint(camera, panelWorldPos);
        Vector2 panelCanvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            panelScreenPos,
            camera,
            out panelCanvasPos);
        
        foreach (GameObject keyObj in surroundingKeyObjects)
        {
            RectTransform keyRect = keyObj.GetComponent<RectTransform>();
            Vector2 keyWorldPos = keyRect.position;
            Vector2 keyScreenPos = RectTransformUtility.WorldToScreenPoint(camera, keyWorldPos);
            Vector2 keyCanvasPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                keyScreenPos,
                camera,
                out keyCanvasPos);
            
            float distance = Vector2.Distance(thumbCanvasPos, keyCanvasPos);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestKey = keyObjectToButtonMap[keyObj];
            }
        }
        
        //update selection
        if (nearestKey != selectedKey)
        {
            selectedKey = nearestKey;
            UpdateKeyVisuals();
        }
    }
    
    void UpdateKeyVisuals()
    {
        foreach (GameObject keyObj in surroundingKeyObjects)
        {
            Image keyImage = keyObj.GetComponent<Image>();
            Button button = keyObjectToButtonMap[keyObj];
            
            if (keyImage != null)
            {
                if (button == selectedKey)
                {
                    keyImage.color = selectedKeyColor;
                }
                else if (button == centerKeyButton)
                {
                    keyImage.color = surroundingKeyColor;
                }
                else
                {
                    keyImage.color = surroundingKeyColor;
                }
            }
        }
    }
    
    void ClearSurroundingKeys()
    {
        foreach (GameObject keyObj in surroundingKeyObjects)
        {
            if (keyObj != null)
            {
                Destroy(keyObj);
            }
        }
        surroundingKeyObjects.Clear();
        keyObjectToButtonMap.Clear();
    }
    
    void HideSurroundingKeys()
    {
        if (surroundingKeysPanel != null)
        {
            surroundingKeysPanel.SetActive(false);
        }
        ClearSurroundingKeys();
        selectedKey = null;
        centerKeyButton = null;
    }
    
    void TypeSelectedKey()
    {
        if (selectedKey == null)
        {
            return;
        }
        
        string keyText = GetButtonText(selectedKey);
        
        if (string.IsNullOrEmpty(keyText))
        {
            return;
        }
        
        //type into input field if available
        if (targetInputField != null)
        {
            targetInputField.text += keyText;
            targetInputField.caretPosition = targetInputField.text.Length;
        }
        else
        {
            //fallback: try to find active input field
            GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
            if (selectedObj != null)
            {
                TMP_InputField inputField = selectedObj.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    inputField.text += keyText;
                    inputField.caretPosition = inputField.text.Length;
                }
            }
        }
        
        UnityEngine.Debug.Log($"Typed: {keyText}");
    }
}