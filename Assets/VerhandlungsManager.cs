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
    private string apiKey;

    [Header("UI")]
    public TextMeshProUGUI subtitleText;

    private List<Message> conversationHistory = new List<Message>();

    private string systemPrompt =
        "Du bist Thomas Bauer, 48 Jahre alt, Abteilungsleiter HR in einem " +
        "mittelgroßen deutschen Unternehmen. Du bist professionell, " +
        "aber zunächst skeptisch gegenüber Gehaltserhöhungen. " +
        "Du sprichst formelles Deutsch (Sie-Form). " +
        "Reagiere realistisch auf die Argumente des Mitarbeiters. " +
        "Halte deine Antworten kurz - maximal 3 Sätze.";

    [System.Serializable]
    private class Config
    {
        public string apiKey;
    }
    // ----------------

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class OpenRouterRequest
    {
        public string model = "anthropic/claude-opus-4";
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

    void Awake()
    {
        // --- ADD THIS ---
        TextAsset configFile = Resources.Load<TextAsset>("Config");
        if (configFile != null)
        {
            Config config = JsonUtility.FromJson<Config>(configFile.text);
            apiKey = config.apiKey;
        }
        else
        {
            Debug.LogError("Config.json not found in Resources folder!");
        }
        // ----------------
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
            subtitleText.text = "Thomas denkt nach...";

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
        www.SetRequestHeader("Authorization", "Bearer " + apiKey);
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

            Debug.Log("Thomas: " + replyText);
        }
        else
        {
            Debug.LogError("Error: " + www.error);
            Debug.LogError("Response: " + www.downloadHandler.text);
            if (subtitleText != null)
                subtitleText.text = "Verbindungsfehler. Bitte versuchen Sie es erneut.";
        }
    }
}
