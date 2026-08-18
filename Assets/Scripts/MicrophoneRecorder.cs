using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Collections;

public class MicrophoneRecorder : MonoBehaviour
{
    public System.Action<AudioClip> OnAudioReady; 
    public LearningMaterialController learningMaterialController;
    private AudioClip recordedClip;
    private bool isRecording = false;
    private int sampleRate = 16000;
    private int maxRecordingSeconds = 300;
    public System.Action OnRecordingStarted;
    public System.Action OnRecordingStopped;
    public SessionLogger sessionLogger;
    public GameObject pdfIsStillOpenPopup;
    private string activeMic = "Headset Microphone (Oculus Virtual Audio Device)";  //Headset Microphone (Oculus Virtual Audio Device)
    public bool isLocked = false;     
    public PipelineManager pipelineManager;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            //activeMic = Microphone.devices[0];
            Debug.Log($"{activeMic}");
        }
        else
        {
            Debug.LogError("mic not found");
        }
    }
    void Update()
    {
        if (isLocked) return;

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool rightTrigger = rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger) && rightTrigger;
        //bool leftTrigger = leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger) && leftTrigger;

        bool triggerPressed = rightTrigger || Input.GetKey(KeyCode.Space);

        if (triggerPressed && !isRecording)
        {
            if (learningMaterialController != null && learningMaterialController.IsOpen)
            {
                StartCoroutine(ShowPdfWarning());
            }
            else
            {
                StartRecording();
            }
        }
        else if (!triggerPressed && isRecording)
        {
            StopRecording();
        }
        
    }


    private void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;
        learningMaterialController?.LockInteraction(true);
        recordedClip = Microphone.Start(activeMic, false, maxRecordingSeconds, sampleRate);
        if (OnRecordingStarted != null)
        {
            OnRecordingStarted();
        }
        sessionLogger.LogUserSpeechStart();
        Debug.Log($"recording started ({activeMic})");
    }

    private void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample = Microphone.GetPosition(activeMic);
        Microphone.End(activeMic);
        float recordingDuration = (float)lastSample / (float)sampleRate;
        if (lastSample <= 0|| recordingDuration < 1f)
        {
            Debug.LogWarning("recording too short.");
            return;
        }

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("recorded", lastSample, recordedClip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);
        recordedClip = trimmed;

        if (OnRecordingStopped != null)
        {
            OnRecordingStopped();
        }

        sessionLogger.LogUserSpeechEnd();
        Debug.Log($"recording stopped");
        isLocked = true;
        if (OnAudioReady != null)
        {
            OnAudioReady(recordedClip);
        }
    }

    private IEnumerator ShowPdfWarning()
    {
        if (pdfIsStillOpenPopup  != null)
        {
            pdfIsStillOpenPopup .SetActive(true);
            yield return new WaitForSeconds(3f);
            pdfIsStillOpenPopup .SetActive(false);
        }
    }
}