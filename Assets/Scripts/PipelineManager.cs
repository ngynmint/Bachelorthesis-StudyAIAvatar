using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NativeWebSocket;

public class PipelineManager : MonoBehaviour
{
    [Header("Components")]
    public MicrophoneRecorder recorder;
    public AudioSource avatarAudioSource;
    public SessionLogger sessionLogger;
    public GameObject InteractionOverPopup;

    private WebSocket websocket;
    private string lastUserText = "";
    private float lastRecordingDuration = 0f;

    private float userStartMs;
    private float aiStartMs;
    private int exchangeCount = 0;
    private const int MAX_EXCHANGES = 5;
    private bool sessionEnded = false;

    async void Start()
    { 
        recorder.OnAudioReady += OnAudioReady; 
        

        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log("Server connected");
        };
        websocket.OnError += (e) => Debug.LogError($"WebSocket Error: {e}");
        websocket.OnClose += (e) => Debug.Log("Connection closed");

        bool waitingForText = true;
        string pendingAiText = "";
        bool pendingIsClosing = false;

        websocket.OnMessage += (bytes) =>
        {
            if (waitingForText)
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                var response = JsonUtility.FromJson<AIResponse>(json);
                pendingAiText = response.text;
                lastUserText = response.stt_text;
                pendingIsClosing = response.is_closing;
                Debug.Log("AI Text: " + response.text);
                Debug.Log("User Text: " + response.stt_text);

                if (lastUserText != null && lastUserText.Trim().Length > 0)
                    sessionLogger.LogUserTurn(lastUserText, lastRecordingDuration, userStartMs);

                waitingForText = false;
            }
            else
            {
                aiStartMs = (Time.time - sessionLogger.GetSessionStartTime()) * 1000f;
                StartCoroutine(PlayAudioAndLog(bytes, pendingAiText, pendingIsClosing));
                waitingForText = true;
            }
        };

        await websocket.Connect();
    }

    public async void SendPromptFile()
    {
        await System.Threading.Tasks.Task.Delay(500);
        string agentName = sessionLogger.variableTested.Trim().ToLower();
        string configJson = "{\"agent_type\": \"" + agentName + "\"}";
        await websocket.Send(System.Text.Encoding.UTF8.GetBytes(configJson));
        Debug.Log("Sent agent type: " + agentName);
    }
    private async void SendEndPrompt()
    {
        string endJson = "{\"end_prompt\": true}";
        await websocket.Send(System.Text.Encoding.UTF8.GetBytes(endJson));
        Debug.Log("Sent end prompt");
    }
    private void OnAudioReady(AudioClip clip, float duration, float startTime)
    {
        lastRecordingDuration = duration;
        userStartMs = (startTime - sessionLogger.GetSessionStartTime()) * 1000f;
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

    private IEnumerator PlayAudioAndLog(byte[] wavBytes, string aiText, bool isClosing)
    {
        float[] samples = WavToFloats(wavBytes, out int channels, out int frequency);
        AudioClip clip = AudioClip.Create("AI_Response", 
        samples.Length / channels, channels, frequency, false);
        clip.SetData(samples, 0);
        avatarAudioSource.clip = clip;
        recorder.isLocked = true;
        float playStartMs = (Time.time - sessionLogger.GetSessionStartTime()) * 1000f;
        avatarAudioSource.Play();

        yield return new WaitForSeconds(clip.length);
        sessionLogger.LogAITurn(aiText, clip.length, playStartMs);

        if (isClosing)
        {
            sessionEnded = true;
            recorder.isLocked = true;
            if (InteractionOverPopup != null)
                InteractionOverPopup.SetActive(true);
            Debug.Log("Session ended. Locking Recorder");
        }
        else
        {
            exchangeCount++;
            Debug.Log($"[Pipeline] Exchange {exchangeCount}/{MAX_EXCHANGES} complete");

            if (exchangeCount >= MAX_EXCHANGES)
            {
                recorder.isLocked = true;
                SendEndPrompt();
            }
            else
            {
                recorder.isLocked = false;
            }
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


    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
        #endif
        //Debug.Log("websocket state: " + websocket?.State);
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
        public bool is_closing;
    }
}