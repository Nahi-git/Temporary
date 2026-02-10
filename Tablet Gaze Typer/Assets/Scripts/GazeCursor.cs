using UnityEngine;
using UnityEngine.UI;

public class GazeCursor : MonoBehaviour
{
    [Header("References")]
    public GazeWebSocketClient gazeSource;
    public UnityGazeCalibrator calibrator; 
    
    [Header("Visual Feedback")]
    public Image cursorImage;
    
    RectTransform rt;
    private bool wasCalibrated = false;
    private bool isVisible = true;

    void Start()
    {
        rt = GetComponent<RectTransform>();
        if (cursorImage == null)
        {
            cursorImage = GetComponent<Image>();
        }
        
        //autofind gazeSource and calibrator if not assigned
        if (gazeSource == null)
        {
            gazeSource = FindObjectOfType<GazeWebSocketClient>();
            if (gazeSource == null)
                UnityEngine.Debug.LogWarning("GazeCursor: Could not find GazeWebSocketClient!");
        }
        
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
            if (calibrator == null)
                UnityEngine.Debug.LogWarning("GazeCursor: Could not find UnityGazeCalibrator!");
        }
        
        UpdateVisibility();
    }
    
    public void SetVisibility(bool visible)
    {
        isVisible = visible;
        UpdateVisibility();
    }
    
    public bool GetVisibility()
    {
        return isVisible;
    }
    
    private void UpdateVisibility()
    {
        if (cursorImage != null)
        {
            cursorImage.enabled = isVisible;
        }
        else if (rt != null)
        {
            rt.gameObject.SetActive(isVisible);
        }
    }

    void Update()
    {
        //autofind gazeSource and calibrator if they are missing (in case scene switched)
        if (gazeSource == null)
        {
            gazeSource = FindObjectOfType<GazeWebSocketClient>();
            if (gazeSource == null)
            {
                if (Time.frameCount % 60 == 0)
                    UnityEngine.Debug.LogWarning("GazeCursor: gazeSource is null, cannot update cursor position.");
                return;
            }
        }
        
        if (calibrator == null)
        {
            calibrator = FindObjectOfType<UnityGazeCalibrator>();
        }

        // Use calibrated gaze if available, otherwise use raw gaze
        Vector2 gazePosition;
        bool usingCalibrated = false;
        
        if (calibrator != null && calibrator.calibrated)
        {
            gazePosition = calibrator.calibratedGaze;
            usingCalibrated = true;
        }
        else
        {
            gazePosition = gazeSource.rawGaze;
        }
        
        //debug logging occasionally
        if (Time.frameCount % 120 == 0)
        {
            UnityEngine.Debug.Log($"GazeCursor: gazePosition=({gazePosition.x:F1}, {gazePosition.y:F1}), calibrated={usingCalibrated}, calibrator.calibrated={(calibrator != null ? calibrator.calibrated.ToString() : "null")}");
        }

        //change color when calibrated
        if (cursorImage != null)
        {
            if (usingCalibrated && !wasCalibrated)
            {
                cursorImage.color = Color.green;
                UnityEngine.Debug.Log("GazeCursor: Now using CALIBRATED gaze (cursor is green)");
            }
            else if (!usingCalibrated && wasCalibrated)
            {
                cursorImage.color = Color.white;
                UnityEngine.Debug.Log("GazeCursor: Using RAW gaze (cursor is white)");
            }
            wasCalibrated = usingCalibrated;
        }

        if (!rt.gameObject.activeSelf)
        {
            rt.gameObject.SetActive(true);
        }
        
        float clampedX = Mathf.Clamp(gazePosition.x, 0, Screen.width);
        float clampedY = Mathf.Clamp(gazePosition.y, 0, Screen.height);
        float flippedY = Screen.height - clampedY;
        
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Camera camera = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                camera = canvas.worldCamera;
            }
            
            Vector2 localPoint;
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                new Vector2(clampedX, flippedY),
                camera,
                out localPoint);
            
            if (success)
            {
                rt.anchoredPosition = localPoint;
            }
            else if (Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.LogWarning($"GazeCursor: Failed to convert screen point to local point. gazePosition=({gazePosition.x:F1}, {gazePosition.y:F1})");
            }
        }
        else
        {
            // fallback to simple method if no canvas found
            float x = clampedX - Screen.width / 2f;
            float y = -(clampedY - Screen.height / 2f);
            rt.anchoredPosition = new Vector2(x, y);
            if (Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.LogWarning("GazeCursor: No Canvas found, using fallback positioning.");
            }
        }
    }
}
