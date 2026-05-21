using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class StudyingTimeManager : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public GameObject TimeOverPopup;
    public PipelineManager pipelineManager;
    public float countdownDuration;
    public float popupDuration;
    public float pauseAfterPopup;

    void Start()
    {
        if (TimeOverPopup != null)
            TimeOverPopup.SetActive(false);

        StartCoroutine(RunStudyingSequence());
    }

    private IEnumerator RunStudyingSequence()
    {
        float remaining = countdownDuration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            remaining = Mathf.Max(remaining, 0f);

            if (timerText != null)
                timerText.text = FormatTime(remaining);

            yield return null;
        }

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (TimeOverPopup != null)
            TimeOverPopup.SetActive(true);

        yield return new WaitForSeconds(popupDuration);

        if (TimeOverPopup != null)
            TimeOverPopup.SetActive(false);

        yield return new WaitForSeconds(pauseAfterPopup);

        if (pipelineManager != null)
            pipelineManager.SendPromptFile();
        else
            Debug.LogError("[StudyingTimeManager] PipelineManager reference is missing!");
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}