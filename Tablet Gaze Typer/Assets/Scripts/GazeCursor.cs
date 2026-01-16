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
        if (gazeSource == null) return;

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
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                new Vector2(clampedX, flippedY),
                camera,
                out localPoint);
            
            rt.anchoredPosition = localPoint;
        }
        else
        {
            // fallback to simple method if no canvas found
            float x = clampedX - Screen.width / 2f;
            float y = -(clampedY - Screen.height / 2f);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
