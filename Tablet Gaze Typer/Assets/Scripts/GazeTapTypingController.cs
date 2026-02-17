using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

public class GazeTapTypingController : MonoBehaviour
{
    [Header("References")]
    public KeyboardHighlighter keyboardHighlighter;
    public TMP_InputField targetInputField;
    public SentenceTypingPractice sentenceTypingPractice;

    private bool isShiftActive;
    private Dictionary<Button, string> originalButtonTexts = new Dictionary<Button, string>();

    void Start()
    {
        if (keyboardHighlighter == null) keyboardHighlighter = FindObjectOfType<KeyboardHighlighter>();
        if (targetInputField == null) targetInputField = FindObjectOfType<TMP_InputField>();
        if (sentenceTypingPractice == null) sentenceTypingPractice = FindObjectOfType<SentenceTypingPractice>();
        Invoke(nameof(StoreOriginalKeyboardTexts), 0.1f);
    }

    void StoreOriginalKeyboardTexts()
    {
        GameObject panel = keyboardHighlighter != null ? keyboardHighlighter.keyboardPanel : null;
        if (panel == null) return;
        originalButtonTexts.Clear();
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
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
        GameObject panel = keyboardHighlighter != null ? keyboardHighlighter.keyboardPanel : null;
        if (panel == null) return;
        foreach (var kv in originalButtonTexts)
        {
            if (kv.Key == null) continue;
            string displayText = ApplyShiftToText(kv.Value);
            var tmp = kv.Key.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) { tmp.text = displayText; tmp.SetAllDirty(); continue; }
            var text = kv.Key.GetComponentInChildren<Text>(true);
            if (text != null) text.text = displayText;
        }
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null) Canvas.ForceUpdateCanvases();
    }

    void Update()
    {
        if (!WasTapThisFrame()) return;
        if (sentenceTypingPractice != null && sentenceTypingPractice.State != SentenceTypingPractice.PracticeState.Typing)
            return;

        Button highlighted = keyboardHighlighter != null ? keyboardHighlighter.CurrentlyHighlightedButton : null;
        if (highlighted == null) return;

        TypeKey(highlighted);
    }

    bool WasTapThisFrame()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0 && touchscreen.touches[0].press.wasPressedThisFrame)
            return true;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;
        return false;
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
