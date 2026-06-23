using UnityEngine;
using UnityPdfViewer;
public class LearningMaterialController : MonoBehaviour
{
    public GameObject learningMaterialWindow;
    public PdfViewerUI pdfViewer;
    public float reachThreshold = 0.15f;
    public SessionLogger sessionLogger;
    private Vector3 handStartPosition;
    private bool handInZone = false;
    private bool windowOpen = false;
    public bool IsOpen => windowOpen;
    private bool gestureUsed = false; 
    private bool interactionLocked = false;
    public GameObject lockedPopup;
    public PipelineManager pipelineManager;

    private void Start()
    {
        windowOpen = true;
        if (pdfViewer != null)
        {
            pdfViewer.LoadPDF();
        }   
    }

    void Update()
    {
        if (!windowOpen || interactionLocked)
        {
            Input.ResetInputAxes();
            return;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
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
        if (!handInZone || gestureUsed) return;
        if (!other.CompareTag("LeftHand") && !other.CompareTag("RightHand")) return;

        float distance = Vector3.Distance(handStartPosition, other.transform.position);

        if (distance >= reachThreshold)
        {
            gestureUsed = true;
            if (interactionLocked)
            {
                if (pipelineManager != null && pipelineManager.IsInteractionStage)
                    pipelineManager.ShowLockedPopup(lockedPopup);
            }
            else
            {
                ToggleWindow();
            }
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
    }
    private int GetCurrentPage()
    {
        var nav = typeof(PdfViewerUI)
            .GetField("navigator", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(pdfViewer);
        return (int)nav.GetType().GetProperty("CurrentPage").GetValue(nav);
    }
    public void NextPage()
    {
        if (pdfViewer == null) return;
        pdfViewer.NextPage();
        sessionLogger.LogMaterialPage(GetCurrentPage());
    }

    public void PreviousPage()
    {
        if (pdfViewer == null) return;
        pdfViewer.PreviousPage();
        sessionLogger.LogMaterialPage(GetCurrentPage());
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
    public void ForceClose()
    {
        if (!windowOpen) return;
        windowOpen = false;
        learningMaterialWindow.SetActive(false);
        sessionLogger.LogMaterialClosed();
        Debug.Log("[LearningMaterial] force closed");
    }
}