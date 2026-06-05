using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine.XR;

public class PipelineManager : MonoBehaviour
{
    [Header("Components")]
    public MicrophoneRecorder recorder;
    public AudioSource avatarAudioSource;
    public SessionLogger sessionLogger;
    public LearningMaterialController learningMaterialController;

    [Header("Avatar")]
    public GameObject avatarRoot;
    public float delayAfterFadeBeforeSpeak = 1.5f;
    
    [Header("Panels")]
    public GameObject ControllerInstructions1Panel;
    public GameObject LearningMaterialCanvas;
    public GameObject TimeOverPopup;           
    public GameObject ControllerInstructions2Panel; 
    public GameObject TaskGoalPanel;           
    public GameObject InteractionOverPopup;
    
    [Header("Blink Transition")]
    public CanvasGroup fadingCanvas;
    public float blinkFadeDuration = 0.4f;

    public float studyDuration = 900f;

    private WebSocket websocket;
    private string lastUserText = "";
    private float lastRecordingDuration = 0f;
    private float userStartMs;
    private float aiStartMs;
    private int interactionCount = 0;
    private const int MAX_INTERACTIONS = 6;
    //private bool sessionEnded = false;

    private enum FlowStage { Instructions, Studying, ControllerInstructions2, TaskGoal, Interaction }
    private FlowStage currentStage = FlowStage.Instructions;
    private bool waitingForButtonPress = false;
    async void Start()
    { 
        fadingCanvas.alpha = 0f;
        fadingCanvas.interactable = false;
        fadingCanvas.blocksRaycasts = false;
        recorder.OnAudioReady += OnAudioReady; 
        recorder.isLocked = true;

        waitingForButtonPress = true;

        websocket = new WebSocket("ws://localhost:8765");
        websocket.OnOpen += () => Debug.Log("Server connected");
        websocket.OnError += (e) => Debug.LogError($"WebSocket Error: {e}");
        websocket.OnClose += (e) => Debug.Log("Connection closed");

        bool waitingForText = true;
        string pendingAiText = "";

        websocket.OnMessage += (bytes) =>
        {
            if (waitingForText)
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                var response = JsonUtility.FromJson<AIResponse>(json);
                pendingAiText = response.text;
                lastUserText = response.stt_text;
                Debug.Log("AI Text: " + response.text);
                Debug.Log("User Text: " + response.stt_text);

                if (lastUserText != null && lastUserText.Trim().Length > 0)
                {
                    sessionLogger.LogUserTurn(lastUserText);
                    sessionLogger.LogLLMResponseReceived();
                }    
                waitingForText = false;
            }
            else
            {
                StartCoroutine(PlayAudioAndLog(bytes, pendingAiText));
                waitingForText = true;
            }
        };
        connectToServer();
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
                websocket?.DispatchMessageQueue();
        #endif

        if (waitingForButtonPress && AnyControllerButtonPressed())
        {
            waitingForButtonPress = false;
            StartCoroutine(HandleButtonPress());
        }
    }

    private bool IsAnyButtonPressed(InputDevice device)
    {
        return
            GetBool(device, CommonUsages.triggerButton) ||
            GetBool(device, CommonUsages.gripButton) ||
            GetBool(device, CommonUsages.primaryButton) ||
            GetBool(device, CommonUsages.secondaryButton) ||
            GetBool(device, CommonUsages.primary2DAxisClick) ||
            GetBool(device, CommonUsages.menuButton);
    }

    private bool GetBool(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out bool value) && value;
    }

    private bool AnyControllerButtonPressed()
    {
        if (Input.anyKeyDown)
            return true;

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        return IsAnyButtonPressed(leftHand) || IsAnyButtonPressed(rightHand);
    }

    private IEnumerator HandleButtonPress()
    {
        switch (currentStage)
        {
            case FlowStage.Instructions:
                ControllerInstructions1Panel.SetActive(false);
                LearningMaterialCanvas.SetActive(true);
                if (learningMaterialController != null)
                    learningMaterialController.LockInteraction(true); 
                sessionLogger.LogStudyStart();
                currentStage = FlowStage.Studying;
                StartCoroutine(StudyingSequence());
                break;

            case FlowStage.ControllerInstructions2:
                ControllerInstructions2Panel.SetActive(false);
                TaskGoalPanel.SetActive(true);
                currentStage = FlowStage.TaskGoal;
                waitingForButtonPress = true;
                break;

            case FlowStage.TaskGoal:
                TaskGoalPanel.SetActive(false);
                currentStage = FlowStage.Interaction;
                StartCoroutine(StartInteraction());
                break;
        }
        yield break;
    }

    private IEnumerator StudyingSequence()
    {
        yield return new WaitForSeconds(studyDuration);
        float remaining = studyDuration;
        while (remaining > 0f)
        {
            Debug.Log($"Study time remaining: {Mathf.CeilToInt(remaining)} seconds");
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        LearningMaterialCanvas.SetActive(false);
        sessionLogger.LogStudyEnd();

        TimeOverPopup.SetActive(true);
        yield return new WaitForSeconds(3.5f);
            TimeOverPopup.SetActive(false);

        ControllerInstructions2Panel.SetActive(true);
        currentStage = FlowStage.ControllerInstructions2;
        waitingForButtonPress = true;
    }

    private async void connectToServer()
    {
        await websocket.Connect();
    }

    private IEnumerator StartInteraction()
    {   
        sessionLogger.LogInteractionStart();
        if (websocket.State == WebSocketState.Closed || 
        websocket.State == WebSocketState.Closing)
        {
            Debug.Log("WebSocket closed, reconnecting...");
            connectToServer();
        }

        float waited = 0f;
        while (websocket.State != WebSocketState.Open && waited < 5f)
        {
            yield return new WaitForSeconds(0.1f);
            waited += 0.1f;
        }

        if (websocket.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket failed to open before SendAgentType");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < blinkFadeDuration)
        {
            elapsed += Time.deltaTime;
            fadingCanvas.alpha = Mathf.Clamp01(elapsed / blinkFadeDuration);
            yield return null;
        }
        fadingCanvas.alpha = 1f;
        avatarRoot.SetActive(true);
        yield return new WaitForSeconds(0.5f);

        elapsed = 0f;
        while (elapsed < blinkFadeDuration)
        {
            elapsed += Time.deltaTime;
            fadingCanvas.alpha = 1f - Mathf.Clamp01(elapsed / blinkFadeDuration);
            yield return null;
        }
        fadingCanvas.alpha = 0f;

        SendAgentType();

        yield return new WaitForSeconds(delayAfterFadeBeforeSpeak);
    }

    public async void SendAgentType()
    {
        await System.Threading.Tasks.Task.Delay(500);
        string agentName = sessionLogger.variableTested.Trim().ToLower();
        string configJson = "{\"agent_type\": \"" + agentName + "\"}";
        websocket.Send(System.Text.Encoding.UTF8.GetBytes(configJson));
        Debug.Log("Sent agent type: " + agentName);
    }

    //private async void SendEndPrompt()
    //{
    //    string endJson = "{\"end_prompt\": true}";
    //    await websocket.Send(System.Text.Encoding.UTF8.GetBytes(endJson));
    //    Debug.Log("Sent end prompt");
    //}

    private void OnAudioReady(AudioClip clip)
    {
        SendAudioToServer(clip);
    }

    private async void SendAudioToServer(AudioClip clip)
    {
        if (websocket.State != WebSocketState.Open) return;

        Debug.Log($"Send Audio: {clip.samples} samples, {clip.channels} channels, {clip.frequency}Hz");

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] wavBytes = FloatsToWav(samples, clip.channels, clip.frequency);
        await websocket.Send(wavBytes);
        Debug.Log($"Audio sent! {wavBytes.Length} bytes");
    }

    private byte[] FloatsToWav(float[] samples, int channels, int frequency)
    {
        byte[] wav = new byte[44 + samples.Length * 2];
        System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, wav, 0, 4);
        System.BitConverter.GetBytes(wav.Length - 8).CopyTo(wav, 4);
        System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, wav, 8, 4);
        System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, wav, 12, 4);
        System.BitConverter.GetBytes(16).CopyTo(wav, 16);
        System.BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        System.BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        System.BitConverter.GetBytes(frequency).CopyTo(wav, 24);
        System.BitConverter.GetBytes(frequency * channels * 2).CopyTo(wav, 28);
        System.BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        System.BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("data"), 0, wav, 36, 4);
        System.BitConverter.GetBytes(samples.Length * 2).CopyTo(wav, 40);
        int offset = 44;
        foreach (float s in samples)
        {
            short val = (short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue);
            System.BitConverter.GetBytes(val).CopyTo(wav, offset);
            offset += 2;
        }
        return wav;
    }

    private IEnumerator PlayAudioAndLog(byte[] wavBytes, string aiText)
    {
        float[] samples = WavToFloats(wavBytes, out int channels, out int frequency);
        AudioClip clip = AudioClip.Create("AI_Response", samples.Length / channels, channels, frequency, false);
        clip.SetData(samples, 0);
        avatarAudioSource.clip = clip;
        recorder.isLocked = true;
        avatarAudioSource.Play();
        sessionLogger.LogAISpeechStart();

        yield return new WaitForSeconds(clip.length);
        sessionLogger.LogAITurn(aiText);
        sessionLogger.LogAISpeechEnd();

        interactionCount++;
        Debug.Log($"[Pipeline] Interaction {interactionCount}/{MAX_INTERACTIONS} complete");

        if (interactionCount >= MAX_INTERACTIONS)
        {
            sessionLogger.LogInteractionEnd();
            //sessionEnded = true;
            recorder.isLocked = true;
            if (InteractionOverPopup != null)
                InteractionOverPopup.SetActive(true);
            Debug.Log("Session ended. Locking recorder.");
        }
        else
        {
            recorder.isLocked = false;
        }
    }

    private float[] WavToFloats(byte[] wav, out int channels, out int frequency)
    {
        channels = System.BitConverter.ToInt16(wav, 22);
        frequency = System.BitConverter.ToInt32(wav, 24);
        int dataStart = 44;
        int sampleCount = (wav.Length - dataStart) / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short s = System.BitConverter.ToInt16(wav, dataStart + i * 2);
            samples[i] = s / 32768f;
        }
        return samples;
    }
    async void OnDestroy()
    {
        await websocket?.Close();
    }

    [System.Serializable]
    private class AIResponse
    {
        public string text;
        public string error;
        public string stt_text; 
        //public bool is_closing;
    }
}