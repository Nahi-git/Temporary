using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.IO;
using System.Text;

//main point of this is to log as much data as possible about the user's typing performance
public class GazeTouchSessionLogger : MonoBehaviour
{
    [Header("References")]
    public SentenceTypingPractice sentenceTypingPractice;
    [Tooltip("Optional. If set, SentenceId is written to CSV for each row.")]
    public SentenceSessionManager sentenceSessionManager;
    public UnityGazeCalibrator gazeCalibrator;
    public GazeWebSocketClient gazeSource;
    public KeyboardHighlighter keyboardHighlighter;
    public ThumbTypingController thumbTypingController;
    
    [Header("Output Location")]
    [Tooltip("Folder name for CSV files, next to the project README (outside the Unity project). Created if it doesn't exist.")]
    public string outputFolderName = "GazeTouchLogs";
    [Tooltip("Name used in the CSV filename: 'Participant X {logFileNameSuffix}.csv'.")]
    public string logFileNameSuffix = "GazeTouch";
    [Header("Logging Rate")]
    [Tooltip("Log frame rows at this rate (Hz). 90 = 90 times per second. Key presses and final frame are always logged immediately.")]
    public float logRateHz = 90f;
    [Header("Participant")]
    [Tooltip("Participant number used in filename (Participant X GazeTouch.csv). Can be set manually or loaded from PlayerPrefs.")]
    public int participantNumber = 1;
    [Tooltip("If set, participant number is loaded from this PlayerPrefs key at start.")]
    public string participantNumberPlayerPrefsKey = "GazeTouchParticipantNumber";
    
    private StreamWriter _writer;
    private float _lastFrameLogTime = -1f;
    private bool _typedThisFrame;
    private string _characterTypedThisFrame = "";
    private bool _loggingActive;
    private float _loggingStartTime;
    
    //for when a key is typed, we want to log the data at that exactmoment
    private bool _hasSnapshotAtType;
    private Vector2 _snapshotCenterGrid;
    private string _snapshotCenterKeyLabel = "";
    private string _snapshotSelectedKeyLabel = "";
    private Vector2 _snapshotGaze;
    private string _snapshotGazeNearestKey = "";
    private bool _snapshotTouchOn;
    private Vector2 _snapshotTouchPosition;
    private bool _snapshotThreeByThreeDisplayed;
    private bool _snapshotCorrectKeyInThreeByThree;
    
    void Start()
    {
        if (sentenceTypingPractice == null) sentenceTypingPractice = FindObjectOfType<SentenceTypingPractice>();
        if (sentenceSessionManager == null) sentenceSessionManager = FindObjectOfType<SentenceSessionManager>();
        if (gazeCalibrator == null) gazeCalibrator = FindObjectOfType<UnityGazeCalibrator>();
        if (gazeSource == null) gazeSource = FindObjectOfType<GazeWebSocketClient>();
        if (keyboardHighlighter == null) keyboardHighlighter = FindObjectOfType<KeyboardHighlighter>();
        if (thumbTypingController == null) thumbTypingController = FindObjectOfType<ThumbTypingController>();
        if (participantNumberPlayerPrefsKey != null && PlayerPrefs.HasKey(participantNumberPlayerPrefsKey))
        {
            participantNumber = PlayerPrefs.GetInt(participantNumberPlayerPrefsKey, participantNumber);
        }
        ThumbTypingController.OnCharacterTyped += OnCharacterTyped;
    }
    
    void OnDestroy()
    {
        ThumbTypingController.OnCharacterTyped -= OnCharacterTyped;
        StopLogging();
    }
    
    //when a key is typed, we want to log the data at that exact moment
    void OnCharacterTyped(string character)
    {
        _typedThisFrame = true;
        _characterTypedThisFrame = character ?? "";
        _hasSnapshotAtType = true;
        if (thumbTypingController != null)
        {
            _snapshotCenterGrid = thumbTypingController.GetCenterGridScreenPosition();
            _snapshotCenterKeyLabel = thumbTypingController.GetCenterKeyLabel();
            _snapshotSelectedKeyLabel = thumbTypingController.GetSelectedKeyLabel();
        }
        else
        {
            _snapshotCenterGrid = Vector2.zero;
            _snapshotCenterKeyLabel = "";
            _snapshotSelectedKeyLabel = "";
        }
        _snapshotGaze = ConvertGazeToUnityScreenSpace(GetGazeCoords());
        _snapshotGazeNearestKey = keyboardHighlighter != null ? keyboardHighlighter.GetNearestKeyLabel() : "";
        CaptureTouchPositionAtType(out _snapshotTouchOn, out _snapshotTouchPosition);
        
        //check if the character that was just typed is in the 3x3 grid
        string characterJustTyped = _characterTypedThisFrame ?? "";
        _snapshotThreeByThreeDisplayed = thumbTypingController != null ? thumbTypingController.IsThreeByThreeDisplayed() : false;
        _snapshotCorrectKeyInThreeByThree = thumbTypingController != null && _snapshotThreeByThreeDisplayed 
            ? thumbTypingController.IsCharacterInThreeByThree(characterJustTyped) : false;
    }
    
    //because we capture frame by frame, we need to capture the touch position at the exact moment the key is typed in case the input was too fast
    void CaptureTouchPositionAtType(out bool touchOn, out Vector2 position)
    {
        position = Vector2.zero;
        touchOn = false;
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
        {
            position = touchscreen.touches[0].position.ReadValue();
            touchOn = true;
            return;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            touchOn = true;
        }
    }
    
    void Update()
    {
        if (sentenceTypingPractice == null) return;

        float interval = logRateHz > 0f ? 1f / logRateHz : 1f / 90f;
        bool shouldLogThisFrame = false;

        if (sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Typing)
        {
            if (!_loggingActive)
            {
                StartLogging();
            }
            //log at 90 Hz or immediately when a key was typed
            shouldLogThisFrame = _typedThisFrame || (Time.time - _lastFrameLogTime >= interval);
            if (shouldLogThisFrame)
                _lastFrameLogTime = Time.time;
            if (shouldLogThisFrame)
                WriteFrameRow();
        }
        else if (_loggingActive && (sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Countdown || sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Break))
        {
            shouldLogThisFrame = (Time.time - _lastFrameLogTime >= interval);
            if (shouldLogThisFrame)
                _lastFrameLogTime = Time.time;
            if (shouldLogThisFrame)
                WriteFrameRow();
        }
        else if (_loggingActive && sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Complete)
        {
            WriteFrameRow(); //should fix issue where the last frame is not logged
            StopLogging();
        }
        
        _typedThisFrame = false;
        _characterTypedThisFrame = "";
        _hasSnapshotAtType = false;
    }
    
    static string GetProjectRootPath()
    {
        string dataPath = Application.dataPath;
        string projectFolder = Directory.GetParent(dataPath).FullName;
        return Directory.GetParent(projectFolder).FullName;
    }
    
    void StartLogging()
    {
        string root = GetProjectRootPath();
        string folder = string.IsNullOrEmpty(outputFolderName) ? root : Path.Combine(root, outputFolderName);
        
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"GazeTouchSessionLogger: Failed to create folder {folder}: {e.Message}");
            return;
        }
        
        string suffix = string.IsNullOrEmpty(logFileNameSuffix) ? "GazeTouch" : logFileNameSuffix;
        string dateTime = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
        string fileName = $"Participant {participantNumber} {suffix} {dateTime}.csv";
        string path = Path.Combine(folder, fileName);
        
        try
        {
            _writer = new StreamWriter(path, false, Encoding.UTF8);
            _writer.WriteLine("Time,SentenceId,GazeX,GazeY,GazeNearestKey,TouchOn,TouchX,TouchY,CenterGridX,CenterGridY,CenterKeyLabel,SelectedKeyLabel,CharacterToBeTyped,TypeFlag,CharacterTyped,InBreak,ThreeByThreeDisplayed,CorrectKeyInThreeByThree");
            _writer.Flush();
            _loggingActive = true;
            _loggingStartTime = Time.time;
            UnityEngine.Debug.Log($"GazeTouchSessionLogger: Started logging to {path}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"GazeTouchSessionLogger: Failed to open file {path}: {e.Message}");
        }
    }
    
    void WriteFrameRow()
    {
        if (_writer == null || !_loggingActive) return;
        
        float time = _loggingActive ? Time.time - _loggingStartTime : 0f;
        bool touchOn;
        Vector2 touchPos;
        if (_typedThisFrame && _hasSnapshotAtType)
        {
            touchOn = _snapshotTouchOn;
            touchPos = _snapshotTouchPosition;
        }
        else
        {
            touchOn = GetTouchOn(out touchPos);
        }
        int typeFlag = _typedThisFrame ? 1 : 0;
        string characterTyped = EscapeCsv(_characterTypedThisFrame);
        string characterToBeTyped = (typeFlag == 1 && !string.IsNullOrEmpty(_characterTypedThisFrame))
            ? _characterTypedThisFrame
            : (sentenceTypingPractice != null ? sentenceTypingPractice.GetCharacterToBeTyped() : "");
        
        Vector2 gaze;
        string gazeNearestKey;
        Vector2 centerGrid;
        string centerKeyLabel;
        string selectedKeyLabel;
        bool threeByThreeDisplayed;
        bool correctKeyInThreeByThree;
        
        if (_typedThisFrame && _hasSnapshotAtType)
        {
            gaze = _snapshotGaze;
            gazeNearestKey = _snapshotGazeNearestKey;
            centerGrid = _snapshotCenterGrid;
            centerKeyLabel = _snapshotCenterKeyLabel;
            selectedKeyLabel = _snapshotSelectedKeyLabel;
            threeByThreeDisplayed = _snapshotThreeByThreeDisplayed;
            correctKeyInThreeByThree = _snapshotCorrectKeyInThreeByThree;
            _hasSnapshotAtType = false;
        }
        else
        {
            gaze = ConvertGazeToUnityScreenSpace(GetGazeCoords());
            gazeNearestKey = keyboardHighlighter != null ? keyboardHighlighter.GetNearestKeyLabel() : "";
            centerGrid = thumbTypingController != null ? thumbTypingController.GetCenterGridScreenPosition() : Vector2.zero;
            centerKeyLabel = thumbTypingController != null ? thumbTypingController.GetCenterKeyLabel() : "";
            selectedKeyLabel = thumbTypingController != null ? thumbTypingController.GetSelectedKeyLabel() : "";
            threeByThreeDisplayed = thumbTypingController != null ? thumbTypingController.IsThreeByThreeDisplayed() : false;
            correctKeyInThreeByThree = thumbTypingController != null && threeByThreeDisplayed 
                ? thumbTypingController.IsCharacterInThreeByThree(characterToBeTyped) : false;
        }
        
        gazeNearestKey = EscapeCsv(gazeNearestKey);
        centerKeyLabel = EscapeCsv(centerKeyLabel);
        selectedKeyLabel = EscapeCsv(selectedKeyLabel);
        characterToBeTyped = EscapeCsv(characterToBeTyped);
        int sentenceId = sentenceSessionManager != null ? sentenceSessionManager.CurrentSentenceId : 0;
        bool inCountdown = sentenceTypingPractice != null && sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Countdown;
        bool inBreakState = sentenceTypingPractice != null && sentenceTypingPractice.State == SentenceTypingPractice.PracticeState.Break;
        int inBreak = (inCountdown || inBreakState) ? 1 : 0;

        _writer.WriteLine($"{time:F3},{sentenceId},{gaze.x:F2},{gaze.y:F2},{gazeNearestKey},{(touchOn ? 1 : 0)},{touchPos.x:F2},{touchPos.y:F2},{centerGrid.x:F2},{centerGrid.y:F2},{centerKeyLabel},{selectedKeyLabel},{characterToBeTyped},{typeFlag},{characterTyped},{inBreak},{(threeByThreeDisplayed ? 1 : 0)},{(correctKeyInThreeByThree ? 1 : 0)}");
        _writer.Flush();
    }
    
    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
    
    Vector2 GetGazeCoords()
    {
        if (keyboardHighlighter != null)
        {
            Vector2 g = keyboardHighlighter.GetGazePositionForExternal();
            if (g != Vector2.zero)
                return g;
        }
        if (gazeCalibrator != null && gazeCalibrator.calibrated)
        {
            return gazeCalibrator.calibratedGaze;
        }
        if (gazeSource != null)
        {
            return gazeSource.rawGaze;
        }
        return Vector2.zero;
    }
    
    //due to gaze implementation, the gaze coordinates are in the top-left corner of the screen
    //we need to convert them to the bottom-left corner of the screen to be comparable to the touch coordinates
    Vector2 ConvertGazeToUnityScreenSpace(Vector2 gaze)
    {
        if (gaze == Vector2.zero) return Vector2.zero;
        return new Vector2(gaze.x, Screen.height - gaze.y);
    }
    
    bool GetTouchOn(out Vector2 position)
    {
        position = Vector2.zero;
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
        {
            var touch = touchscreen.touches[0];
            if (touch.press.isPressed)
            {
                position = touch.position.ReadValue();
                return true;
            }
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            position = mouse.position.ReadValue();
            return true;
        }
        return false;
    }
    
    void StopLogging()
    {
        if (_writer != null)
        {
            try
            {
                _writer.Close();
                _writer.Dispose();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"GazeTouchSessionLogger: Error closing file: {e.Message}");
            }
            _writer = null;
        }
        _loggingActive = false;
    }
}
