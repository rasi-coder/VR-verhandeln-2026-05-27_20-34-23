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
    private Animator animator;
    public float talkingY = 0f;
    public float idleY = 0f;

    [Header("UI")]
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI feedbackText;

    [Header("Audio")]
    public AudioSource audioSource;

    private List<Message> conversationHistory = new List<Message>();
    private VoiceInput voiceInput;

    private string LoadPrompt(string filename)
    {
        TextAsset file = Resources.Load<TextAsset>(filename);
        if (file != null)
            return file.text;
        Debug.LogError("Prompt file not found: " + filename);
        return "";
    }

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

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        animator = GetComponent<Animator>();
        voiceInput = GetComponent<VoiceInput>();
    }

    void Start()
    {
        string variantPrompt = LevelSelector.SelectedPromptVariant switch
        {
            "unterstützend" => LoadPrompt("PromptUnterstützend"),
            "streng" => LoadPrompt("PromptStreng"),
            _ => LoadPrompt("PromptNeutral")
        };

        string lengthInstruction;
          if (LevelSelector.LongAnswers)
          {
          Debug.Log("Answer length: LONG");
          lengthInstruction = "";
          }
          else
          {
          Debug.Log("Answer length: SHORT - max 50 words");
          lengthInstruction = "Halte deine Antworten kurz - maximal 50 Wörter.";
          }

        string fullPrompt = LoadPrompt("PromptBase")
            .Replace("{{PERSONALITY}}", variantPrompt)
            .Replace("{{LENGTH}}", lengthInstruction);

        conversationHistory.Add(new Message {
            role = "system",
            content = fullPrompt
        });

        StartCoroutine(SendToOpenRouter("Guten Tag, Sie wollten mich sprechen?"));
    }

    void Update()
    {
        #if UNITY_EDITOR // skip dialogue by pressing d on keyboard (not in glasses)
        if (UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
            StartCoroutine(StartFeedback());
        #endif
    }

    public void SendUserMessage(string userMessage)
    {
        StartCoroutine(SendToOpenRouter(userMessage));
    }

    public void DebugSkipToEnd()
    {
        StartCoroutine(StartFeedback());
    }

    public void ToggleSubtitles()
    {
        if (subtitleText != null)
            subtitleText.transform.parent.gameObject.SetActive(
                !subtitleText.transform.parent.gameObject.activeSelf);
    }

    private bool longAnswers = false;

    public void ToggleAnswerLength()
    {
        longAnswers = !longAnswers;
        Debug.Log("Answer length: " + (longAnswers ? "long" : "short"));
    }

    private IEnumerator SendToOpenRouter(string userMessage)
    {
        if (subtitleText != null)
            subtitleText.text = "Frau Schneider denkt nach...";

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
            OpenRouterResponse response = JsonUtility.FromJson<OpenRouterResponse>(rawResponse);
            string replyText = response.choices[0].message.content;

            // Detect end marker
            bool isEnd = replyText.Contains("[ENDE]");
            string cleanedReply = replyText.Replace("[ENDE]", "").Trim();

            conversationHistory.Add(new Message {
                role = "assistant",
                content = cleanedReply
            });

            if (subtitleText != null)
                subtitleText.text = cleanedReply;

            Debug.Log("Frau Schneider: " + cleanedReply);
            StartCoroutine(SpeakText(cleanedReply));

            if (isEnd)
                StartCoroutine(StartFeedback());
        }
        else
        {
            Debug.LogError("Error: " + www.error);
            Debug.LogError("Response: " + www.downloadHandler.text);
            if (subtitleText != null)
                subtitleText.text = "Verbindungsfehler. Bitte versuchen Sie es erneut.";
        }
    }

    private IEnumerator StartFeedback()
    {
        // Wait for Frau Schneider to finish speaking
        yield return new WaitWhile(() => audioSource.isPlaying);

        // Disable voice input so user can't keep talking
        if (voiceInput != null)
            voiceInput.enabled = false;

        // Build full transcript from conversation history (skip system prompt at index 0)
        StringBuilder transcript = new StringBuilder();
        transcript.AppendLine("=== Gesprächsprotokoll ===\n");

        for (int i = 2; i < conversationHistory.Count; i++)
        {
            Message msg = conversationHistory[i];
            if (msg.role == "user")
                transcript.AppendLine("Sie: " + msg.content + "\n");
            else if (msg.role == "assistant")
                transcript.AppendLine("Frau Schneider: " + msg.content + "\n");
        }

        string transcriptStr = transcript.ToString();

        if (subtitleText != null)
            subtitleText.text = transcriptStr;

        string feedbackPrompt = LoadPrompt("PromptFeedback")
            .Replace("{{TRANSCRIPT}}", transcriptStr);
        StartCoroutine(SendFeedbackRequest(feedbackPrompt, transcriptStr));
    }

    private IEnumerator SendFeedbackRequest(string feedbackPrompt, string transcriptStr)
    {
        if (feedbackText != null)
            feedbackText.text = "Feedback wird geladen...";

        OpenRouterRequest request = new OpenRouterRequest
        {
            messages = new List<Message>
            {
                new Message { role = "user", content = feedbackPrompt }
            }
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
            string raw = www.downloadHandler.text;
            OpenRouterResponse response = JsonUtility.FromJson<OpenRouterResponse>(raw);
            string replyText = response.choices[0].message.content;

            ParseFeedbackResponse(replyText,
                out string feedbackBody,
                out List<string> positiveQuotes,
                out List<string> improvementQuotes);

            if (subtitleText != null)
                subtitleText.text = ApplyHighlights(transcriptStr, positiveQuotes, improvementQuotes);

            if (feedbackText != null)
                feedbackText.text = feedbackBody;

            Debug.Log("Feedback: " + feedbackBody);
        }
        else
        {
            Debug.LogError("Feedback Fehler: " + www.error);
            if (feedbackText != null)
                feedbackText.text = "Feedback konnte nicht geladen werden.";
        }
    }

    private void ParseFeedbackResponse(string response,
        out string feedbackBody,
        out List<string> positiveQuotes,
        out List<string> improvementQuotes)
    {
        feedbackBody = "";
        positiveQuotes = new List<string>();
        improvementQuotes = new List<string>();

        string currentSection = "";
        System.Text.StringBuilder feedbackBuilder = new System.Text.StringBuilder();

        foreach (string rawLine in response.Split('\n'))
        {
            string line = rawLine.Trim();

            if (line == "FEEDBACK:")               { currentSection = "feedback"; continue; }
            if (line == "HIGHLIGHTS_POSITIV:")     { currentSection = "positiv"; continue; }
            if (line == "HIGHLIGHTS_VERBESSERUNG:"){ currentSection = "verbesserung"; continue; }

            if (string.IsNullOrEmpty(line)) continue;

            switch (currentSection)
            {
                case "feedback":
                    feedbackBuilder.AppendLine(line);
                    break;
                case "positiv":
                    positiveQuotes.Add(line);
                    break;
                case "verbesserung":
                    improvementQuotes.Add(line);
                    break;
            }
        }

        feedbackBody = feedbackBuilder.ToString().Trim();
    }

    private string ApplyHighlights(string transcript,
        List<string> positiveQuotes,
        List<string> improvementQuotes)
    {
        foreach (string quote in positiveQuotes)
            if (!string.IsNullOrEmpty(quote))
                transcript = transcript.Replace(quote, $"<mark=#00C85080>{quote}</mark>");

        foreach (string quote in improvementQuotes)
            if (!string.IsNullOrEmpty(quote))
                transcript = transcript.Replace(quote, $"<mark=#FF990080>{quote}</mark>");

        return transcript;
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
            if (audioSource != null && clip != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                animator.SetBool("isTalking", true);

                yield return new WaitWhile(() => audioSource.isPlaying);

                animator.SetBool("isTalking", false);
            }
        }
        else
        {
            Debug.LogError("TTS Fehler: " + www.error);
            Debug.LogError("TTS Response: " + www.downloadHandler.text);
        }
    }
}
