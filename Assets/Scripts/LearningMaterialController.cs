using UnityEngine;

public class LearningMaterialController : MonoBehaviour
{
    [Header("Notepad")]
    public GameObject learningMaterialWindow;

    [Header("settings")]
    public float reachThreshold = 0.3f;
    public SessionLogger sessionLogger;
    private Vector3 handStartPosition;
    private bool handInZone = false;
    private bool windowOpen = false;
    private bool gestureUsed = false; 

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand")) return;

        handInZone = true;
        gestureUsed = false;
        handStartPosition = other.transform.position;
        Debug.Log("[LearningMaterial    ] Hand entered zone");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!handInZone || gestureUsed) return;
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand")) return;

        float distance = Vector3.Distance(handStartPosition, other.transform.position);

        if (distance >= reachThreshold)
        {
            gestureUsed = true;
            ToggleWindow();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand")) return;
        handInZone = false;
        Debug.Log("[LearningMaterial] Hand left zone");
    }

    private void ToggleWindow()
    {
        windowOpen = !windowOpen;
        learningMaterialWindow.SetActive(windowOpen);
        if (windowOpen)
            sessionLogger.LogMaterialOpened();
        else
            sessionLogger.LogMaterialClosed();
        Debug.Log("[LearningMaterial] Window " + (windowOpen ? "opened" : "closed"));
    }
}