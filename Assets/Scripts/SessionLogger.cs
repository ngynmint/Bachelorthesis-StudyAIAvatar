using UnityEngine;
using System;
using System.IO;

public class SessionLogger : MonoBehaviour
{
    private string logFilePath;
    private int turnIndex = 0;

    void Awake()
    {
        string logDir = Path.Combine(Application.persistentDataPath, "Logger");
        Directory.CreateDirectory(logDir);
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logFilePath = Path.Combine(logDir, $"Session_{timestamp}.txt");
        
        File.WriteAllText(logFilePath, $"Session Start: {DateTime.Now}\n\n");
        Debug.Log($"[Logger] Logging to: {logFilePath}");
    }

    public void LogTurn(string userText, float recordingDuration, string aiResponse)
    {
        turnIndex++;
        string entry = $"--- Turn {turnIndex} ---\n" +
                       $"User: {userText}\n" +
                       $"Explanation Length: {recordingDuration:F1} sec\n" +
                       $"AI Response: {aiResponse}\n\n";
        
        File.AppendAllText(logFilePath, entry);
        Debug.Log($"[Logger] Turn {turnIndex} logged");
    }
}