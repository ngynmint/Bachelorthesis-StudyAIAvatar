using UnityEngine;
using UnityEngine.InputSystem;

public class MicrophoneRecorder : MonoBehaviour
{
    [Header("Input Action")]
    public InputActionReference recordButton;

    public System.Action<AudioClip> OnAudioReady;

    private AudioClip recordedClip;
    private bool isRecording = false;
    private int sampleRate = 44100;
    private int maxRecordingSeconds = 15;

    void OnEnable()
    {
        recordButton.action.started  += OnButtonPressed;
        recordButton.action.canceled += OnButtonReleased;
        recordButton.action.Enable();
    }

    void OnDisable()
    {
        recordButton.action.started  -= OnButtonPressed;
        recordButton.action.canceled -= OnButtonReleased;
        recordButton.action.Disable();
    }

    private void OnButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isRecording) return;
        isRecording = true;
        recordedClip = Microphone.Start(null, false, maxRecordingSeconds, sampleRate);
        Debug.Log("Recording started;:");
    }

    private void OnButtonReleased(InputAction.CallbackContext ctx)
    {
        if (!isRecording) return;
        isRecording = false;

        int lastSample = Microphone.GetPosition(null);
        Microphone.End(null);

        float[] samples = new float[lastSample * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("recorded", lastSample,
            recordedClip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);
        recordedClip = trimmed;

        Debug.Log($"Recording stopped: {lastSample / (float)sampleRate:F1}s");
        OnAudioReady?.Invoke(recordedClip);
    }

    //Test
    void Update()
    {
    if (Input.GetKeyDown(KeyCode.Space))
        OnButtonPressed(default);

    if (Input.GetKeyUp(KeyCode.Space))
        OnButtonReleased(default);
    }
}