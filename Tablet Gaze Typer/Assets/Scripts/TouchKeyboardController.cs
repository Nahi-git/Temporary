using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

public class TouchKeyboardController : MonoBehaviour
{
    [Header("References")]
    public GameObject keyboardPanel;
    public TMP_InputField targetInputField;
    public SentenceTypingPractice sentenceTypingPractice;

    private bool isShiftActive;
    private Dictionary<Button, string> originalButtonTexts = new Dictionary<Button, string>();

    void Start()
    {
        if (keyboardPanel == null)
        {
            var highlighter = FindFirstObjectByType<KeyboardHighlighter>();
            if (highlighter != null) keyboardPanel = highlighter.keyboardPanel;
        }
        if (targetInputField == null) targetInputField = FindFirstObjectByType<TMP_InputField>();
        if (sentenceTypingPractice == null) sentenceTypingPractice = FindFirstObjectByType<SentenceTypingPractice>();

        Invoke(nameof(SetupKeyboard), 0.1f);
    }

    void SetupKeyboard()
    {
        StoreOriginalKeyboardTexts();
        if (keyboardPanel == null) return;
        foreach (Button button in keyboardPanel.GetComponentsInChildren<Button>(true))
        {
            Button b = button;
            b.onClick.AddListener(() => OnKeyClicked(b));
        }
    }

    void OnKeyClicked(Button button)
    {
        if (sentenceTypingPractice != null && sentenceTypingPractice.State != SentenceTypingPractice.PracticeState.Typing)
            return;
        TypeKey(button);
    }

    void StoreOriginalKeyboardTexts()
    {
        if (keyboardPanel == null) return;
        originalButtonTexts.Clear();
        foreach (Button button in keyboardPanel.GetComponentsInChildren<Button>(true))
        {
            string text = GetRawButtonText(button);
            if (!string.IsNullOrEmpty(text) && text.Length == 1 && char.IsLetter(text[0]))
                text = text.ToLower();
            originalButtonTexts[button] = text;
        }
        UpdateKeyboardDisplay();
    }

    void UpdateKeyboardDisplay()
    {
        if (keyboardPanel == null) return;
        foreach (var kv in originalButtonTexts)
        {
            if (kv.Key == null) continue;
            string displayText = ApplyShiftToText(kv.Value);
            var tmp = kv.Key.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) { tmp.text = displayText; tmp.SetAllDirty(); continue; }
            var text = kv.Key.GetComponentInChildren<Text>(true);
            if (text != null) text.text = displayText;
        }
        var canvas = keyboardPanel.GetComponentInParent<Canvas>();
        if (canvas != null) Canvas.ForceUpdateCanvases();
    }

    void TypeKey(Button button)
    {
        string keyText = GetRawButtonText(button);
        string buttonName = button.gameObject.name;
        string normalized = keyText.Trim().ToLower();

        bool isShift = buttonName.ToLower().Contains("shift") || normalized == "shift" || normalized == "⇧";
        if (isShift)
        {
            isShiftActive = !isShiftActive;
            UpdateKeyboardDisplay();
            return;
        }

        string displayText = ApplyShiftToText(keyText);
        bool isSpace = buttonName == "Key_Space";
        bool isBackspace = buttonName == "Key_Backspace";

        if (isSpace)
            displayText = " ";
        else if (isBackspace)
            displayText = "";
        else if (string.IsNullOrEmpty(displayText))
            return;

        bool isBackspaceByText = normalized == "backspace" || normalized == "←" || normalized == "delete";
        bool isSpaceByText = normalized == "space" || normalized == "space bar" || normalized == "spacebar";

        string processedText;
        if (isBackspace || isBackspaceByText)
            processedText = "";
        else if (isSpace || isSpaceByText)
            processedText = " ";
        else
            processedText = ProcessSpecialKey(displayText);

        bool isLetter = !string.IsNullOrEmpty(processedText) && processedText.Length == 1 && char.IsLetter(processedText[0]);
        if (isLetter && isShiftActive)
        {
            isShiftActive = false;
            UpdateKeyboardDisplay();
        }

        TMP_InputField field = targetInputField;
        if (field == null)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null) field = selected.GetComponent<TMP_InputField>();
        }
        if (field == null) return;

        if (isBackspace || isBackspaceByText)
        {
            if (field.text.Length > 0)
            {
                field.text = field.text.Substring(0, field.text.Length - 1);
                field.caretPosition = field.text.Length;
            }
            ThumbTypingController.NotifyCharacterTyped("");
        }
        else if (!string.IsNullOrEmpty(processedText))
        {
            field.text += processedText;
            field.caretPosition = field.text.Length;
            ThumbTypingController.NotifyCharacterTyped(processedText);
        }
    }

    static string GetRawButtonText(Button button)
    {
        if (button == null) return "";
        var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text.Trim();
        var text = button.GetComponentInChildren<Text>();
        if (text != null && !string.IsNullOrEmpty(text.text)) return text.text.Trim();
        return "";
    }

    string ApplyShiftToText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length != 1) return text;
        if (char.IsLetter(text[0]))
            return isShiftActive ? text.ToUpper() : text.ToLower();
        return text;
    }

    static string ProcessSpecialKey(string keyText)
    {
        string normalized = keyText.Trim().ToLower();
        if (normalized == "space" || normalized == "space bar" || normalized == "spacebar" || normalized == " ")
            return " ";
        switch (normalized)
        {
            case "enter":
            case "return":
                return "\n";
            case "tab":
                return "\t";
            default:
                if (keyText.Length == 1) return keyText;
                return "";
        }
    }
}
