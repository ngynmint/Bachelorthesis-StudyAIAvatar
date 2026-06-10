using UnityEngine;
using UnityEngine.XR;
using UnityPdfViewer;

public class PDFJoystickNav : MonoBehaviour
{
    public PdfViewerUI pdfViewer;
    public LearningMaterialController learningMaterial; // assign in Inspector, remove pdfViewer ref
    private bool axisCooledDown = true;

    void Update()
    {
        if (pdfViewer == null) return;

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftAxis);
        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightAxis);

        float combinedX = Mathf.Abs(leftAxis.x) > Mathf.Abs(rightAxis.x) ? leftAxis.x : rightAxis.x;

        if (Mathf.Abs(combinedX) < 0.5f)
        {
            axisCooledDown = true;
            return;
        }

        if (!axisCooledDown) return;

        if (combinedX < -0.5f)
            learningMaterial.NextPage();
        else if (combinedX > 0.5f)
            learningMaterial.PreviousPage();

        axisCooledDown = false;
    }
}