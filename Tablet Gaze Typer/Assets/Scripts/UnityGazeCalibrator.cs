using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UnityGazeCalibrator : MonoBehaviour
{
    //ensure only one instance of the calibrator exists
    private static UnityGazeCalibrator _instance;
    public static UnityGazeCalibrator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UnityGazeCalibrator>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UnityGazeCalibrator");
                    _instance = go.AddComponent<UnityGazeCalibrator>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("References")]
    public GazeWebSocketClient gazeClient;
    public RectTransform calibrationDot;   
    public TextMeshProUGUI instructionText;
    public GameObject keyboardPanel;
    [Tooltip("Button or panel to show after calibration (e.g. 'Go to Gaze Typer'). Assign and it will be shown when calibration completes.")]
    public GameObject postCalibrationButton;          

    [Header("Calibration Settings")]
    public float marginPx = 120f;          
    public float sampleSeconds = 1.2f;     
    public KeyCode captureKey = KeyCode.Space;

    [Tooltip("If true, this calibrator will persist across scene changes")]
    public bool persistAcrossScenes = true;

    [Header("Output")]
    public bool calibrated;
    public Vector2 calibratedGaze;         

    // Affine: X = ax + by + c, Y = dx + ey + f
    float a, b, c, d, e, f;

    //smoothing
    public bool smoothing = true;
    [Range(0.01f, 1f)] public float emaAlpha = 0.25f;
    Vector2 ema;

    List<Vector2> targetPoints = new List<Vector2>(); // true screen coords
    List<Vector2> measuredPoints = new List<Vector2>(); // averaged raw gaze

    int index = 0;
    bool isCalibrating = false; 

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }
        else if (_instance != this)
        {
            //if another instance already exists, destroy this one
            UnityEngine.Debug.LogWarning("UnityGazeCalibrator: Another instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        //unsubscribe from scene loaded events
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //update UI references when a new scene is loaded
        UpdateUIReferences();
        UnityEngine.Debug.Log($"UnityGazeCalibrator: Scene '{scene.name}' loaded. UI references updated.");
    }

    void Start()
    {
        //auto find references if not assigned
        if (gazeClient == null)
        {
            gazeClient = FindObjectOfType<GazeWebSocketClient>();
            if (!gazeClient) UnityEngine.Debug.LogError("UnityGazeCalibrator: Assign GazeWebSocketClient or ensure one exists in the scene");
        }
        
        if (calibrationDot == null)
        {
            //try to find calibration dot in scene
            GameObject dotObj = GameObject.Find("CalibrationDot");
            if (dotObj != null)
            {
                calibrationDot = dotObj.GetComponent<RectTransform>();
            }
            if (calibrationDot == null) UnityEngine.Debug.LogWarning("UnityGazeCalibrator: Assign calibrationDot (UI) - calibration UI may not work");
        }
        
        if (instructionText == null)
        {
            GameObject textObj = GameObject.Find("InstructionText");
            if (textObj != null)
            {
                instructionText = textObj.GetComponent<TextMeshProUGUI>();
            }
        }
        
        if (keyboardPanel == null)
        {
            keyboardPanel = GameObject.Find("KeyboardPanel");
        }

        Build9PointLayout();
        HideDot();
        HideKeyboard();
        HideInstructionText();
        HidePostCalibrationButton();
        LoadCalibrationData();
    }

    void Update()
    {
        //start calibration
        if (!isCalibrating && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            StartCalibration();
        }

        //if calibrating, space/tap/click captures for current point
        bool captureInput = false;
        if (isCalibrating && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            captureInput = true;
        }
        //check for touch input
        if (isCalibrating && !captureInput)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                if (touchscreen.touches[0].press.wasPressedThisFrame)
                {
                    captureInput = true;
                }
            }
        } 
        //check for mouse click
        if (isCalibrating && !captureInput)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                captureInput = true;
            }
        }
        
        if (captureInput)
        {
            StartCoroutine(CaptureCurrentPoint());
        }

        //update calibrated gaze continuously
        if (calibrated)
        {
            var raw = gazeClient.rawGaze;
            var mapped = ApplyAffine(raw);
            
            //clamp calibrated gaze to screen bounds
            mapped.x = Mathf.Clamp(mapped.x, 0, Screen.width);
            mapped.y = Mathf.Clamp(mapped.y, 0, Screen.height);
            
            if (smoothing)
            {
                ema = Vector2.Lerp(ema, mapped, emaAlpha);
                calibratedGaze = ema;
            }
            else calibratedGaze = mapped;
        }
    }

    public Vector2 GetCalibratedGazeOrRaw()
    {
        return calibrated ? calibratedGaze : gazeClient.rawGaze;
    }

    public void StartCalibration()
    {
        calibrated = false;
        isCalibrating = true;
        index = 0;
        measuredPoints.Clear();
        targetPoints.Clear();
        Build9PointLayout();

        if (gazeClient != null && gazeClient.browserWindowSize.x > 0 && gazeClient.browserWindowSize.y > 0)
        {
            if (layoutWidth != gazeClient.browserWindowSize.x || layoutHeight != gazeClient.browserWindowSize.y)
            {
                UnityEngine.Debug.LogWarning($"Layout size mismatch: layout={layoutWidth}x{layoutHeight}, browser={gazeClient.browserWindowSize.x}x{gazeClient.browserWindowSize.y}");
            }
        }
        HideKeyboard();
        ShowInstructionText();
        ShowDotAt(layout[index]);
    }

    // --- layout storage ---
    Vector2[] layout;
    float layoutWidth, layoutHeight;

    void Build9PointLayout()
    {
        //use browser window size if available, otherwise fall back to Unity screen size
        float w, h;
        if (gazeClient != null && gazeClient.browserWindowSize.x > 0 && gazeClient.browserWindowSize.y > 0)
        {
            w = gazeClient.browserWindowSize.x;
            h = gazeClient.browserWindowSize.y;
            UnityEngine.Debug.Log($"Using browser window size for calibration: {w}x{h}");
        }
        else
        {
            w = Screen.width;
            h = Screen.height;
            UnityEngine.Debug.Log($"Using Unity screen size for calibration: {w}x{h}");
        }

        layoutWidth = w;
        layoutHeight = h;
        float left = marginPx;
        float right = w - marginPx;
        float top = marginPx;
        float bottom = h - marginPx;
        float midX = w * 0.5f;
        float midY = h * 0.5f;

        layout = new Vector2[]
        {
            new Vector2(left,  top),
            new Vector2(midX,  top),
            new Vector2(right, top),

            new Vector2(left,  midY),
            new Vector2(midX,  midY),
            new Vector2(right, midY),

            new Vector2(left,  bottom),
            new Vector2(midX,  bottom),
            new Vector2(right, bottom)
        };
    }

    IEnumerator CaptureCurrentPoint()
    {
        if (!isCalibrating) yield break;

        yield return new WaitForSeconds(0.2f);

        float t = 0f;
        List<Vector2> samples = new List<Vector2>();

        while (t < sampleSeconds)
        {
            var g = gazeClient.rawGaze;
            //more lenient validation - accept wider range of values
            //webgazer can sometimes give values outside screen bounds but still valid
            if (!float.IsNaN(g.x) && !float.IsNaN(g.y) &&
                !float.IsInfinity(g.x) && !float.IsInfinity(g.y) &&
                g.x > -Screen.width && g.x < Screen.width * 2f &&
                g.y > -Screen.height && g.y < Screen.height * 2f)
            {
                samples.Add(g);
            }
            t += Time.deltaTime;
            yield return null;
        }

        Vector2 avg;
        
        if (samples.Count == 0)
        {
            avg = gazeClient.rawGaze;
            UnityEngine.Debug.LogWarning($"No valid samples for point {index+1}, using current raw gaze: ({avg.x:F0}, {avg.y:F0})");
        }
        else if (samples.Count < 5)
        {
            Vector2 sum = Vector2.zero;
            foreach (var s in samples) sum += s;
            avg = sum / samples.Count;
        }
        else
        {
            //use median for better robustness to outliers
            avg = CalculateMedian(samples);
            if (samples.Count > 10)
            {
                Vector2 filteredAvg = FilterOutliers(samples, avg);
                avg = filteredAvg;
            }
        }

        measuredPoints.Add(avg);
        targetPoints.Add(layout[index]);

        index++;

        if (index >= layout.Length)
        {
            // solve mapping
            SolveAffine(measuredPoints, targetPoints);
            calibrated = true;
            isCalibrating = false;
            ema = ApplyAffine(gazeClient.rawGaze);

            SaveCalibrationData();
            HideDot();
            HideInstructionText();
            ShowKeyboard();
            ShowPostCalibrationButton();
        }
        else
        {
            ShowDotAt(layout[index]);
        }
    }

    //UI helpers (dot positions in Canvas space)
    void ShowDotAt(Vector2 screenPx)
    {
        if (calibrationDot != null)
        {
            calibrationDot.gameObject.SetActive(true);
            calibrationDot.anchoredPosition = ScreenPxToCanvasAnchored(screenPx);
        }
    }

    void HideDot()
    {
        if (calibrationDot != null)
        {
            calibrationDot.gameObject.SetActive(false);
        }
    }

    void ShowKeyboard()
    {
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(true);
        }
    }

    void HideKeyboard()
    {
        if (keyboardPanel != null)
        {
            keyboardPanel.SetActive(false);
        }
    }

    void ShowInstructionText()
    {
        if (instructionText != null)
            instructionText.gameObject.SetActive(true);
    }

    void HideInstructionText()
    {
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);
    }

    void ShowPostCalibrationButton()
    {
        if (postCalibrationButton != null)
            postCalibrationButton.SetActive(true);
    }

    void HidePostCalibrationButton()
    {
        if (postCalibrationButton != null)
            postCalibrationButton.SetActive(false);
    }

    //call this when switching scenes to update UI references
    public void UpdateUIReferences()
    {
        //auto find references in the new scene
        if (gazeClient == null)
        {
            gazeClient = FindObjectOfType<GazeWebSocketClient>();
        }
        
        if (calibrationDot == null)
        {
            GameObject dotObj = GameObject.Find("CalibrationDot");
            if (dotObj != null)
            {
                calibrationDot = dotObj.GetComponent<RectTransform>();
            }
        }
        
        if (instructionText == null)
        {
            GameObject textObj = GameObject.Find("InstructionText");
            if (textObj != null)
            {
                instructionText = textObj.GetComponent<TextMeshProUGUI>();
            }
        }
        
        if (keyboardPanel == null)
        {
            keyboardPanel = GameObject.Find("KeyboardPanel");
        }
    }

    //save calibration data to PlayerPrefs
    void SaveCalibrationData()
    {
        PlayerPrefs.SetInt("GazeCalibrated", calibrated ? 1 : 0);
        PlayerPrefs.SetFloat("GazeCal_a", a);
        PlayerPrefs.SetFloat("GazeCal_b", b);
        PlayerPrefs.SetFloat("GazeCal_c", c);
        PlayerPrefs.SetFloat("GazeCal_d", d);
        PlayerPrefs.SetFloat("GazeCal_e", e);
        PlayerPrefs.SetFloat("GazeCal_f", f);
        PlayerPrefs.Save();
        UnityEngine.Debug.Log("Calibration data saved to PlayerPrefs");
    }

    //load calibration data from PlayerPrefs
    public bool LoadCalibrationData()
    {
        if (PlayerPrefs.HasKey("GazeCalibrated") && PlayerPrefs.GetInt("GazeCalibrated") == 1)
        {
            a = PlayerPrefs.GetFloat("GazeCal_a");
            b = PlayerPrefs.GetFloat("GazeCal_b");
            c = PlayerPrefs.GetFloat("GazeCal_c");
            d = PlayerPrefs.GetFloat("GazeCal_d");
            e = PlayerPrefs.GetFloat("GazeCal_e");
            f = PlayerPrefs.GetFloat("GazeCal_f");
            calibrated = true;
            
            if (gazeClient != null)
            {
                ema = ApplyAffine(gazeClient.rawGaze);
            }
            
            UnityEngine.Debug.Log("Calibration data loaded from PlayerPrefs");
            return true;
        }
        return false;
    }

    //clear saved calibration data
    public void ClearCalibrationData()
    {
        PlayerPrefs.DeleteKey("GazeCalibrated");
        PlayerPrefs.DeleteKey("GazeCal_a");
        PlayerPrefs.DeleteKey("GazeCal_b");
        PlayerPrefs.DeleteKey("GazeCal_c");
        PlayerPrefs.DeleteKey("GazeCal_d");
        PlayerPrefs.DeleteKey("GazeCal_e");
        PlayerPrefs.DeleteKey("GazeCal_f");
        calibrated = false;
        UnityEngine.Debug.Log("Calibration data cleared");
    }

    Vector2 ScreenPxToCanvasAnchored(Vector2 screenPx)
    {
        if (calibrationDot == null) return Vector2.zero;
        
        Canvas canvas = calibrationDot.GetComponentInParent<Canvas>();
        if (canvas == null) return Vector2.zero;
        
        Camera camera = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
        {
            camera = canvas.worldCamera;
        }
        
        Vector2 unityScreenPoint = screenPx;
        
        if (layoutWidth > 0 && layoutHeight > 0 && 
            (Mathf.Abs(layoutWidth - Screen.width) > 1f || Mathf.Abs(layoutHeight - Screen.height) > 1f))
        {
            float scaleX = Screen.width / layoutWidth;
            float scaleY = Screen.height / layoutHeight;
            unityScreenPoint = new Vector2(screenPx.x * scaleX, screenPx.y * scaleY);
        }
        float flippedY = Screen.height - unityScreenPoint.y;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            new Vector2(unityScreenPoint.x, flippedY),
            camera,
            out localPoint);
        
        return localPoint;
    }

    Vector2 ApplyAffine(Vector2 raw)
    {
        float X = a * raw.x + b * raw.y + c;
        float Y = d * raw.x + e * raw.y + f;
        return new Vector2(X, Y);
    }

    void SolveAffine(List<Vector2> raw, List<Vector2> target)
    {
        //least squares for 3 parameters per output:
        // [x y 1] * [a b c]^T = X
        // [x y 1] * [d e f]^T = Y
        //solve using normal equations: (M^T M)p = (M^T v)

        int N = raw.Count;
        if (N < 3)
        {
            UnityEngine.Debug.LogError("Need at least 3 points to solve affine.");
            return;
        }

        //compute sums for M^T M (3x3)
        double Sxx = 0, Sxy = 0, Sx1 = 0;
        double Syy = 0, Sy1 = 0;
        double S11 = N;

        double SxX = 0, SyX = 0, S1X = 0;
        double SxY = 0, SyY = 0, S1Y = 0;

        for (int i = 0; i < N; i++)
        {
            double x = raw[i].x;
            double y = raw[i].y;
            double X = target[i].x;
            double Y = target[i].y;

            Sxx += x * x;
            Sxy += x * y;
            Sx1 += x;

            Syy += y * y;
            Sy1 += y;

            SxX += x * X;
            SyX += y * X;
            S1X += X;

            SxY += x * Y;
            SyY += y * Y;
            S1Y += Y;
        }

        //matrix A = M^T M
        // [Sxx Sxy Sx1]
        // [Sxy Syy Sy1]
        // [Sx1 Sy1 S11]
        //solve A * pX = bX, A * pY = bY

        if (!Solve3x3(
            Sxx, Sxy, Sx1,
            Sxy, Syy, Sy1,
            Sx1, Sy1, S11,
            SxX, SyX, S1X,
            out double pa, out double pb, out double pc))
        {
            UnityEngine.Debug.LogError("Failed to solve affine for X");
            return;
        }

        if (!Solve3x3(
            Sxx, Sxy, Sx1,
            Sxy, Syy, Sy1,
            Sx1, Sy1, S11,
            SxY, SyY, S1Y,
            out double pd, out double pe, out double pf))
        {
            UnityEngine.Debug.LogError("Failed to solve affine for Y");
            return;
        }

        a = (float)pa; b = (float)pb; c = (float)pc;
        d = (float)pd; e = (float)pe; f = (float)pf;

        //calculate and report calibration accuracy
        float maxError = 0f;
        float avgError = 0f;
        for (int i = 0; i < N; i++)
        {
            Vector2 predicted = ApplyAffine(raw[i]);
            float error = Vector2.Distance(predicted, target[i]);
            maxError = Mathf.Max(maxError, error);
            avgError += error;
        }
        avgError /= N;

        UnityEngine.Debug.Log($"Affine solved:\nX={a:F4}x+{b:F4}y+{c:F2}\nY={d:F4}x+{e:F4}y+{f:F2}");
        UnityEngine.Debug.Log($"Calibration accuracy: Avg error={avgError:F1}px, Max error={maxError:F1}px");
        if (avgError > 200f)
        {
            UnityEngine.Debug.LogWarning($"Very high calibration error ({avgError:F1}px). Consider recalibrating.");
        }
        else if (avgError > 150f)
        {
            UnityEngine.Debug.Log($"Calibration complete with moderate accuracy ({avgError:F1}px). Should be usable.");
        }
        else
        {
            UnityEngine.Debug.Log($"Calibration complete with good accuracy ({avgError:F1}px).");
        }
    }

    //gaussian elimination for 3x3
    bool Solve3x3(
        double a00, double a01, double a02,
        double a10, double a11, double a12,
        double a20, double a21, double a22,
        double b0, double b1, double b2,
        out double x0, out double x1, out double x2)
    {
        double[,] m = new double[3, 4] {
            { a00, a01, a02, b0 },
            { a10, a11, a12, b1 },
            { a20, a21, a22, b2 }
        };

        for (int col = 0; col < 3; col++)
        {
            int pivot = col;
            double max = System.Math.Abs(m[col, col]);
            for (int r = col + 1; r < 3; r++)
            {
                double v = System.Math.Abs(m[r, col]);
                if (v > max) { max = v; pivot = r; }
            }
            if (max < 1e-9)
            {
                x0 = x1 = x2 = 0;
                return false;
            }
            if (pivot != col)
            {
                for (int k = col; k < 4; k++)
                {
                    (m[col, k], m[pivot, k]) = (m[pivot, k], m[col, k]);
                }
            }

            double div = m[col, col];
            for (int k = col; k < 4; k++) m[col, k] /= div;

            for (int r = 0; r < 3; r++)
            {
                if (r == col) continue;
                double factor = m[r, col];
                for (int k = col; k < 4; k++)
                    m[r, k] -= factor * m[col, k];
            }
        }

        x0 = m[0, 3];
        x1 = m[1, 3];
        x2 = m[2, 3];
        return true;
    }
    
    // Calculate median (more robust to outliers than mean)
    Vector2 CalculateMedian(List<Vector2> samples)
    {
        if (samples.Count == 0) return Vector2.zero;
        if (samples.Count == 1) return samples[0];
        
        var sortedX = new List<float>();
        var sortedY = new List<float>();
        foreach (var s in samples)
        {
            sortedX.Add(s.x);
            sortedY.Add(s.y);
        }
        sortedX.Sort();
        sortedY.Sort();
        
        int mid = sortedX.Count / 2;
        float medianX = sortedX.Count % 2 == 0 
            ? (sortedX[mid - 1] + sortedX[mid]) / 2f 
            : sortedX[mid];
        float medianY = sortedY.Count % 2 == 0 
            ? (sortedY[mid - 1] + sortedY[mid]) / 2f 
            : sortedY[mid];
        
        return new Vector2(medianX, medianY);
    }
    
    //filter outliers and return mean of remaining points
    Vector2 FilterOutliers(List<Vector2> samples, Vector2 center)
    {
        if (samples.Count == 0) return center;
        
        //calculate standard deviation
        float sumDistSq = 0f;
        foreach (var s in samples)
        {
            float dist = Vector2.Distance(s, center);
            sumDistSq += dist * dist;
        }
        float stdDev = Mathf.Sqrt(sumDistSq / samples.Count);
        
        float threshold = stdDev * 3f;
        Vector2 sum = Vector2.zero;
        int count = 0;
        
        foreach (var s in samples)
        {
            if (Vector2.Distance(s, center) <= threshold)
            {
                sum += s;
                count++;
            }
        }
        
        if (count < samples.Count * 0.5f)
        {
            return center;
        }
        
        return count > 0 ? sum / count : center;
    }
}
