using UnityEngine;
using System;
using System.IO;
using System.Globalization; 

public class SessionLogger : MonoBehaviour
{
    [Header("Settings")]
    public string participantID = " ";
    public string variableTested= " ";
    public Transform avatarTransform;

    private string logFilePath;
    private float sessionStartTime;
    private double sessionStartUnix;
    private bool lastGazeState = false;
    private bool materialOpen = false;

    void Awake()
    {
        sessionStartTime = Time.time;
        sessionStartUnix = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        string logDir = Path.Combine(Application.dataPath, "Logger");
        Directory.CreateDirectory(logDir);

        string fileName = $"Session_{participantID}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        logFilePath = Path.Combine(logDir, fileName);

        File.WriteAllText(logFilePath, "unix_time,time_ms,variable,value\n");
        LogEvent("PARTICIPANT_ID", participantID);
        LogEvent("VARIABLE_TESTED", variableTested);
        Debug.Log("[Logger] CSV Log @: " + logFilePath);
    }

    //FORMAT THINGS
    private string FormatFloat(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private float GetTimeNow() => Time.time - sessionStartTime;
    
    private string FormatDouble(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private string EscapeCsv(string value) // ensure no breaking csv?
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    //LOGGING
    private void LogEvent(string variable, string value = "")
    {
        double unixTime = sessionStartUnix + GetTimeNow();
        float timeMs = GetTimeNow() * 1000f;
        string line = $"{FormatDouble(unixTime)},{FormatFloat(timeMs)},{variable},{EscapeCsv(value)}\n";
        File.AppendAllText(logFilePath, line);
    }

    private void LogEventAtTime(float eventTime, string variable, string value = "")
    {
        double unixTime = sessionStartUnix + eventTime;
        float timeMs = eventTime * 1000f;
        string line = $"{FormatDouble(unixTime)},{FormatFloat(timeMs)},{variable},{EscapeCsv(value)}\n";
        File.AppendAllText(logFilePath, line);
    }

    void Update()
    {
        bool gazing = IsGazing();

        if (gazing != lastGazeState)
        {
            LogEvent("GAZE", gazing ? "ON" : "OFF");
            lastGazeState = gazing;
        }
    }

    public void LogStudyStart()
    {
        LogEvent("STUDY_PHASE", "START");
    }

    public void LogStudyEnd()
    {
        LogEvent("STUDY_PHASE", "END");
    }

    public void LogInteractionStart()
    {
        LogEvent("INTERACTION_PHASE", "START");
    }

    public void LogInteractionEnd()
    {
        LogEvent("INTERACTION_PHASE", "END");
    }

    // USER EVENTS
    public void LogUserSpeechStart()
    {
        LogEvent("USER_SPEECH", "START");
    }

    public void LogUserSpeechEnd()
    {
        LogEvent("USER_SPEECH", "END");
    }

    public void LogUserTurn(string userText)
    {
        LogEvent("USER_SPEECH", userText);
    }

    // AI EVENTS
    public void LogLLMResponseReceived()
    {
        LogEvent("LLM_RESPONSE_RECEIVED");
    }

    public void LogAISpeechStart()
    {
        LogEvent("AI_SPEECH", "START");
    }

    public void LogAISpeechEnd()
    {
        LogEvent("AI_SPEECH", "END");
    }

    public void LogAITurn(string aiText)
    {
        LogEvent("AI_SPEECH", aiText);
    }

    //GAZE
    private bool IsGazing()
    {
        if (avatarTransform == null) return false;
        if (IsOutOfView()) return false;
        return true;
    }

    private bool IsOutOfView()
    {
        if (avatarTransform == null) return true;
        Renderer[] renderers = avatarTransform.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return true;
        foreach (var r in renderers)
        {
            if (r.isVisible)
                return false;
        }
        return true;
    }

    //MATERIAL 
    public void LogMaterialOpened()
    {
        materialOpen = true;
        LogEvent("MATERIAL", "OPEN");
    }
    public void LogMaterialClosed()
    {
        if (!materialOpen) return;
        materialOpen = false;
        LogEvent("MATERIAL", "CLOSE");
    }
}