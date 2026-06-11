using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine.XR;
using UnityEngine.InputSystem;

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
    public GameObject taskBoard;
    
    [Header("Blink Transition")]
    public CanvasGroup fadingCanvas;
    public float blinkFadeDuration = 0.4f;

    [Header("Proceed Buttons")]
    public GameObject StartLearningButton;
    public GameObject ContinueButton;
    public GameObject StartInteractionButton;
    public float startLearningLockDuration = 4f;
    public float continueLockDuration = 4f;
    public float startInteractionLockDuration = 4f;

    public float studyDuration = 900f;
    public AvatarAnimationController avatarAnimationController;
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
    private bool panelLocked = true;
    async void Start()
    { 
        fadingCanvas.alpha = 0f;
        fadingCanvas.interactable = false;
        fadingCanvas.blocksRaycasts = false;
        recorder.OnAudioReady += OnAudioReady; 
        recorder.isLocked = true;
        learningMaterialController.LockInteraction(true); 

        sessionLogger.LogPanelOpen("ControllerInstructions1");
        StartCoroutine(LockThenReveal(StartLearningButton, startLearningLockDuration, () => waitingForButtonPress = true));
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
        HandleKeyboardPanelRecovery();
    }
    private void HandleKeyboardPanelRecovery()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            RecoverToPanel(1);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            RecoverToPanel(2);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            RecoverToPanel(3);
    }

    private void RecoverToPanel(int panelNumber)
    {
        StopAllCoroutines();
        waitingForButtonPress = false;
        panelLocked = true;
 
        ControllerInstructions1Panel.SetActive(false);
        ControllerInstructions2Panel.SetActive(false);
        TaskGoalPanel.SetActive(false);
        LearningMaterialCanvas.SetActive(false);
        if (StartLearningButton != null) StartLearningButton.SetActive(false);
        if (TimeOverPopup != null) TimeOverPopup.SetActive(false);
        if (ContinueButton != null) ContinueButton.SetActive(false);
        if (StartInteractionButton != null) StartInteractionButton.SetActive(false);
 
        sessionLogger.LogPanelRecovery(panelNumber);
 
        switch (panelNumber)
        {
            case 1:
                sessionLogger.LogPanelOpen("ControllerInstructions1");
                ControllerInstructions1Panel.SetActive(true);
                currentStage = FlowStage.Instructions;
                StartCoroutine(LockThenReveal(StartLearningButton, startLearningLockDuration, () => { waitingForButtonPress = true; }));
                break;
 
            case 2:
                sessionLogger.LogPanelOpen("ControllerInstructions2");
                ControllerInstructions2Panel.SetActive(true);
                currentStage = FlowStage.ControllerInstructions2;
                StartCoroutine(LockThenReveal(ContinueButton, continueLockDuration, () => { waitingForButtonPress = true; }));
                break;
 
            case 3:
                sessionLogger.LogPanelOpen("TaskGoal");
                TaskGoalPanel.SetActive(true);
                currentStage = FlowStage.TaskGoal;
                StartCoroutine(LockThenReveal(StartInteractionButton, startInteractionLockDuration, () => { waitingForButtonPress = true; }));
                break;
        }
    }

    private IEnumerator LockThenReveal(GameObject label, float lockDuration, System.Action onUnlocked)
    {
        panelLocked = true;
        yield return new WaitForSeconds(lockDuration);
        if (label != null) label.SetActive(true);
        panelLocked = false;
        onUnlocked?.Invoke();
    }

    private bool IsAnyButtonPressed(UnityEngine.XR.InputDevice device)
    {   
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        return true;
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerVal) && triggerVal > 0.7f)
            return true;

        //if (device.TryGetFeatureValue(CommonUsages.grip, out float gripVal) && gripVal > 0.7f)
            //return true;

        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryTouch, out bool primaryTouched);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryTouch, out bool secondaryTouched);

        if (GetBool(device, UnityEngine.XR.CommonUsages.primaryButton) && !primaryTouched)
            return true;

        if (GetBool(device, UnityEngine.XR.CommonUsages.secondaryButton) && !secondaryTouched)
            return true;

        if (GetBool(device, UnityEngine.XR.CommonUsages.primary2DAxisClick))
            return true;

        if (GetBool(device, UnityEngine.XR.CommonUsages.menuButton))
            return true;

        return false;
    }

    private bool GetBool(UnityEngine.XR.InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out bool value) && value;
    }

    private bool AnyControllerButtonPressed()
    {

        UnityEngine.XR.InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        UnityEngine.XR.InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
           IsAnyButtonPressed(leftHand) ||
           IsAnyButtonPressed(rightHand);
    }

    private IEnumerator HandleButtonPress()
    {
        switch (currentStage)
        {
            case FlowStage.Instructions:
                ControllerInstructions1Panel.SetActive(false);
                sessionLogger.LogPanelClose("ControllerInstructions1");
                if (StartLearningButton != null) StartLearningButton.SetActive(false);
                LearningMaterialCanvas.SetActive(true);
                if (learningMaterialController != null) learningMaterialController.LockInteraction(true); 
                sessionLogger.LogStudyStart();
                currentStage = FlowStage.Studying;
                StartCoroutine(StudyingSequence());
                break;

            case FlowStage.ControllerInstructions2:
                ControllerInstructions2Panel.SetActive(false);
                sessionLogger.LogPanelClose("ControllerInstructions2");
                if (ContinueButton != null) ContinueButton.SetActive(false);
                TaskGoalPanel.SetActive(true);
                sessionLogger.LogPanelOpen("TaskGoal");
                currentStage = FlowStage.TaskGoal;
                StartCoroutine(LockThenReveal(StartInteractionButton, startInteractionLockDuration, () => { waitingForButtonPress = true; }));
                break;

            case FlowStage.TaskGoal:
                TaskGoalPanel.SetActive(false);
                sessionLogger.LogPanelClose("TaskGoal");
                if (StartInteractionButton != null) StartInteractionButton.SetActive(false);
                currentStage = FlowStage.Interaction;
                StartCoroutine(StartInteraction());
                break;
        }
        yield break;
    }

    private IEnumerator StudyingSequence()
    {
        yield return new WaitForSeconds(studyDuration);

        LearningMaterialCanvas.SetActive(false);
        sessionLogger.LogStudyEnd();

        TimeOverPopup.SetActive(true);
        yield return new WaitForSeconds(4.5f);
        TimeOverPopup.SetActive(false);

        ControllerInstructions2Panel.SetActive(true);
        sessionLogger.LogPanelOpen("ControllerInstructions2");
        currentStage = FlowStage.ControllerInstructions2;
        StartCoroutine(LockThenReveal(ContinueButton, continueLockDuration, () => { waitingForButtonPress = true; }));
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
        taskBoard.SetActive(true);      
        if (learningMaterialController != null) learningMaterialController.LockInteraction(false);
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
        avatarAnimationController?.SetThinking(true);
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
        if (interactionCount >=1)
        {
            avatarAnimationController?.SetThinking(false);
            yield return new WaitForSeconds(2f);
        }
    
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