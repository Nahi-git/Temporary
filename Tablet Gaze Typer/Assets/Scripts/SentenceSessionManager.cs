using UnityEngine;
using TMPro;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class SentenceSessionManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign or will be found at runtime.")]
    public SentenceTypingPractice sentenceTypingPractice;

    [Tooltip("Optional: show progress e.g. 'Sentence 5 of 40'")]
    public TextMeshProUGUI progressDisplay;

    [Header("Settings")]
    [Tooltip("Resource name of the sentences file (no extension). Must be in Resources folder.")]
    public string sentencesResourceName = "Sentences";
    [Tooltip("Text shown when all sentences are done.")]
    public string endOfSessionText = "End of Gaze + Touch";
    [Tooltip("After this many sentences, run the countdown again")]
    public int breakEveryNSentences = 10;

    public struct SentenceEntry
    {
        public int Id;
        public string Text;
    }

    private List<SentenceEntry> _shuffledOrder = new List<SentenceEntry>();
    private int _currentIndex = -1;
    private bool _lastFrameWasComplete;
    private bool _sessionEnded;

    public int CurrentSentenceId
    {
        get
        {
            if (_currentIndex < 0 || _currentIndex >= _shuffledOrder.Count)
                return 0;
            return _shuffledOrder[_currentIndex].Id;
        }
    }

    public int CurrentSentenceNumber => _currentIndex < 0 ? 0 : _currentIndex + 1;
    public int TotalSentences => _shuffledOrder.Count;
    public bool IsSessionActive => !_sessionEnded && _shuffledOrder.Count > 0;

    void Start()
    {
        if (sentenceTypingPractice == null)
            sentenceTypingPractice = FindObjectOfType<SentenceTypingPractice>();

        if (sentenceTypingPractice == null)
        {
            Debug.LogError("SentenceSessionManager: SentenceTypingPractice not found.");
            return;
        }

        List<SentenceEntry> loaded = LoadSentences();
        if (loaded == null || loaded.Count == 0)
        {
            Debug.LogWarning("SentenceSessionManager: No sentences loaded. Using default single sentence.");
            return;
        }

        _shuffledOrder = new List<SentenceEntry>(loaded);
        Shuffle(_shuffledOrder);
        _currentIndex = 0;
        _lastFrameWasComplete = false;
        _sessionEnded = false;

        sentenceTypingPractice.SetNewSentence(_shuffledOrder[0].Text);
        UpdateProgressDisplay();
    }

    void Update()
    {
        if (sentenceTypingPractice == null || _shuffledOrder.Count == 0 || _sessionEnded)
            return;

        bool isComplete = sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Complete;

        if (isComplete && !_lastFrameWasComplete)
        {
            _lastFrameWasComplete = true;
            _currentIndex++;

            if (_currentIndex >= _shuffledOrder.Count)
            {
                _sessionEnded = true;
                EndSession();
                Debug.Log("SentenceSessionManager: Session complete. All sentences finished.");
                return;
            }

            bool shouldBreak = breakEveryNSentences > 0 &&
                              (_currentIndex % breakEveryNSentences == 0) &&
                              _currentIndex < _shuffledOrder.Count;

            if (shouldBreak)
            {
                SentenceEntry next = _shuffledOrder[_currentIndex];
                sentenceTypingPractice.SetNewSentence(next.Text);
                sentenceTypingPractice.BeginCountdown();
                UpdateProgressDisplay();
                _lastFrameWasComplete = false;
                return;
            }

            AdvanceToNextSentence();
        }
        else if (!isComplete)
        {
            _lastFrameWasComplete = false;
        }
    }

    void AdvanceToNextSentence()
    {
        if (_currentIndex < 0 || _currentIndex >= _shuffledOrder.Count) return;
        SentenceEntry next = _shuffledOrder[_currentIndex];
        sentenceTypingPractice.SetNewSentence(next.Text);
        sentenceTypingPractice.StartTypingAgain();
        _lastFrameWasComplete = false;
        UpdateProgressDisplay();
    }

    static List<SentenceEntry> LoadSentences()
    {
        TextAsset asset = Resources.Load<TextAsset>("Sentences");
        if (asset == null)
        {
            Debug.LogWarning("SentenceSessionManager: Resources/Sentences not found. Create a .txt file in Assets/Resources named Sentences.");
            return null;
        }

        string[] lines = asset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        var list = new List<SentenceEntry>();

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            int tab = trimmed.IndexOf('\t');
            if (tab < 0) continue;

            string idStr = trimmed.Substring(0, tab).Trim();
            string text = trimmed.Substring(tab + 1).Trim();
            if (string.IsNullOrEmpty(text)) continue;

            if (!int.TryParse(idStr, out int id))
                continue;

            list.Add(new SentenceEntry { Id = id, Text = text });
        }

        return list;
    }

    static void Shuffle(List<SentenceEntry> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    void EndSession()
    {
        if (sentenceTypingPractice != null)
        {
            if (sentenceTypingPractice.keyboardPanel != null)
                sentenceTypingPractice.keyboardPanel.SetActive(false);
            if (sentenceTypingPractice.targetInputField != null)
                sentenceTypingPractice.targetInputField.interactable = false;
            if (sentenceTypingPractice.sentenceDisplay != null)
                sentenceTypingPractice.sentenceDisplay.gameObject.SetActive(false);
        }
        UpdateProgressDisplay();
    }

    void UpdateProgressDisplay()
    {
        if (progressDisplay == null) return;

        if (_sessionEnded)
        {
            progressDisplay.text = string.IsNullOrEmpty(endOfSessionText) ? "End of session" : endOfSessionText;
            progressDisplay.gameObject.SetActive(true);
            return;
        }

        if (_currentIndex < 0 || _shuffledOrder.Count == 0)
        {
            progressDisplay.text = "";
            return;
        }

        progressDisplay.text = $"Sentence {CurrentSentenceNumber} of {TotalSentences}";
    }
}
