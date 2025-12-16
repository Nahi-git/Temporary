using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Collections;

public class GazeWebSocketClient : MonoBehaviour
{
    public Vector2 gaze;
    WebSocket ws;

    void Start()
    {
        StartCoroutine(ConnectWithRetry());
    }

    IEnumerator ConnectWithRetry()
    {
        // Wait for Node to start
        yield return new WaitForSeconds(3f);

        while (true)
        {
            ws = new WebSocket("ws://localhost:8765");

            ws.OnOpen += () =>
            {
                Debug.Log("Unity connected to WebSocket");
            };

            ws.OnError += e =>
            {
                Debug.LogWarning("WebSocket error, retrying...");
            };

            ws.OnMessage += bytes =>
            {
                string json = Encoding.UTF8.GetString(bytes);
                var data = JsonUtility.FromJson<GazeData>(json);
                gaze = new Vector2(data.x, data.y);
            };

            var connectTask = ws.Connect();

            // Wait up to 2 seconds
            float timeout = 2f;
            while (!connectTask.IsCompleted && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (ws.State == WebSocketState.Open)
            {
                yield break; // connected successfully
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
        if (ws != null)
            await ws.Close();
    }

    [System.Serializable]
    class GazeData
    {
        public float x;
        public float y;
    }
}

