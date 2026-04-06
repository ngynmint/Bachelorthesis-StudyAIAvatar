using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using NativeWebSocket;

public class AIManager : MonoBehaviour
{
    [Header("Komponenten")]
    public MicrophoneRecorder recorder;
    public AudioSource avatarAudioSource;

    private WebSocket websocket;
    private bool waitingForText = true;
    private string lastAIText = "";

    public System.Action<string> OnAITextReady;
    public System.Action<AudioClip> OnAIAudioReady;

    async void Start()
    {
        recorder.OnAudioReady += SendAudioToServer;

        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () => Debug.Log("Server verbunden!");
        websocket.OnError += (e) => Debug.LogError($"WebSocket Error: {e}");
        websocket.OnClose += (e) => Debug.Log("Verbindung getrennt");

        websocket.OnMessage += (bytes) =>
        {
            if (waitingForText)
            {
                // Erste Nachricht = Text
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                var response = JsonUtility.FromJson<AIResponse>(json);
                lastAIText = response.text;
                OnAITextReady?.Invoke(lastAIText);
                waitingForText = false;
                Debug.Log($"KI Text: {lastAIText}");
            }
            else
            {
                // Zweite Nachricht = Audio
                StartCoroutine(PlayAudio(bytes));
                waitingForText = true;
            }
        };

        await websocket.Connect();
    }

    private async void SendAudioToServer(AudioClip clip)
    {
        if (websocket.State != WebSocketState.Open) return;

        waitingForText = true;

        // AudioClip → bytes
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] bytes = new byte[samples.Length * 2];
        int offset = 0;
        foreach (float s in samples)
        {
            short val = (short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue);
            System.BitConverter.GetBytes(val).CopyTo(bytes, offset);
            offset += 2;
        }

        await websocket.Send(bytes);
        Debug.Log("Audio gesendet!");
    }

    private IEnumerator PlayAudio(byte[] wavBytes)
    {
        // WAV bytes → AudioClip
        float[] samples = WavToFloats(wavBytes, out int channels, out int frequency);

        AudioClip clip = AudioClip.Create("AI_Response", 
            samples.Length / channels, channels, frequency, false);
        clip.SetData(samples, 0);

        avatarAudioSource.clip = clip;
        avatarAudioSource.Play();

        OnAIAudioReady?.Invoke(clip);

        yield return null;
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
    }
}