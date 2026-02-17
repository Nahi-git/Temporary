using UnityEngine;
using UnityEngine.UI;

public class CameraFeedDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RawImage component to display the camera feed. If not assigned, will try to find it automatically.")]
    public RawImage cameraFeedImage;
    
    [Tooltip("GazeWebSocketClient to receive camera frames from. If not assigned, will try to find it automatically.")]
    public GazeWebSocketClient gazeClient;
    
    [Header("Display Settings")]
    [Tooltip("Size of the camera feed display in pixels")]
    public Vector2 feedSize = new Vector2(320, 240);
    
    [Tooltip("Position offset from top left corner")]
    public Vector2 offsetFromCorner = new Vector2(20, 20);
    
    private RectTransform rectTransform;
    
    void Start()
    {
        if (cameraFeedImage == null)
        {
            cameraFeedImage = GetComponent<RawImage>();
            if (cameraFeedImage == null)
            {
                UnityEngine.Debug.LogWarning("CameraFeedDisplay: No RawImage component found. Creating one...");
                CreateCameraFeedUI();
            }
        }
        
        if (gazeClient == null)
        {
            gazeClient = FindObjectOfType<GazeWebSocketClient>();
            if (gazeClient == null)
            {
                UnityEngine.Debug.LogWarning("CameraFeedDisplay: Could not find GazeWebSocketClient!");
            }
        }
        
        rectTransform = cameraFeedImage.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            SetupRectTransform();
        }
        
        //subscribe to camera frame updates
        if (gazeClient != null)
        {
            gazeClient.OnCameraFrameReceived += OnCameraFrameReceived;
        }
        if (cameraFeedImage != null)
        {
            cameraFeedImage.enabled = true;
        }
    }
    
    void CreateCameraFeedUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            UnityEngine.Debug.LogError("CameraFeedDisplay: No Canvas found in scene!");
            return;
        }

        GameObject feedObj = new GameObject("CameraFeedDisplay");
        feedObj.transform.SetParent(canvas.transform, false);
        cameraFeedImage = feedObj.AddComponent<RawImage>();
        rectTransform = feedObj.GetComponent<RectTransform>();
        SetupRectTransform();
    }
    
    void SetupRectTransform()
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.sizeDelta = feedSize;
        rectTransform.anchoredPosition = new Vector2(offsetFromCorner.x, -offsetFromCorner.y);
        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            rectTransform.SetAsLastSibling();
        }
    }
    
    void OnCameraFrameReceived(Texture2D texture)
    {
        if (cameraFeedImage != null && texture != null)
        {
            cameraFeedImage.texture = texture;
        }
    }
    
    void Update()
    {
        if (gazeClient == null)
        {
            gazeClient = FindObjectOfType<GazeWebSocketClient>();
            if (gazeClient != null)
            {
                gazeClient.OnCameraFrameReceived += OnCameraFrameReceived;
            }
        }
        if (cameraFeedImage != null)
        {
            cameraFeedImage.enabled = true;
        }
    }
    
    void OnDestroy()
    {
        if (gazeClient != null)
        {
            gazeClient.OnCameraFrameReceived -= OnCameraFrameReceived;
        }
    }
}
