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
    private int turnIndex = 0;
    private float sessionStartTime;
    private float gazeStartTime = -1f;
    private float gazeDuration = 0f;
    private float totalExplanationTime = 0f;
    private float totalGazeExplanation = 0f;
    private bool wasGazing = false;
    public float GetSessionStartTime() => sessionStartTime;
    public GameObject gazeDebugDot;


    void Awake()
    {
        sessionStartTime = Time.time;
        string logDir = Path.Combine(Application.dataPath, "Logger");
        Directory.CreateDirectory(logDir);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        logFilePath = Path.Combine(logDir, $"Session_{participantID}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        File.WriteAllText(logFilePath,
            $"[0.00s] Session Start: {timestamp}\n" +
            $"Participant ID: {participantID}\n" +
            $"Variable Tested: {variableTested}\n\n");

        Debug.Log($"[Logger] Logging to: {logFilePath}");
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
    public void StartGazeTracking()
    {
        gazeDuration = 0f;
        wasGazing = false;
        gazeStartTime = -1f;
    }

    public float StopGazeTracking()
    {
        if (wasGazing && gazeStartTime >= 0f)
            gazeDuration += Time.time - gazeStartTime;
        return gazeDuration;
    }

    void Update()
    {
        bool gazing = IsGazing();

        if (gazeDebugDot != null)
            {
                gazeDebugDot.SetActive(gazing);
            }
        if (gazing)
        {
            if (!wasGazing)
            {
                gazeStartTime = Time.time;
            }
        }
        else
        {
            if (wasGazing && gazeStartTime >= 0f)
            {
                gazeDuration += Time.time - gazeStartTime;
                gazeStartTime = -1f;
            }
        }

        wasGazing = gazing;
    }

    public void LogTurn(string userText, float recordingDuration, float gazeExplanation, string aiResponse, float aiDuration, float gazeAI, float userStartMs, float aiStartMs)
    {
        turnIndex++;
        totalExplanationTime += recordingDuration;
        totalGazeExplanation += gazeExplanation;

        float gazePctExplanation = recordingDuration > 0 ? (gazeExplanation / recordingDuration) * 100f : 0f;
        float gazePctAI = aiDuration > 0 ? (gazeAI / aiDuration) * 100f : 0f;
        float userStartS = userStartMs / 1000f;
        float aiStartS = aiStartMs / 1000f;

        string entry =
            $"Interaction {turnIndex}:\n" +
            $"[{userStartS:F2}s] User: {userText}\n" +
            $"[{aiStartS:F2}s] AI Response: {aiResponse}\n" +
            $"Gaze at Avatar (Explanation): {gazeExplanation * 1000:F0} ms, {gazePctExplanation:F0}% of {recordingDuration:F1}s\n" +
            $"Gaze at Avatar (AI Response): {gazeAI * 1000:F0} ms, {gazePctAI:F0}% of {aiDuration:F1}s\n\n";

        File.AppendAllText(logFilePath, entry);
        Debug.Log($"[Logger] Interaction {turnIndex} logged");
    }

    void OnApplicationQuit()
    {
        float totalPct = totalExplanationTime > 0
            ? totalGazeExplanation / totalExplanationTime * 100f : 0f;

        string summary =
            "=== SESSION SUMMARY ===\n" +
            $"Total Gaze at Avatar (Explanation): {totalGazeExplanation * 1000f:F0} ms\n" +
            $"Total Explanation Time: {totalExplanationTime:F2} s\n" +
            $"Overall Gaze Percentage: {totalPct:F1}%\n";

        File.AppendAllText(logFilePath, summary);
    }
}