using UnityEngine;
using System.Diagnostics;
using System.IO;

public class AppBootstrap : MonoBehaviour
{
    private static AppBootstrap _instance;
    private static bool _hasStarted = false;

    void Awake()
    {
        //only one instance of AppBootstrap exists
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            UnityEngine.Debug.LogWarning("AppBootstrap: Another instance already exists. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        //start servers and Chrome once, even if scene reloads
        if (!_hasStarted)
        {
            _hasStarted = true;
            StartNodeServer();
            StartHttpServer();
            Invoke(nameof(StartChrome), 3f); // wait for servers
        }
        else
        {
            UnityEngine.Debug.Log("AppBootstrap: Servers and Chrome already started, skipping initialization.");
        }
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
                "--disable-background-timer-throttling " +
                "--disable-backgrounding-occluded-windows",
            UseShellExecute = true
        });

        UnityEngine.Debug.Log($"Chrome launched in fullscreen mode ({screenWidth}x{screenHeight})");
    }
}

