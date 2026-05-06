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
    private bool lastGazeState = false;
    private bool materialOpen = false;
    private float materialOpenTime = 0f;
    public float GetSessionStartTime()
    {
        return sessionStartTime;
    }
    public GameObject gazeDebugDot;


    void Awake()
    {
        sessionStartTime = Time.time;
        string logDir = Path.Combine(Application.dataPath, "Logger");
        Directory.CreateDirectory(logDir);

        string fileName = $"Session_{participantID}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        logFilePath = Path.Combine(logDir, fileName);

        File.WriteAllText(logFilePath, "time_s,event_type,value\n");
        LogEvent("SESSION_START", participantID);
        LogEvent("VARIABLE", variableTested);
        Debug.Log("[Logger] CSV Log @: " + logFilePath);
    }

    private string FormatFloat(float value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }
    private float GetTimeNow()
    {
        return Time.time - sessionStartTime;
    }
    private void LogEvent(string eventType, string value = "")
    {
        string line = FormatFloat(GetTimeNow()) + "," + eventType + "," + value + "\n";
        File.AppendAllText(logFilePath, line);
    }
    private bool IsGazing()
    {
        if (avatarTransform == null)
        {
            return false;
        }

        Transform cam = Camera.main.transform;

        Ray ray = new Ray(cam.position, cam.forward);
        RaycastHit hit;

        bool didHit = Physics.Raycast(ray, out hit, 100f);

        if (Physics.Raycast(ray, out hit, 100f))
        {
            Transform t = hit.transform;

            if (t == avatarTransform)
            {
                return true;
            }

            while (t.parent != null)
            {
                t = t.parent;

                if (t == avatarTransform)
                {
                    return true;
                }
            }
        }

        return false;
    }

    void Update()
    {
        bool gazing = IsGazing();

        if (gazeDebugDot != null)
            {
                gazeDebugDot.SetActive(gazing);
            }
        if (gazing != lastGazeState)
        {
            if (gazing)
            {
                LogEvent("GAZE_ON", "");
            }
            else
            {
                LogEvent("GAZE_OFF", "");
            }

            lastGazeState = gazing;
        }
    }

    public void LogUserTurn(string userText, float recordingDuration, float userStartMs)
    {
        float userStartS = userStartMs / 1000f;
        string speechLine = FormatFloat(userStartS) + ",USER_SPEECH,\"" + userText + "\"\n";
        File.AppendAllText(logFilePath, speechLine);
        string durationLine = FormatFloat(userStartS) + ",EXPLANATION_DURATION," + FormatFloat(recordingDuration) + "\n";
        File.AppendAllText(logFilePath, durationLine);
    }

    public void LogAITurn(string aiText, float aiDuration, float aiStartMs)
    {
        float aiStartS = aiStartMs / 1000f;
        string responseLine = FormatFloat(aiStartS) + ",AI_RESPONSE,\"" + aiText + "\"\n";
        File.AppendAllText(logFilePath, responseLine);
        string durationLine = FormatFloat(aiStartS) + ",AI_DURATION," + FormatFloat(aiDuration) + "\n";
        File.AppendAllText(logFilePath, durationLine);
    }

    public void LogMaterialOpened()
    {
        materialOpen = true;
        materialOpenTime = GetTimeNow();
        LogEvent("MATERIAL_OPEN", "");
    }
    public void LogMaterialClosed()
    {
        if (!materialOpen) return;
        materialOpen = false;
        float duration = GetTimeNow() - materialOpenTime;
        LogEvent("MATERIAL_CLOSE", FormatFloat(duration));
    }
}