using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Collections;

public class GazeWebSocketClient : MonoBehaviour
{
    private static GazeWebSocketClient _instance;

    [Header("Gaze Data")]
    public Vector2 rawGaze;
    
    [Header("Browser Window Info")]
    public Vector2 browserWindowSize = Vector2.zero;
    
    [Header("Camera Feed")]
    public Texture2D cameraTexture;
    public System.Action<Texture2D> OnCameraFrameReceived; 
    
    WebSocket ws;
    private bool isConnecting = false;
    public WebSocketState ConnectionState => ws != null ? ws.State : WebSocketState.Closed;
    void Awake()
    {
        //only one instance of GazeWebSocketClient exists
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            //another instance of GazeWebSocketClient already exists, destroy this one
            UnityEngine.Debug.LogWarning("GazeWebSocketClient: Another instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        //only connect if not already connected or connecting
        if (ws == null && !isConnecting)
        {
            StartCoroutine(ConnectWithRetry());
        }
        else if (ws != null && ws.State == WebSocketState.Open)
        {
            UnityEngine.Debug.Log("GazeWebSocketClient: Already connected, maintaining connection.");
        }
    }

    IEnumerator ConnectWithRetry()
    {
        if (isConnecting)
            yield break;
            
        isConnecting = true;
        yield return new WaitForSeconds(2.5f);

        while (true)
        {
            //if already connected, stop trying
            if (ws != null && ws.State == WebSocketState.Open)
            {
                isConnecting = false;
                yield break;
            }
            ws = new WebSocket("ws://localhost:8765");

            ws.OnOpen += () => UnityEngine.Debug.Log("Unity connected to WebSocket");
            ws.OnError += (e) => UnityEngine.Debug.LogWarning("WebSocket error, retrying...");

            ws.OnMessage += bytes =>
            {
                var json = Encoding.UTF8.GetString(bytes);
                
                //check if this is a camera frame message
                if (json.Contains("\"type\"") && json.Contains("cameraFrame"))
                {
                    try
                    {
                        var frameData = JsonUtility.FromJson<CameraFrameData>(json);
                        if (!string.IsNullOrEmpty(frameData.data))
                        {
                            //decode base64 image data
                            string base64Data = frameData.data;
                            if (base64Data.StartsWith("data:image"))
                            {
                                //remove data URL prefix
                                int commaIndex = base64Data.IndexOf(',');
                                if (commaIndex >= 0)
                                {
                                    base64Data = base64Data.Substring(commaIndex + 1);
                                }
                            }
                            
                            byte[] imageBytes = System.Convert.FromBase64String(base64Data);
                            if (cameraTexture == null)
                            {
                                cameraTexture = new Texture2D(2, 2);
                            }
                            if (cameraTexture.LoadImage(imageBytes))
                            {
                                OnCameraFrameReceived?.Invoke(cameraTexture);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to parse camera frame: {ex.Message}");
                    }
                }
                //check if this is a window size message
                else if (json.Contains("\"type\"") && json.Contains("windowSize"))
                {
                    try
                    {
                        var sizeData = JsonUtility.FromJson<WindowSizeData>(json);
                        if (sizeData.width > 0 && sizeData.height > 0)
                        {
                            browserWindowSize = new Vector2(sizeData.width, sizeData.height);
                            UnityEngine.Debug.Log($"Browser window size received: {browserWindowSize.x}x{browserWindowSize.y}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to parse window size: {ex.Message}");
                    }
                }
                else
                {
                    //regular gaze data - try to parse as GazeData
                    try
                    {
                        var data = JsonUtility.FromJson<GazeData>(json);
                        if (data.x != 0 || data.y != 0 || json.Contains("\"x\"")) // Valid gaze data
                        {
                            rawGaze = new Vector2(data.x, data.y);
                            //debug logging occasionally
                            if (Time.frameCount % 60 == 0) 
                            {
                                UnityEngine.Debug.Log($"GazeWebSocketClient: Received gaze data: ({rawGaze.x:F1}, {rawGaze.y:F1})");
                            }
                        }
                    }
                    catch
                    {
                        //ignore parse errors for gaze data
                    }
                }
            };

            var task = ws.Connect();

            float timeout = 2f;
            while (!task.IsCompleted && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (ws.State == WebSocketState.Open)
            {
                isConnecting = false;
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    async void OnApplicationQuit()
    {
        if (ws != null) await ws.Close();
    }

    [System.Serializable]
    class GazeData { public float x; public float y; }
    
    [System.Serializable]
    class WindowSizeData { 
        public string type; 
        public float width; 
        public float height; 
    }
    
    [System.Serializable]
    class CameraFrameData {
        public string type;
        public string data;
    }
}
