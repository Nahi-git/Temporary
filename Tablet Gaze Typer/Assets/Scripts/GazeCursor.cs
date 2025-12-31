using UnityEngine;
using UnityEngine.UI;

public class GazeCursor : MonoBehaviour
{
    [Header("References")]
    public GazeWebSocketClient gazeSource;
    public UnityGazeCalibrator calibrator; 
    
    [Header("Visual Feedback")]
    public Image cursorImage; //change color when calibrated
    
    RectTransform rt;
    private bool wasCalibrated = false;

    void Start()
    {
        rt = GetComponent<RectTransform>();
        if (cursorImage == null)
        {
            cursorImage = GetComponent<Image>();
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

        //clamp gaze position to screen bounds
        float clampedX = Mathf.Clamp(gazePosition.x, 0, Screen.width);
        float clampedY = Mathf.Clamp(gazePosition.y, 0, Screen.height);
        
        //convert browser screen coords to Unity canvas coords
        float x = clampedX - Screen.width / 2f;
        float y = -(clampedY - Screen.height / 2f);

        rt.anchoredPosition = new Vector2(x, y);
    }
}
