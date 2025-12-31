using UnityEngine;
using System.Diagnostics;
using System.IO;

public class AppBootstrap : MonoBehaviour
{
    void Start()
    {
        StartNodeServer();
        StartHttpServer();
        Invoke(nameof(StartChrome), 3f); // wait for servers
    }

    void StartNodeServer()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backend = Path.Combine(projectRoot, "gaze-backend");
        string nodeExe = Path.Combine(backend, "node.exe");
        string serverJs = Path.Combine(backend, "server.js");

        if (!File.Exists(nodeExe))
        {
            UnityEngine.Debug.LogError("Bundled node.exe not found!");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = nodeExe,
            Arguments = $"\"{serverJs}\"",
            WorkingDirectory = backend,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        UnityEngine.Debug.Log("Bundled Node server started");
    }


    void StartHttpServer()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backend = Path.Combine(projectRoot, "gaze-backend");
        string nodeExe = Path.Combine(backend, "node.exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = nodeExe,
            Arguments = "node_modules/http-server/bin/http-server . -p 8081",
            WorkingDirectory = backend,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        UnityEngine.Debug.Log("Bundled HTTP server started");
    }


    void StartChrome()
    {
        string chrome1 = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        string chrome2 = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        string chrome = File.Exists(chrome1) ? chrome1 : chrome2;

        if (!File.Exists(chrome))
        {
            UnityEngine.Debug.LogError("Chrome not found! Please install Chrome or update the path.");
            return;
        }

        // Get screen resolution for fullscreen
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;

        Process.Start(new ProcessStartInfo
        {
            FileName = chrome,
            Arguments =
                $"--app=http://localhost:8081/gaze.html " +
                $"--window-size={screenWidth},{screenHeight} " +
                "--start-maximized " +
                "--use-fake-ui-for-media-stream " +
                "--disable-background-timer-throttling " +
                "--disable-backgrounding-occluded-windows",
            UseShellExecute = true
        });

        UnityEngine.Debug.Log($"Chrome launched in fullscreen mode ({screenWidth}x{screenHeight})");
    }
}

