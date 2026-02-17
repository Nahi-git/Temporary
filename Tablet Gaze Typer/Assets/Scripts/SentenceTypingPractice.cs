using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;
using System.Collections;

public class SentenceTypingPractice : MonoBehaviour
{
    public enum PracticeState { Countdown, Typing, Complete }
    
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
    
    [Header("Sentence Settings")]
    [Tooltip("The sentence the user should type")]
    [TextArea(2, 5)]
    public string targetSentence = "The quick brown fox jumps over the lazy dog";
    
    [Header("Countdown & Timing")]
    [Tooltip("Countdown duration in seconds before typing starts")]
    public float countdownSeconds = 10f;
    
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
    private Coroutine countdownCoroutine;
    public PracticeState State => state;
    
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
        EnsureSentenceDisplayUniformSize();
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
    }
    
    void BeginCountdown()
    {
        state = PracticeState.Countdown;
        if (targetInputField != null)
        {
            targetInputField.interactable = false;
            targetInputField.text = "";
            previousInputText = "";
        }
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(false);
        }
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }
    
    IEnumerator CountdownRoutine()
    {
        int remaining = Mathf.CeilToInt(countdownSeconds);
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
        state = PracticeState.Typing;
        typingStartTime = Time.time;
        
        if (targetInputField != null)
        {
            targetInputField.interactable = true;
        }
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(true);
        }
    }
    
    void OnInputFieldChanged(string newText)
    {
        if (string.IsNullOrEmpty(originalSentence) || isProcessingInput)
        {
            return;
        }
        if (state != PracticeState.Typing)
        {
            previousInputText = newText;
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
        
        if (targetInputField != null)
        {
            targetInputField.interactable = false;
        }
        
        string timeText = FormatTime(elapsed);
        if (resultDisplay != null)
        {
            resultDisplay.gameObject.SetActive(true);
            resultDisplay.text = "Time: " + timeText;
        }
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
