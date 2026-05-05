using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class MicrophoneRecorder : MonoBehaviour
{
    public System.Action<AudioClip, float> OnAudioReady;
    private AudioClip recordedClip;
    private bool isRecording = false;
    private int sampleRate = 44100;
    private int maxRecordingSeconds = 60;
    private float recordingStartTime;
    public System.Action OnRecordingStarted;
    public System.Action OnRecordingStopped;
    private string activeMic = "Headset Microphone (Oculus Virtual Audio Device)"; 
    public bool isLocked = false;     


    void Update()
    {
        if (isLocked) return;

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool rightTrigger = rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger) && rightTrigger;
        bool leftTrigger = leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger) && leftTrigger;

        bool triggerPressed = rightTrigger || leftTrigger || Input.GetKey(KeyCode.Space);

        if (triggerPressed && !isRecording)
        {
            StartRecording();
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
        recordingStartTime = Time.time;
        recordedClip = Microphone.Start(activeMic, false, maxRecordingSeconds, sampleRate);
        if (OnRecordingStarted != null)
        {
            OnRecordingStarted();
        }
        Debug.Log($"Recording started ({activeMic})");
    }

    private void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample = Microphone.GetPosition(activeMic);
        Microphone.End(activeMic);

        if (lastSample <= 0)
        {
            Debug.LogWarning("No Audio recorded");
            return;
        }

        float duration = Time.time - recordingStartTime;

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("recorded", lastSample, recordedClip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);
        recordedClip = trimmed;

        if (OnRecordingStopped != null)
        {
            OnRecordingStopped();
        }

        Debug.Log($"Recording stopped: {duration:F1}s");
        
        if (OnAudioReady != null)
        {
            OnAudioReady(recordedClip, duration);
        }
    }
}