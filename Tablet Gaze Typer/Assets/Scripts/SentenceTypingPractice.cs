using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;
using System.Collections;

public class SentenceTypingPractice : MonoBehaviour
{
    public enum PracticeState { Countdown, Typing, Complete, Break }
    
    [Header("References")]
    [Tooltip("Text component that displays the sentence to type")]
    public TextMeshProUGUI sentenceDisplay;
    
    [Tooltip("Input field where the user types. If not assigned, will try to find it automatically.")]
    public TMP_InputField targetInputField;
    
    [Tooltip("Optional: Text to show countdown.")]
    public TextMeshProUGUI countdownDisplay;
    
    [Tooltip("Optional: Text or panel to show final time when complete. If null, time is only logged.")]
    public TextMeshProUGUI resultDisplay;
    
    [Tooltip("Optional: Keyboard panel to disable during countdown.")]
    public GameObject keyboardPanel;
    
    [Tooltip("Optional: Button or panel to show when typing is complete (e.g. 'Go to Gaze Only'). Hidden at start and during countdown/typing.")]
    public GameObject buttonToShowWhenComplete;
    
    [Tooltip("Optional: logs one row per completed phrase to a separate CSV (condition, WPM, TER, etc.).")]
    public PhraseMetricsLogger phraseMetricsLogger;
    [Tooltip("Optional: used with phraseMetricsLogger to get PhraseID and sentence number.")]
    public SentenceSessionManager sentenceSessionManager;
    [Tooltip("Optional: disabled during Break/Countdown so user cannot type (Gaze+Touch mode).")]
    public ThumbTypingController thumbTypingController;
    [Tooltip("Optional: disabled during Break/Countdown so user cannot type (Gaze-only mode).")]
    public GazeTapTypingController gazeTapTypingController;
    
    [Header("Break")]
    [Tooltip("Optional: black square shown in center during break for re-orienting gaze. Created at runtime if null.")]
    public GameObject breakCenterSquare;
    
    [Header("Sentence Settings")]
    [Tooltip("The sentence the user should type")]
    [TextArea(2, 5)]
    public string targetSentence = "The quick brown fox jumps over the lazy dog";
    
    [Header("Countdown & Timing")]
    [Tooltip("Countdown duration in seconds before typing starts (e.g. at session start).")]
    public float countdownSeconds = 10f;
    [Tooltip("Countdown duration in seconds when resuming from a break (press P to continue).")]
    public float countdownAfterBreakSeconds = 3f;
    
    [Header("Colors")]
    [Tooltip("Color for correctly typed letters")]
    public Color correctColor = Color.green;
    
    [Tooltip("Color for letters not yet typed")]
    public Color defaultColor = Color.white;
    
    [Tooltip("Color for the current letter to type (optional highlight)")]
    public Color currentLetterColor = Color.yellow;
    
    [Tooltip("Color for current space (shown as a block). Use alpha for transparency.")]
    public Color currentSpaceColor = new Color(1f, 1f, 0f, 0.5f);
    
    [Tooltip("Color for incorrectly typed letters")]
    public Color wrongColor = Color.red;
    
    [Tooltip("Color for incorrect space (shown as a block). Use alpha for transparency.")]
    public Color wrongSpaceColor = new Color(1f, 0f, 0f, 0.5f);
    
    private string originalSentence;
    private int currentIndex = 0;
    private List<bool> characterCorrectness = new List<bool>();
    private StringBuilder coloredText;
    private string previousInputText = "";
    private bool isProcessingInput = false;
    
    private PracticeState state = PracticeState.Countdown;
    private float typingStartTime;
    private float typingEndTime;
    private float firstKeystrokeTime;
    private float lastKeystrokeTime;
    private Coroutine countdownCoroutine;
    private bool _breakPreservesInput;
    private bool _resumingFromManualPause;
    private float _accumulatedTypingTime;
    private float _pauseStartTime;
    private float _totalPauseDuration;
    private float _currentSegmentFirstKeyTime;
    private float _currentCountdownDuration;
    public PracticeState State => state;
    
    void Update()
    {
        if (!WasPKeyPressedThisFrame()) return;
        if (state == PracticeState.Typing)
        {
            BeginBreak(clearInput: false);
            return;
        }
        if (state != PracticeState.Break) return;
        BeginCountdown(clearInput: !_breakPreservesInput, countdownDuration: countdownAfterBreakSeconds);
    }

    static bool WasPKeyPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.pKey.wasPressedThisFrame;
    }

    public float GetTypingElapsedTime()
    {
        if (state != PracticeState.Typing && state != PracticeState.Complete) return 0f;
        return Time.time - typingStartTime;
    }
    
    public string GetCharacterToBeTyped()
    {
        if (string.IsNullOrEmpty(originalSentence) || currentIndex >= originalSentence.Length)
            return "";
        return originalSentence[currentIndex].ToString();
    }
    
    void Start()
    {
        if (targetInputField == null)
        {
            targetInputField = FindObjectOfType<TMP_InputField>();
            if (targetInputField == null)
            {
                UnityEngine.Debug.LogWarning("SentenceTypingPractice: Could not find TMP_InputField. Please assign it manually.");
            }
        }
        if (sentenceDisplay == null)
        {
            UnityEngine.Debug.LogWarning("SentenceTypingPractice: sentenceDisplay not assigned. Please assign a TextMeshProUGUI component.");
        }
        if (keyboardPanel == null)
        {
            keyboardPanel = GameObject.Find("KeyboardPanel");
        }
        if (phraseMetricsLogger == null) phraseMetricsLogger = FindObjectOfType<PhraseMetricsLogger>();
        if (sentenceSessionManager == null) sentenceSessionManager = FindObjectOfType<SentenceSessionManager>();
        if (thumbTypingController == null) thumbTypingController = FindObjectOfType<ThumbTypingController>();
        if (gazeTapTypingController == null) gazeTapTypingController = FindObjectOfType<GazeTapTypingController>();
        EnsureSentenceDisplayUniformSize();
        EnsureBreakCenterSquare();
        InitializeSentence();
        BeginCountdown();
    }
    
    void EnsureSentenceDisplayUniformSize()
    {
        if (sentenceDisplay == null) return;
        sentenceDisplay.enableAutoSizing = false;
    }
    
    void OnEnable()
    {
        if (targetInputField != null)
        {
            targetInputField.onValueChanged.AddListener(OnInputFieldChanged);
        }
    }
    
    void OnDisable()
    {
        if (targetInputField != null)
        {
            targetInputField.onValueChanged.RemoveListener(OnInputFieldChanged);
        }
    }
    
    void InitializeSentence()
    {
        originalSentence = targetSentence;
        currentIndex = 0;
        characterCorrectness.Clear();
        previousInputText = "";
        
        if (sentenceDisplay != null)
        {
            UpdateSentenceDisplay();
        }
        
        if (targetInputField != null)
        {
            targetInputField.text = "";
            previousInputText = "";
        }
        
        if (resultDisplay != null)
        {
            resultDisplay.text = "";
            resultDisplay.gameObject.SetActive(false);
        }
        if (buttonToShowWhenComplete != null)
            buttonToShowWhenComplete.SetActive(false);
    }
    
    public void BeginBreak(bool clearInput = true)
    {
        state = PracticeState.Break;
        _breakPreservesInput = !clearInput;
        if (targetInputField != null)
        {
            targetInputField.interactable = false;
            if (clearInput)
            {
                targetInputField.text = "";
                previousInputText = "";
            }
        }
        if (keyboardPanel != null)
            keyboardPanel.SetActive(false);
        if (buttonToShowWhenComplete != null)
            buttonToShowWhenComplete.SetActive(false);
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        if (countdownDisplay != null)
        {
            countdownDisplay.gameObject.SetActive(true);
            countdownDisplay.text = "BREAK\nPress P to continue";
        }
        EnsureBreakCenterSquare();
        if (breakCenterSquare != null)
            breakCenterSquare.SetActive(true);
        SetTypingControllersEnabled(false);
        if (!clearInput)
        {
            _pauseStartTime = Time.time;
            if (firstKeystrokeTime > 0f && lastKeystrokeTime > 0f)
                _accumulatedTypingTime += lastKeystrokeTime - firstKeystrokeTime;
        }
    }

    public void BeginCountdown(bool clearInput = true, float? countdownDuration = null)
    {
        state = PracticeState.Countdown;
        _currentCountdownDuration = countdownDuration ?? countdownSeconds;
        if (breakCenterSquare != null)
            breakCenterSquare.SetActive(false);
        _resumingFromManualPause = !clearInput;
        if (targetInputField != null)
        {
            targetInputField.interactable = false;
            if (clearInput)
            {
                targetInputField.text = "";
                previousInputText = "";
            }
        }
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(false);
        }
        if (buttonToShowWhenComplete != null)
            buttonToShowWhenComplete.SetActive(false);
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        SetTypingControllersEnabled(false);
        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }
    
    void EnsureBreakCenterSquare()
    {
        if (breakCenterSquare != null) return;
        Canvas canvas = countdownDisplay != null ? countdownDisplay.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        Vector2 size = new Vector2(150f, 150f);
        var gazeCursor = FindObjectOfType<GazeCursor>();
        if (gazeCursor != null)
        {
            var cursorRect = gazeCursor.GetComponent<RectTransform>();
            if (cursorRect != null) size = cursorRect.sizeDelta;
        }
        var go = new GameObject("BreakCenterSquare");
        go.transform.SetParent(canvas.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.color = Color.black;
        breakCenterSquare = go;
        go.SetActive(false);
    }
    
    void SetTypingControllersEnabled(bool enabled)
    {
        if (thumbTypingController != null) thumbTypingController.enabled = enabled;
        if (gazeTapTypingController != null) gazeTapTypingController.enabled = enabled;
    }
    
    IEnumerator CountdownRoutine()
    {
        int remaining = Mathf.CeilToInt(_currentCountdownDuration);
        for (int i = remaining; i >= 1; i--)
        {
            if (countdownDisplay != null)
            {
                countdownDisplay.gameObject.SetActive(true);
                countdownDisplay.text = i.ToString();
            }
            yield return new WaitForSeconds(1f);
        }
        
        if (countdownDisplay != null)
        {
            countdownDisplay.text = "Go!";
            yield return new WaitForSeconds(0.5f);
            countdownDisplay.gameObject.SetActive(false);
        }
        
        countdownCoroutine = null;
        StartTypingState();
    }

    //prevents countdown from running again between sentences
    public void StartTypingAgain()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        StartTypingState();
    }

    void StartTypingState()
    {
        state = PracticeState.Typing;
        if (!_resumingFromManualPause)
        {
            typingStartTime = Time.time;
            firstKeystrokeTime = 0f;
            lastKeystrokeTime = 0f;
            _accumulatedTypingTime = 0f;
            _totalPauseDuration = 0f;
            _currentSegmentFirstKeyTime = 0f;
        }
        else
        {
            _totalPauseDuration += Time.time - _pauseStartTime;
            lastKeystrokeTime = 0f;
            _currentSegmentFirstKeyTime = 0f;
        }
        _resumingFromManualPause = false;
        if (targetInputField != null)
        {
            targetInputField.interactable = true;
        }
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(true);
        }
        SetTypingControllersEnabled(true);
    }
    
    void OnInputFieldChanged(string newText)
    {
        if (string.IsNullOrEmpty(originalSentence) || isProcessingInput)
        {
            return;
        }
        if (state != PracticeState.Typing)
        {
            if (state == PracticeState.Break || state == PracticeState.Countdown)
            {
                if (targetInputField != null && targetInputField.text != previousInputText)
                {
                    isProcessingInput = true;
                    targetInputField.text = previousInputText;
                    isProcessingInput = false;
                }
            }
            else
            {
                previousInputText = newText;
            }
            return;
        }
        if (currentIndex >= originalSentence.Length)
        {
            previousInputText = newText;
            return;
        }
        //handle backspace
        if (newText.Length < previousInputText.Length)
        {
            previousInputText = newText;
            RecalculateProgress();
            UpdateSentenceDisplay();
            return;
        }
        
        //when a new character is entered, compare it with the expected character
        //if the character is wrong, user does not need to backspace, just type the correct character
        if (newText.Length > previousInputText.Length)
        {
            float t = Time.time;
            if (firstKeystrokeTime <= 0f) firstKeystrokeTime = t;
            else if (_currentSegmentFirstKeyTime <= 0f) _currentSegmentFirstKeyTime = t;
            lastKeystrokeTime = t;
            char newChar = newText[newText.Length - 1];
            char expectedChar = originalSentence[currentIndex];
            bool isMatch = false;
            if (char.IsLetter(newChar) && char.IsLetter(expectedChar))
            {
                isMatch = char.ToLower(newChar) == char.ToLower(expectedChar);
            }
            else
            {
                isMatch = newChar == expectedChar;
            }
            characterCorrectness.Add(isMatch);
            currentIndex++;
            UpdateSentenceDisplay();
            if (currentIndex >= originalSentence.Length)
            {
                OnTypingComplete();
            }
        }
        previousInputText = newText;
    }
    
    void OnTypingComplete()
    {
        state = PracticeState.Complete;
        typingEndTime = Time.time;
        float elapsed = typingEndTime - typingStartTime;

        if (phraseMetricsLogger != null)
        {
            int phraseId = sentenceSessionManager != null ? sentenceSessionManager.CurrentSentenceId : 0;
            int sentenceNumber = sentenceSessionManager != null ? sentenceSessionManager.CurrentSentenceNumber : 1;
            string typed = targetInputField != null ? targetInputField.text : "";
            float lastSegmentTyping = 0f;
            if (lastKeystrokeTime > 0f && _currentSegmentFirstKeyTime > 0f)
                lastSegmentTyping = lastKeystrokeTime - _currentSegmentFirstKeyTime;
            else if (lastKeystrokeTime > 0f && firstKeystrokeTime > 0f)
                lastSegmentTyping = lastKeystrokeTime - firstKeystrokeTime;
            phraseMetricsLogger.LogPhrase(
                phraseId,
                sentenceNumber,
                originalSentence,
                typed,
                typingStartTime,
                firstKeystrokeTime,
                lastKeystrokeTime,
                typingEndTime,
                totalPauseDuration: _totalPauseDuration,
                preAccumulatedTypingTime: _accumulatedTypingTime,
                lastSegmentTypingTime: lastSegmentTyping);
        }

        if (targetInputField != null)
        {
            targetInputField.interactable = false;
        }
        SetTypingControllersEnabled(false);
        
        string timeText = FormatTime(elapsed);
        if (resultDisplay != null)
        {
            resultDisplay.gameObject.SetActive(true);
            resultDisplay.text = "Time: " + timeText;
        }
        if (buttonToShowWhenComplete != null)
            buttonToShowWhenComplete.SetActive(true);
        UnityEngine.Debug.Log($"SentenceTypingPractice: Completed in {timeText}");
    }
    
    static string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        float secs = seconds - mins * 60f;
        if (mins > 0)
        {
            return $"{mins}m {secs:F2}s";
        }
        return $"{secs:F2}s";
    }
    
    void RecalculateProgress()
    {
        characterCorrectness.Clear();
        currentIndex = 0;
        if (targetInputField == null || string.IsNullOrEmpty(previousInputText))
        {
            return;
        }
        int minLength = Mathf.Min(previousInputText.Length, originalSentence.Length);
        for (int i = 0; i < minLength; i++)
        {
            char typedChar = previousInputText[i];
            char expectedChar = originalSentence[i];
            bool isMatch = false;
            if (char.IsLetter(typedChar) && char.IsLetter(expectedChar))
            {
                isMatch = char.ToLower(typedChar) == char.ToLower(expectedChar);
            }
            else
            {
                isMatch = typedChar == expectedChar;
            }
            characterCorrectness.Add(isMatch);
            currentIndex = i + 1;
        }
    }
    
    //change the color of the sentence display to show the progress
    void UpdateSentenceDisplay()
    {
        if (sentenceDisplay == null || string.IsNullOrEmpty(originalSentence))
        {
            return;
        }
        
        coloredText = new StringBuilder();
        
        for (int i = 0; i < originalSentence.Length; i++)
        {
            char c = originalSentence[i];
            string charStr = c.ToString();
            
            if (i < currentIndex)
            {
                bool correct = i < characterCorrectness.Count && characterCorrectness[i];
                if (correct)
                {
                    coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(correctColor)}>{charStr}</color>");
                }
                else if (c == ' ')
                {
                    coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGBA(wrongSpaceColor)}>\u2588</color>");
                }
                else
                {
                    coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(wrongColor)}>{charStr}</color>");
                }
            }
            else if (i == currentIndex)
            {
                if (c == ' ')
                {
                    coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGBA(currentSpaceColor)}>\u2588</color>");
                }
                else
                {
                    coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(currentLetterColor)}>{charStr}</color>");
                }
            }
            else
            {
                coloredText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(defaultColor)}>{charStr}</color>");
            }
        }
        
        sentenceDisplay.text = coloredText.ToString();
    }
    
    public void SetNewSentence(string newSentence)
    {
        targetSentence = newSentence;
        InitializeSentence();
    }
    
    public void ResetPractice()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        InitializeSentence();
        BeginCountdown();
    }
    
    public float GetProgress()
    {
        if (string.IsNullOrEmpty(originalSentence))
        {
            return 0f;
        }
        return (float)currentIndex / originalSentence.Length;
    }

    public bool IsComplete()
    {
        return currentIndex >= originalSentence.Length;
    }
}
