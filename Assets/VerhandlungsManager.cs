using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class NegotiationManager : MonoBehaviour
{
    [Header("OpenRouter Settings")]
    private string baseUrl = "https://openrouter.ai/api/v1/chat/completions";
    private string openRouterKey;
    private string openAIKey;

    [Header("UI")]
    public TextMeshProUGUI subtitleText;

    [Header("Audio")]
    public AudioSource audioSource;

    private List<Message> conversationHistory = new List<Message>();

    private string systemPrompt =
        "Du bist Susie Bauer, 48 Jahre alt, Abteilungsleiterin HR in einem " +
        "mittelgroßen deutschen Unternehmen. Du bist professionell, " +
        "aber zunächst skeptisch gegenüber Gehaltserhöhungen. " +
        "Du sprichst formelles Deutsch (Sie-Form). " +
        "Reagiere realistisch auf die Argumente des Mitarbeiters. " +
        "Halte deine Antworten kurz - maximal 3 Sätze.";

    [System.Serializable]
    private class Config
    {
        public string openrouter_api_key;
        public string openai_api_key;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class OpenRouterRequest
    {
        public string model = "anthropic/claude-haiku-4-5";
        public int max_tokens = 1024;
        public List<Message> messages;
    }

    [System.Serializable]
    private class OpenRouterResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }

    [System.Serializable]
    private class TTSRequest
    {
        public string model = "tts-1";
        public string input;
        public string voice = "nova";
    }

    void Awake()
    {
        TextAsset configFile = Resources.Load<TextAsset>("Config");
        if (configFile != null)
        {
            Config config = JsonUtility.FromJson<Config>(configFile.text);
            openRouterKey = config.openrouter_api_key;
            openAIKey = config.openai_api_key;
            Debug.Log("Config geladen!");
        }
        else
        {
            Debug.LogError("Config.json not found in Resources folder!");
        }

        // Auto-find AudioSource if not assigned
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        Debug.Log("AudioSource found: " + (audioSource == null ? "NO" : "YES"));

    }

    void Start()
    {
        conversationHistory.Add(new Message {
            role = "system",
            content = systemPrompt
        });

        StartCoroutine(SendToOpenRouter("Guten Tag, Sie wollten mich sprechen?"));
    }

    public void SendUserMessage(string userMessage)
    {
        StartCoroutine(SendToOpenRouter(userMessage));
    }

    private IEnumerator SendToOpenRouter(string userMessage)
    {
        if (subtitleText != null)
            subtitleText.text = "Susie denkt nach...";

        conversationHistory.Add(new Message {
            role = "user",
            content = userMessage
        });

        OpenRouterRequest request = new OpenRouterRequest
        {
            messages = conversationHistory
        };

        string jsonBody = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest www = new UnityWebRequest(baseUrl, "POST");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", "Bearer " + openRouterKey);
        www.SetRequestHeader("HTTP-Referer", "https://unity.com");
        www.SetRequestHeader("X-Title", "VR Negotiation Training");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string rawResponse = www.downloadHandler.text;
            Debug.Log("Raw response: " + rawResponse);

            OpenRouterResponse response = JsonUtility.FromJson<OpenRouterResponse>(rawResponse);
            string replyText = response.choices[0].message.content;

            conversationHistory.Add(new Message {
                role = "assistant",
                content = replyText
            });

            if (subtitleText != null)
                subtitleText.text = replyText;

            Debug.Log("Susie: " + replyText);
            Debug.Log("Sending to TTS: " + replyText.Substring(0, Mathf.Min(50, replyText.Length)));
            StartCoroutine(SpeakText(replyText));
        }
        else
        {
            Debug.LogError("Error: " + www.error);
            Debug.LogError("Response: " + www.downloadHandler.text);
            if (subtitleText != null)
                subtitleText.text = "Verbindungsfehler. Bitte versuchen Sie es erneut.";
        }
    }

    private IEnumerator SpeakText(string text)
    {
        TTSRequest ttsRequest = new TTSRequest { input = text };
        string jsonBody = JsonUtility.ToJson(ttsRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        UnityWebRequest www = new UnityWebRequest(
            "https://api.openai.com/v1/audio/speech", "POST");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerAudioClip(
            "https://api.openai.com/v1/audio/speech", AudioType.MPEG);
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", "Bearer " + openAIKey);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            Debug.Log("Clip: " + (clip == null ? "NULL" : clip.length + "s"));
            Debug.Log("AudioSource: " + (audioSource == null ? "NULL" : "found"));
            if (audioSource != null && clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("Susie spricht...");
            }
        }
        else
        {
            Debug.LogError("TTS Fehler: " + www.error);
            Debug.LogError("TTS Response: " + www.downloadHandler.text);
        }
    }
}
