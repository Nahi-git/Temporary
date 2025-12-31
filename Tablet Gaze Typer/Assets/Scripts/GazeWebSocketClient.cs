using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Collections;

public class GazeWebSocketClient : MonoBehaviour
{
    [Header("Gaze Data")]
    public Vector2 rawGaze;
    
    [Header("Browser Window Info")]
    public Vector2 browserWindowSize = Vector2.zero; 
    
    WebSocket ws;

    void Start()
    {
        StartCoroutine(ConnectWithRetry());
    }

    IEnumerator ConnectWithRetry()
    {
        yield return new WaitForSeconds(2.5f);

        while (true)
        {
            ws = new WebSocket("ws://localhost:8765");

            ws.OnOpen += () => UnityEngine.Debug.Log("Unity connected to WebSocket");
            ws.OnError += (e) => UnityEngine.Debug.LogWarning("WebSocket error, retrying...");

            ws.OnMessage += bytes =>
            {
                var json = Encoding.UTF8.GetString(bytes);
                
                //check if this is a window size message
                if (json.Contains("\"type\"") && json.Contains("windowSize"))
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
                yield break;

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
}
