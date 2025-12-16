using UnityEngine;

public class GazeCursor : MonoBehaviour
{
    public GazeWebSocketClient gazeSource;
    RectTransform rt;

    void Start()
    {
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (gazeSource == null) return;

        // Convert browser screen coords to Unity canvas coords
        float x = gazeSource.gaze.x - Screen.width / 2f;
        float y = -(gazeSource.gaze.y - Screen.height / 2f);

        rt.anchoredPosition = new Vector2(x, y);
    }
}
