using UnityEngine;
using System.Collections.Generic;

public class MobileDebugConsole : MonoBehaviour
{
    private Queue<string> logs = new Queue<string>();
    private const int MaxLogs = 20;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logs.Enqueue($"[{type}] {logString}");
        if (logs.Count > MaxLogs)
        {
            logs.Dequeue();
        }
    }

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 3.0f); // Scale up for high-DPI
        GUILayout.BeginArea(new Rect(10, 10, Screen.width / 3, Screen.height / 3));
        foreach (var log in logs)
        {
            GUILayout.Label(log);
        }
        GUILayout.EndArea();
    }
}
