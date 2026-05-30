using UnityEngine;
using UnityPdfViewer;

public class LearningMaterialController : MonoBehaviour
{
    public GameObject learningMaterialWindow;
    public PdfViewerUI pdfViewer;
    public float reachThreshold = 0.3f;
    public SessionLogger sessionLogger;
    private Vector3 handStartPosition;
    private bool handInZone = false;
    private bool windowOpen = false;
    private bool gestureUsed = false; 
    private bool interactionLocked = false;


    private void Start()
    {
        if (learningMaterialWindow != null)
            learningMaterialWindow.SetActive(true);
        windowOpen = true;
        if (pdfViewer != null)
            pdfViewer.LoadPDF();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (interactionLocked) return;
        Debug.Log("[LearningMaterial] OnTriggerEnter hit by: " + other.gameObject.name + " | Tag: " + other.tag);
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand"))
        {
            Debug.Log("[LearningMaterial] Ignored — wrong tag");
            return;
        }

        handInZone = true;
        gestureUsed = false;
        handStartPosition = other.transform.position;
        Debug.Log("[LearningMaterial    ] Hand entered zone");
    }

    private void OnTriggerStay(Collider other)
    {
        if (interactionLocked) return;
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
        {
            pdfViewer.LoadPDF();
            sessionLogger.LogMaterialOpened();
            Debug.Log("[LearningMaterial] Window opened, PDF loading...");
        }
        else{
            sessionLogger.LogMaterialClosed();
            Debug.Log("[LearningMaterial] Window closed");
        }
        Debug.Log("[LearningMaterial] Window " + (windowOpen ? "opened" : "closed"));
    }

    public void LockInteraction(bool locked)
    {
        interactionLocked = locked;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = handInZone ? Color.green : Color.yellow;
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}