using UnityEngine;
using System;
using System.IO;

public class SessionLogger : MonoBehaviour
{
    [Header("Settings")]
    public string participantID = " ";
    public string variableTested= " ";
    public Transform avatarTransform;
    private string logFilePath;
    private float sessionStartTime;
    private bool lastGazeState = false;
    public float GetSessionStartTime() => sessionStartTime;
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

    private float TimeNow() => Time.time - sessionStartTime;
    private void LogEvent(string eventType, string value = "")
    {
        string line = $"{TimeNow():F2},{eventType},{value}\n";
        File.AppendAllText(logFilePath, line);
    }
    private bool IsGazing()
    {
        if (avatarTransform == null)
        {
            return false;
        }

        Transform cam = Camera.main.transform;

        Vector3 origin = cam.position;
        Vector3 direction = cam.forward;

        Ray ray = new Ray(origin, direction);
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
            LogEvent(gazing ? "GAZE_ON" : "GAZE_OFF");
            lastGazeState = gazing;
        }
    }

    public void LogUserTurn(string userText, float recordingDuration, float userStartMs)
    {
        float userStartS = userStartMs / 1000f;
        File.AppendAllText(logFilePath, 
            $"{userStartS:F2},USER_SPEECH,\"{userText}\"\n");
        File.AppendAllText(logFilePath,
            $"{userStartS:F2},EXPLANATION_DURATION,{recordingDuration:F2}\n");
    }

    public void LogAITurn(string aiText, float aiDuration, float aiStartMs)
    {
        float aiStartS = aiStartMs / 1000f;
        File.AppendAllText(logFilePath,
            $"{aiStartS:F2},AI_RESPONSE,\"{aiText}\"\n");
        File.AppendAllText(logFilePath,
            $"{aiStartS:F2},AI_DURATION,{aiDuration:F2}\n");
    }
}