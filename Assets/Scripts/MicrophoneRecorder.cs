using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class MicrophoneRecorder : MonoBehaviour
{
    public System.Action<AudioClip> OnAudioReady;

    private AudioClip recordedClip;
    private bool isRecording = false;
    private int sampleRate = 44100;
    private int maxRecordingSeconds = 60;

    private const string HEADSET_MIC = "Headset Microphone (Oculus Virtual Audio Device)";
    private string activeMic => HEADSET_MIC;

    void Update()
    {
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool rightTrigger = rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger) && rightTrigger;
        bool leftTrigger = leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger) && leftTrigger;

        bool triggerPressed = rightTrigger || leftTrigger;

        if (triggerPressed && !isRecording)
            StartRecording();
        else if (!triggerPressed && isRecording)
            StopRecording();
    }

    private void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;
        recordedClip = Microphone.Start(activeMic, false, maxRecordingSeconds, sampleRate);
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

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("recorded", lastSample,
            recordedClip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);
        recordedClip = trimmed;

        Debug.Log($"Recording stopped: {lastSample / (float)sampleRate:F1}s");
        OnAudioReady?.Invoke(recordedClip);
    }


    /* Test ohneheadset
    private const string LAPTOP_MIC = "Mikrofonarray (Realtek(R) Audio)";
    private string testMic => LAPTOP_MIC;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            StartTestRecording();
        if (Input.GetKeyUp(KeyCode.Space))
            StopTestRecording();
    }

    private void StartTestRecording()
    {
        if (isRecording) return;
        isRecording = true;
        recordedClip = Microphone.Start(testMic, false, maxRecordingSeconds, sampleRate);
        Debug.Log($"Test Recording started ({testMic})");
    }

    private void StopTestRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample = Microphone.GetPosition(testMic);
        Microphone.End(testMic);

        if (lastSample <= 0)
        {
            Debug.LogWarning("No Audio recorded");
            return;
        }

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("recorded", lastSample,
            recordedClip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);
        recordedClip = trimmed;

        Debug.Log($"Test Recording stopped: {lastSample / (float)sampleRate:F1}s");
        OnAudioReady?.Invoke(recordedClip);
    }
    */
}