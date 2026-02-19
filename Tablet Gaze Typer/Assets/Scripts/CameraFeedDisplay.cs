using UnityEngine;
using UnityEngine.UI;

public class CameraFeedDisplay : MonoBehaviour
{
    public enum FaceGuideShape { Oval, Square }

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

    [Header("Face guide (center overlay)")]
    [Tooltip("Show a guide in the center so users can keep their face in frame. Oval matches face shape better.")]
    public bool showFaceGuide = true;
    public FaceGuideShape faceGuideShape = FaceGuideShape.Oval;
    [Tooltip("Size of the guide as a fraction of the feed (e.g. 0.5 = half width/height).")]
    [Range(0.2f, 0.95f)]
    public float faceGuideSizeFraction = 0.5f;
    [Tooltip("Color of the guide outline.")]
    public Color faceGuideColor = Color.white;
    [Tooltip("Outline thickness in pixels on the guide texture.")]
    [Range(1, 6)]
    public int faceGuideLineWidth = 2;
    
    private RectTransform rectTransform;
    private GameObject faceGuideObject;
    
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
            if (showFaceGuide)
                CreateFaceGuide();
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

    void CreateFaceGuide()
    {
        if (rectTransform == null || faceGuideObject != null) return;

        faceGuideObject = new GameObject("FaceGuide");
        faceGuideObject.transform.SetParent(rectTransform, false);

        RectTransform guideRect = faceGuideObject.AddComponent<RectTransform>();
        guideRect.anchorMin = new Vector2(0.5f, 0.5f);
        guideRect.anchorMax = new Vector2(0.5f, 0.5f);
        guideRect.pivot = new Vector2(0.5f, 0.5f);
        guideRect.anchoredPosition = Vector2.zero;
        float w = feedSize.x * faceGuideSizeFraction;
        float h = feedSize.y * faceGuideSizeFraction;
        guideRect.sizeDelta = new Vector2(w, h);

        Image img = faceGuideObject.AddComponent<Image>();
        img.sprite = CreateGuideSprite();
        img.color = faceGuideColor;
        img.raycastTarget = false;
        guideRect.localEulerAngles = new Vector3(0f, 0f, 90f);
    }

    Sprite CreateGuideSprite()
    {
        int texSize = 128;
        Texture2D tex = new Texture2D(texSize, texSize);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0, 0, 0, 0);
        float cx = (texSize - 1) * 0.5f;
        float cy = (texSize - 1) * 0.5f;
        float rx = (texSize - 1) * 0.5f;
        float ry = (texSize - 1) * 0.5f;
        float lw = Mathf.Max(1, faceGuideLineWidth);

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                bool onBorder = false;
                if (faceGuideShape == FaceGuideShape.Oval)
                {
                    float nx = (rx > 0) ? (x - cx) / rx : 0;
                    float ny = (ry > 0) ? (y - cy) / ry : 0;
                    float ellipseR = Mathf.Sqrt(nx * nx + ny * ny);
                    float tol = (lw + 0.5f) / Mathf.Min(rx, ry);
                    if (Mathf.Abs(ellipseR - 1f) <= tol)
                        onBorder = true;
                }
                else
                {
                    float px = Mathf.Abs(x - cx);
                    float py = Mathf.Abs(y - cy);
                    bool inOuter = px <= rx && py <= ry;
                    bool inInner = px <= rx - lw && py <= ry - lw;
                    onBorder = inOuter && !inInner;
                }
                tex.SetPixel(x, y, onBorder ? Color.white : clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
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
