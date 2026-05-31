using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Linq;


public class VoiceInput : MonoBehaviour
{
    [Header("Recording Settings")]
    private int recordingDuration = 30;
    private int sampleRate = 16000;

    private string apiKey;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private NegotiationManager negotiationManager;

    void Start()
    {
        LoadConfig();
        negotiationManager = GetComponent<NegotiationManager>();
        Debug.Log("Mikrofone verfügbar: " + string.Join(", ", Microphone.devices));
    }

    private void LoadConfig()
    {
        TextAsset configFile = Resources.Load<TextAsset>("Config");
        if (configFile != null)
        {
            Config config = JsonUtility.FromJson<Config>(configFile.text);
            apiKey = config.openai_api_key;
            Debug.Log("Config geladen! Key starts with: " + apiKey?.Substring(0, 8));
        }
        else
        {
            Debug.LogError("Config.json nicht gefunden in Resources!");
        }
    }

    void Update()
    {
      bool spacePressed = UnityEngine.InputSystem.Keyboard.current != null &&
          UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;

      bool aButtonPressed = UnityEngine.InputSystem.InputSystem.devices
          .OfType<UnityEngine.InputSystem.XR.XRController>()
          .Any(c => c.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton")
              is var btn && btn != null && btn.wasPressedThisFrame);

      if (spacePressed || aButtonPressed)
        {
            if (!isRecording)
                StartRecording();
            else
                StopAndTranscribe();
        }
    }

    private void StartRecording()
    {
        isRecording = true;
        recordingClip = Microphone.Start(null, false, recordingDuration, sampleRate);
        Debug.Log("Aufnahme gestartet... Drücken Sie Space zum Beenden");
    }

    private void StopAndTranscribe()
    {
        isRecording = false;
        int position = Microphone.GetPosition(null);
        Microphone.End(null);

        if (position <= 0)
        {
            Debug.Log("Keine Aufnahme gefunden");
            return;
        }

        float[] samples = new float[position * recordingClip.channels];
        recordingClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create("recording", position,
            recordingClip.channels, sampleRate, false);
        trimmedClip.SetData(samples, 0);

        Debug.Log("Aufnahme beendet, sende an Whisper...");
        StartCoroutine(SendToWhisper(trimmedClip));
    }

    private IEnumerator SendToWhisper(AudioClip clip)
    {
        byte[] wavData = ConvertToWAV(clip);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "recording.wav", "audio/wav");
        form.AddField("model", "whisper-1");
        form.AddField("language", "de");

        UnityWebRequest www = UnityWebRequest.Post(
            "https://api.openai.com/v1/audio/transcriptions", form);
        www.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = www.downloadHandler.text;
            Debug.Log("Whisper Antwort: " + jsonResponse);

            WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(jsonResponse);
            string transcribedText = response.text;

            Debug.Log("Sie sagten: " + transcribedText);

            if (negotiationManager != null && !string.IsNullOrEmpty(transcribedText))
                negotiationManager.SendUserMessage(transcribedText);
        }
        else
        {
            Debug.LogError("Whisper Fehler: " + www.error);
            Debug.LogError("Response: " + www.downloadHandler.text);
        }
    }

    private byte[] ConvertToWAV(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int sampleCount = samples.Length;
            int byteCount = sampleCount * 2;

            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + byteCount);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(byteCount);

            foreach (float sample in samples)
            {
                short s = (short)(sample * 32767f);
                writer.Write(s);
            }

            return stream.ToArray();
        }
    }

    [System.Serializable]
    private class Config
    {
        public string openai_api_key;
    }

    [System.Serializable]
    private class WhisperResponse
    {
        public string text;
    }
}
