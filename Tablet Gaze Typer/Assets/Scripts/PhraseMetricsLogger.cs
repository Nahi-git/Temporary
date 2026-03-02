using UnityEngine;
using System.IO;
using System.Text;

public class PhraseMetricsLogger : MonoBehaviour
{
    [Header("Output")]
    [Tooltip("Folder name for CSV files (same as GazeTouchSessionLogger).")]
    public string outputFolderName = "GazeTouchLogs";
    [Tooltip("Participant number used in filename. Can load from PlayerPrefs if key set.")]
    public int participantNumber = 1;
    [Tooltip("If set, participant number is loaded from this PlayerPrefs key at start.")]
    public string participantNumberPlayerPrefsKey = "GazeTouchParticipantNumber";

    [Header("Condition")]
    [Tooltip("Condition name for this run (e.g. GazeTouch, Gaze Only). Written in header and every row so data is distinguishable by condition.")]
    public string conditionName = "GazeTouch";

    private StreamWriter _writer;
    private string _currentPath;
    private bool _headerWritten;

    void Start()
    {
        if (!string.IsNullOrEmpty(participantNumberPlayerPrefsKey) && PlayerPrefs.HasKey(participantNumberPlayerPrefsKey))
            participantNumber = PlayerPrefs.GetInt(participantNumberPlayerPrefsKey, participantNumber);
    }

    void OnDestroy()
    {
        CloseWriter();
    }

    // after every phrase, we want to log the data
    public void LogPhrase(
        int phraseId,
        int sentenceNumber,
        string targetText,
        string typedText,
        float phraseStartTime,
        float firstKeystrokeTime,
        float lastKeystrokeTime,
        float endTime)
    {
        EnsureFileAndHeader();

        if (_writer == null) return;

        string targetStr = targetText ?? "";
        string typedStr = typedText ?? "";
        int typedLength = typedStr.Length;

        float taskTime = endTime - phraseStartTime;
        float timeToFirstKey = firstKeystrokeTime > 0f ? (firstKeystrokeTime - phraseStartTime) : 0f;
        float typingTime = (lastKeystrokeTime > 0f && firstKeystrokeTime > 0f) ? (lastKeystrokeTime - firstKeystrokeTime) : 0f;
        float submitLatency = lastKeystrokeTime > 0f ? (endTime - lastKeystrokeTime) : 0f;

        float taskWPM = (typedLength > 1 && taskTime > 0f) ? ((typedLength - 1) / taskTime) * 60f * 0.2f : 0f;
        float typingWPM = (typedLength > 1 && typingTime > 0f) ? ((typedLength - 1) / typingTime) * 60f * 0.2f : 0f;

        int correctChars = CountCorrectCharacters(targetStr, typedStr);
        int incorrectChars = typedLength - correctChars;
        float ter = typedLength > 0 ? (float)incorrectChars / typedLength : 0f;

        string row =
            $"{participantNumber},{Csv(conditionName)},{phraseId},{sentenceNumber}," +
            $"{Csv(targetStr)},{Csv(typedStr)}," +
            $"{taskTime:F2},{typingTime:F2},{timeToFirstKey:F2},{submitLatency:F2}," +
            $"{taskWPM:F2},{typingWPM:F2},{correctChars},{incorrectChars},{ter:F2}\n";

        _writer.Write(row);
        _writer.Flush();
        UnityEngine.Debug.Log($"[PhraseMetricsLogger] Phrase {sentenceNumber} ({conditionName}) → {_currentPath} | TaskWPM={taskWPM:F1} TypingWPM={typingWPM:F1} TER={ter:F2}");
    }

    void EnsureFileAndHeader()
    {
        if (_headerWritten && _writer != null) return;

        string folder = GetOutputFolder();
        if (string.IsNullOrEmpty(folder)) return;

        try
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[PhraseMetricsLogger] Failed to create folder: {e.Message}");
            return;
        }

        string safeCondition = string.IsNullOrEmpty(conditionName) ? "Unknown" : conditionName.Replace(",", "_").Replace("\"", "");
        string fileName = $"Participant {participantNumber} phrase_metrics {safeCondition}.csv";
        _currentPath = Path.Combine(folder, fileName);
        bool exists = File.Exists(_currentPath);

        try
        {
            _writer = new StreamWriter(_currentPath, true, Encoding.UTF8);
            if (!exists)
            {
                const string header =
                    "ParticipantID,Condition,PhraseID,SentenceNumber,TargetText,TypedText," +
                    "TaskTime,TypingTime,TimeToFirstKey,SubmitLatency," +
                    "TaskWPM,TypingWPM,CorrectChars,IncorrectChars,TER\n";
                _writer.Write(header);
                _writer.Flush();
                UnityEngine.Debug.Log($"[PhraseMetricsLogger] Created {_currentPath} with header (Condition = {conditionName})");
            }
            _headerWritten = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[PhraseMetricsLogger] Failed to open {_currentPath}: {e.Message}");
            _writer = null;
        }
    }

    static string GetProjectRootPath()
    {
        string dataPath = Application.dataPath;
        return Directory.GetParent(Directory.GetParent(dataPath).FullName).FullName;
    }

    string GetOutputFolder()
    {
        string root = GetProjectRootPath();
        return string.IsNullOrEmpty(outputFolderName) ? root : Path.Combine(root, outputFolderName);
    }

    void CloseWriter()
    {
        if (_writer != null)
        {
            try { _writer.Close(); _writer.Dispose(); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[PhraseMetricsLogger] Close error: {e.Message}"); }
            _writer = null;
        }
    }

    static int CountCorrectCharacters(string target, string typed)
    {
        int correct = 0;
        int minLen = Mathf.Min(target.Length, typed.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (target[i] == typed[i]) correct++;
        }
        return correct;
    }

    static string Csv(string v)
    {
        if (v == null) return "";
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
