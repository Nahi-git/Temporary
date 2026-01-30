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

    [Header("Debug")]
    [Tooltip("If enabled, logs gaze coordinates (raw + calibrated) whenever a key is typed.")]
    public bool logGazeOnKeyPress = true;
    
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
    private bool isShiftActive = false;
    private Dictionary<Button, string> originalButtonTexts = new Dictionary<Button, string>(); 

    [Header("Thumb Popup Layout")]
    [Tooltip("If enabled, the surrounding-key popup uses a hard-coded QWERTY neighbor layout based on button GameObject names (e.g. Key_A, Key_Shift, Key_Space).")]
    public bool useHardcodedQwertyPopupLayout = true;
    private readonly Dictionary<string, Button> buttonByName = new Dictionary<string, Button>();
    private static readonly string[][] QwertyRows = new string[][]
    {
        new [] { "Key_Q", "Key_W", "Key_E", "Key_R", "Key_T", "Key_Y", "Key_U", "Key_I", "Key_O", "Key_P", "Key_Backspace" },
        new [] { "Key_A", "Key_S", "Key_D", "Key_F", "Key_G", "Key_H", "Key_J", "Key_K", "Key_L", "Key_Enter" },
        new [] { "Key_Shift", "Key_Z", "Key_X", "Key_C", "Key_V", "Key_B", "Key_N", "Key_M", "Key_Comma", "Key_Fullstop" },
        new [] { "Key_Punctuation", "Key_Space" },
    };
    
    void Start()
    {
        if (surroundingKeysPanelPrefab == null)
        {
            CreateSurroundingKeysPanel();
        }
        BuildButtonLookup();
        Invoke(nameof(StoreOriginalKeyboardTexts), 0.1f);
    }

    public void RefreshStoredKeyboardTexts()
    {
        StoreOriginalKeyboardTexts();
    }
    
    public void ResetShift()
    {
        if (isShiftActive)
        {
            isShiftActive = false;
            UpdateKeyboardDisplay();
        }
    }
    
    void StoreOriginalKeyboardTexts()
    {
        if (keyboardPanel == null)
        {
            UnityEngine.Debug.LogWarning("StoreOriginalKeyboardTexts: keyboardPanel is null");
            return;
        }
        
        Button[] allButtons = keyboardPanel.GetComponentsInChildren<Button>(true); 
        originalButtonTexts.Clear();
        BuildButtonLookup(allButtons);
        
        int letterCount = 0;
        foreach (Button button in allButtons)
        {
            string text = GetRawButtonTextDirect(button);
            if (!string.IsNullOrEmpty(text) && text.Length == 1 && char.IsLetter(text[0]))
            {
                text = text.ToLower();
                letterCount++;
            }
            originalButtonTexts[button] = text;
        }
        
        UnityEngine.Debug.Log($"StoreOriginalKeyboardTexts: Stored {originalButtonTexts.Count} buttons, {letterCount} letters");
        UpdateKeyboardDisplay();
    }

    void BuildButtonLookup()
    {
        if (keyboardPanel == null)
        {
            return;
        }
        BuildButtonLookup(keyboardPanel.GetComponentsInChildren<Button>(true));
    }

    void BuildButtonLookup(Button[] allButtons)
    {
        buttonByName.Clear();
        if (allButtons == null) return;
        foreach (Button b in allButtons)
        {
            if (b == null) continue;
            string n = b.gameObject != null ? b.gameObject.name : null;
            if (string.IsNullOrEmpty(n)) continue;
            if (!buttonByName.ContainsKey(n))
            {
                buttonByName.Add(n, b);
            }
        }
    }
    //used to get the row and column of a button in the hardcoded QWERTY layout
    bool TryGetQwertyPosition(string keyName, out int rowIndex, out int colIndex)
    {
        rowIndex = -1;
        colIndex = -1;
        if (string.IsNullOrEmpty(keyName)) return false;

        for (int r = 0; r < QwertyRows.Length; r++)
        {
            string[] row = QwertyRows[r];
            for (int c = 0; c < row.Length; c++)
            {
                if (row[c] == keyName)
                {
                    rowIndex = r;
                    colIndex = c;
                    return true;
                }
            }
        }

        return false;
    }

    Button GetButtonByName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return null;
        return buttonByName.TryGetValue(keyName, out Button b) ? b : null;
    }

    Button GetButtonAt(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= QwertyRows.Length) return null;
        string[] row = QwertyRows[rowIndex];
        if (colIndex < 0 || colIndex >= row.Length) return null;
        return GetButtonByName(row[colIndex]);
    }

    //for keys on the bottom left of the keyboard, they should show punctuation and space on the bottom grid
    bool ShouldShowPunctuationAndSpace(string centerKeyName)
    {
        return centerKeyName == "Key_Shift" || centerKeyName == "Key_Z" || centerKeyName == "Key_X";
    }
    //switch to hardcoded grid map for thumb typing
    bool TryBuildHardcodedGridMap(Button centerButton, out Dictionary<Vector2Int, Button> gridMap, out bool wideSpaceBottom, out bool punctPlusSpaceBottom)
    {
        gridMap = null;
        wideSpaceBottom = false;
        punctPlusSpaceBottom = false;
        if (!useHardcodedQwertyPopupLayout) return false;
        if (centerButton == null || centerButton.gameObject == null) return false;
        if (keyboardPanel == null) return false;
        if (buttonByName.Count == 0) BuildButtonLookup();

        string centerName = centerButton.gameObject.name;
        if (!TryGetQwertyPosition(centerName, out int r, out int c))
        {
            return false;
        }

        gridMap = new Dictionary<Vector2Int, Button>();
        gridMap[new Vector2Int(0, 0)] = centerButton;

        if (r >= 3)
        {
            // Bottom row: Space and Punctuation. Popup shows row above (Shift/Z/X) and both bottom keys.
            Button shift = GetButtonAt(2, 0);
            Button z = GetButtonAt(2, 1);
            Button x = GetButtonAt(2, 2);
            if (shift != null) gridMap[new Vector2Int(-1, 1)] = shift;
            if (z != null) gridMap[new Vector2Int(0, 1)] = z;
            if (x != null) gridMap[new Vector2Int(1, 1)] = x;
            Button punct = GetButtonByName("Key_Punctuation");
            Button space = GetButtonByName("Key_Space");
            if (centerName == "Key_Punctuation")
            {
                if (space != null) gridMap[new Vector2Int(1, 0)] = space;
            }
            else
            {
                if (punct != null) gridMap[new Vector2Int(-1, 0)] = punct;
            }
            return true;
        }

        int topRow = r - 1;
        if (topRow >= 0)
        {
            Button tl = GetButtonAt(topRow, c - 1);
            Button tc = GetButtonAt(topRow, c);
            Button tr = GetButtonAt(topRow, c + 1);
            if (tl != null) gridMap[new Vector2Int(-1, 1)] = tl;
            if (tc != null) gridMap[new Vector2Int(0, 1)] = tc;
            if (tr != null) gridMap[new Vector2Int(1, 1)] = tr;
        }
        Button ml = GetButtonAt(r, c - 1);
        Button mr = GetButtonAt(r, c + 1);
        if (ml != null) gridMap[new Vector2Int(-1, 0)] = ml;
        if (mr != null) gridMap[new Vector2Int(1, 0)] = mr;

        if (r <= 1)
        {
            int bottomRow = r + 1;
            Button bl = GetButtonAt(bottomRow, c - 1);
            Button bc = GetButtonAt(bottomRow, c);
            Button br = GetButtonAt(bottomRow, c + 1);
            if (bl != null) gridMap[new Vector2Int(-1, -1)] = bl;
            if (bc != null) gridMap[new Vector2Int(0, -1)] = bc;
            if (br != null) gridMap[new Vector2Int(1, -1)] = br;
        }
        else
        {
            punctPlusSpaceBottom = ShouldShowPunctuationAndSpace(centerName);
            if (punctPlusSpaceBottom)
            {
                Button punct = GetButtonByName("Key_Punctuation");
                Button space = GetButtonByName("Key_Space");
                if (punct != null) gridMap[new Vector2Int(-1, -1)] = punct;
                if (space != null) gridMap[new Vector2Int(0, -1)] = space;
            }
            else
            {
                Button space = GetButtonByName("Key_Space");
                if (space != null) gridMap[new Vector2Int(0, -1)] = space;
                wideSpaceBottom = true;
            }
        }

        return true;
    }
    
    string GetRawButtonTextDirect(Button button)
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
                isHolding = true;
                holdStartTime = Time.time;
                holdStartPosition = touchPosition;
            }
            else if (primaryTouch.press.wasReleasedThisFrame)
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
        if (keyboardHighlighter == null)
        {
            return;
        }
        if (keyboardHighlighter.CurrentlyHighlightedButton != null)
        {
            centerKeyButton = keyboardHighlighter.CurrentlyHighlightedButton;
        }
        else
        {
            centerKeyButton = keyboardHighlighter.GetNearestButtonToGaze();
            if (centerKeyButton == null)
            {
                return;
            }
        }
        //builds grid layout for thumb typing
        Dictionary<Vector2Int, Button> gridMap;
        bool wideSpaceBottom;
        bool punctPlusSpaceBottom;
        if (!TryBuildHardcodedGridMap(centerKeyButton, out gridMap, out wideSpaceBottom, out punctPlusSpaceBottom) ||
            gridMap == null || gridMap.Count == 0)
        {
            return;
        }
        
        if (surroundingKeysPanel == null)
        {
            CreateSurroundingKeysPanel();
        }   
        surroundingKeysPanel.SetActive(true);
        ClearSurroundingKeys();
        RectTransform panelRect = surroundingKeysPanel.GetComponent<RectTransform>();
        
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Camera camera = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                camera = canvas.worldCamera;
            }
            Vector2 panelLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                holdStartPosition,
                camera,
                out panelLocalPos);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = panelLocalPos;
        }
        CreateSurroundingKeyVisuals(gridMap, centerKeyButton, wideSpaceBottom, punctPlusSpaceBottom);
    }
    
    void CreateSurroundingKeysPanel()
    {
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        GameObject panel = new GameObject("SurroundingKeysPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(300, 200);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;     
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.3f);
        
        surroundingKeysPanel = panel;
    }
    
    void CreateSurroundingKeyVisuals(Dictionary<Vector2Int, Button> gridMap, Button centerButton, bool wideSpaceBottom, bool punctPlusSpaceBottom)
    {
        if (surroundingKeysPanel == null || gridMap == null || centerButton == null) return;
        bool usingHardcoded = true;
        
        //create 3x3 grid layout
        int gridSize = 3;
        float keySize = 80f;
        float totalSize = gridSize * keySize + (gridSize - 1) * keySpacing;
        float startOffset = -totalSize / 2f + keySize / 2f;
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
                float x = startOffset + col * (keySize + keySpacing);
                float y = startOffset + (gridSize - 1 - row) * (keySize + keySpacing);
                keyRect.anchoredPosition = new Vector2(x, y);
                //for space key on the bottom row, it should be larger than the other keys
                if (usingHardcoded && button != null && button.gameObject != null && button.gameObject.name == "Key_Space" && gridY == -1)
                {
                    if (wideSpaceBottom)
                    {
                        keyRect.sizeDelta = new Vector2(totalSize, keySize);
                        keyRect.anchoredPosition = new Vector2(0f, y);
                    }
                    else if (punctPlusSpaceBottom)
                    {
                        float spanWidth = keySize * 2f + keySpacing;
                        float xCenter = startOffset + 1 * (keySize + keySpacing);
                        float xRight = startOffset + 2 * (keySize + keySpacing);
                        keyRect.sizeDelta = new Vector2(spanWidth, keySize);
                        keyRect.anchoredPosition = new Vector2((xCenter + xRight) * 0.5f, y);
                    }
                }
                
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
        
        //highlight the center key on keyboard initially
        if (keyboardHighlighter != null && selectedKey != null)
        {
            keyboardHighlighter.HighlightButtonExternal(selectedKey);
        }
    }
    
    string GetButtonText(Button button)
    {
        string text = GetRawButtonText(button);
        return ApplyShiftToText(text);
    }
    
    string GetRawButtonText(Button button)
    {
        if (originalButtonTexts.ContainsKey(button))
        {
            return originalButtonTexts[button];
        }
        
        return GetRawButtonTextDirect(button);
    }
    
    string ApplyShiftToText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length != 1)
        {
            return text;
        }
        if (char.IsLetter(text[0]))
        {
            if (isShiftActive)
            {
                return text.ToUpper();
            }
            else
            {
                return text.ToLower();
            }
        }
        
        return text;
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
            if (keyboardHighlighter != null && selectedKey != null)
            {
                keyboardHighlighter.HighlightButtonExternal(selectedKey);
            }
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
        
        
        //resume automatic keyboard highlighting
        if (keyboardHighlighter != null)
        {
            keyboardHighlighter.ResumeAutomaticHighlighting();
        }
    }
    
    void TypeSelectedKey()
    {
        if (selectedKey == null)
        {
            return;
        }
        
        string keyText = GetRawButtonText(selectedKey);
        string buttonName = selectedKey.gameObject.name;

        if (logGazeOnKeyPress)
        {
            LogGazeForKeyPress(selectedKey, keyText);
        }
        
        UnityEngine.Debug.Log($"TypeSelectedKey called: button='{buttonName}', text='{keyText}'"); 
        string normalized = keyText.Trim().ToLower();
        bool isShift = buttonName.ToLower().Contains("shift") || normalized == "shift" || normalized == "⇧";   
        if (isShift)
        {
            isShiftActive = !isShiftActive;
            UnityEngine.Debug.Log($"Shift button pressed! Toggling shift to: {isShiftActive}");
            RefreshSurroundingKeysDisplay();
            return;
        }

        string displayText = ApplyShiftToText(keyText);
        bool isSpace = buttonName == "Key_Space";
        bool isBackspace = buttonName == "Key_Backspace";
        
        //if it's space or backspace, handle directly
        if (isSpace)
        {
            displayText = " ";
        }
        else if (isBackspace)
        {
            displayText = "";
        }
        else if (string.IsNullOrEmpty(displayText))
        {
            return;
        }
        
        //process special keys for other buttons
        string normalizedLower = displayText.Trim().ToLower();
        bool isBackspaceByText = normalizedLower == "backspace" || normalizedLower == "←" || normalizedLower == "delete";
        bool isSpaceByText = normalizedLower == "space" || normalizedLower == "space bar" || normalizedLower == "spacebar";
        
        string processedText;
        if (isBackspace || isBackspaceByText)
        {
            processedText = "";
        }
        else if (isSpace || isSpaceByText)
        {
            processedText = " ";
        }
        else
        {
            processedText = ProcessSpecialKey(displayText);
        }      
        //auto-disable shift after typing a letter
        bool isLetter = !string.IsNullOrEmpty(processedText) && 
                       processedText.Length == 1 && 
                       char.IsLetter(processedText[0]);
        if (isLetter && isShiftActive)
        {
            UnityEngine.Debug.Log($"Auto-disabling shift after typing letter: '{processedText}'");
            isShiftActive = false;
            RefreshSurroundingKeysDisplay(); 
        }
        
        if (targetInputField != null)
        {
            if (isBackspace)
            {
                if (targetInputField.text.Length > 0)
                {
                    targetInputField.text = targetInputField.text.Substring(0, targetInputField.text.Length - 1);
                    targetInputField.caretPosition = targetInputField.text.Length;
                }
                UnityEngine.Debug.Log($"Backspace pressed");
            }
            else if (!string.IsNullOrEmpty(processedText))
            {
                targetInputField.text += processedText;
                targetInputField.caretPosition = targetInputField.text.Length;
                UnityEngine.Debug.Log($"Typed: {displayText} -> '{processedText}'");
            }
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
                    if (isBackspace)
                    {
                        if (inputField.text.Length > 0)
                        {
                            inputField.text = inputField.text.Substring(0, inputField.text.Length - 1);
                            inputField.caretPosition = inputField.text.Length;
                        }
                        UnityEngine.Debug.Log($"Backspace pressed");
                    }
                    else if (!string.IsNullOrEmpty(processedText))
                    {
                        inputField.text += processedText;
                        inputField.caretPosition = inputField.text.Length;
                        UnityEngine.Debug.Log($"Typed: {displayText} -> '{processedText}'");
                    }
                }
            }
        }
    }
    //for testing gaze accuracy
    void LogGazeForKeyPress(Button button, string rawKeyText)
    {
        Vector2 gaze = Vector2.zero;

        if (keyboardHighlighter != null && keyboardHighlighter.calibrator != null)
        {
            gaze = keyboardHighlighter.calibrator.GetCalibratedGazeOrRaw();
        }
        else if (keyboardHighlighter != null && keyboardHighlighter.gazeSource != null)
        {
            gaze = keyboardHighlighter.gazeSource.rawGaze;
        }

        UnityEngine.Debug.Log($"{gaze.x:F0}, {gaze.y:F0}");
    }
    
    void RefreshSurroundingKeysDisplay()
    {
        foreach (GameObject keyObj in surroundingKeyObjects)
        {
            Button button = keyObjectToButtonMap[keyObj];
            TextMeshProUGUI textComponent = keyObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = GetButtonText(button);
            }
        }
        UpdateKeyboardDisplay();
    }
    //after pressing keys change the keyboard (like shift), update the panel visually
    void UpdateKeyboardDisplay()
    {
        if (keyboardPanel == null)
        {
            UnityEngine.Debug.LogWarning("UpdateKeyboardDisplay: keyboardPanel is null");
            return;
        }
        
        Button[] allButtons = keyboardPanel.GetComponentsInChildren<Button>(true); 
        
        int updatedCount = 0;
        int totalLetters = 0;
        foreach (Button button in allButtons)
        {
            if (!originalButtonTexts.ContainsKey(button)) continue;
            
            string originalText = originalButtonTexts[button];
            string displayText = ApplyShiftToText(originalText);
            if (originalText != displayText)
            {
                totalLetters++;
            }
            TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                if (tmpText.text != displayText)
                {
                    tmpText.text = displayText;
                    tmpText.SetAllDirty(); 
                    updatedCount++;
                }
                continue;
            }

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                if (text.text != displayText)
                {
                    text.text = displayText;
                    updatedCount++;
                }
            }
        }
        Canvas canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Canvas.ForceUpdateCanvases();
        }
        
        UnityEngine.Debug.Log($"UpdateKeyboardDisplay: Updated {updatedCount}/{totalLetters} keys. Shift active: {isShiftActive}. Panel active: {keyboardPanel.activeSelf}");
    }
    
    string ProcessSpecialKey(string keyText)
    {
        string normalized = keyText.Trim().ToLower();
        if (normalized == "space" || normalized == "space bar" || normalized == "spacebar" || normalized == " ")
        {
            return " ";
        }
        
        switch (normalized)
        {
            case "enter":
            case "return":
                return "\n";
            case "tab":
                return "\t";
            default:
                //if it's a single character, return it as-is
                if (keyText.Length == 1)
                {
                    return keyText;
                }
                return "";
        }
    }
}