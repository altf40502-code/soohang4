using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class GeminiChatUI : MonoBehaviour
{
    [Header("Gemini API")]
    [SerializeField] private string apiKey = "YOUR_GEMINI_API_KEY";
    [SerializeField] private string modelName = "gemini-3.5-flash";

    [TextArea(2, 5)]
    [SerializeField]
    private string systemPrompt =
        "너는 유니티 RPG 게임 안에 있는 친절한 가이드 NPC다. 플레이어의 행동에 직접적으로 개입해서는 안되며, 오로지 조언만 한다. 플레이어에게 한국어로 짧고 자연스럽게 대답해라. 답변은 2줄 이내로한다. 세계관 이외의 내용은 말하지 않는다.";

    [Header("UI")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform messageContent;
    [SerializeField] private TextMeshProUGUI messageTextPrefab;

    [Header("Option")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Slash;
    [SerializeField] private int maxHistoryCount = 12;

    private bool isOpen = false;
    private bool isWaitingResponse = false;

    private readonly List<GeminiContent> conversationHistory = new List<GeminiContent>();

    private void Start()
    {
        if (chatPanel != null)
            chatPanel.SetActive(false);

        if (sendButton != null)
            sendButton.onClick.AddListener(SendCurrentMessage);

        if (inputField != null)
        {
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.onSubmit.AddListener(_ => SendCurrentMessage());
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleChat();
        }
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(SendCurrentMessage);
    }

    private void ToggleChat()
    {
        isOpen = !isOpen;

        if (chatPanel != null)
            chatPanel.SetActive(isOpen);

        if (isOpen && inputField != null)
        {
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    private void SendCurrentMessage()
    {
        if (isWaitingResponse)
            return;

        if (inputField == null)
            return;

        string playerMessage = inputField.text.Trim();

        if (string.IsNullOrEmpty(playerMessage))
            return;

        inputField.text = "";

        AddMessageToUI("[나]", playerMessage);

        conversationHistory.Add(new GeminiContent("user", playerMessage));
        TrimHistory();

        StartCoroutine(RequestGemini(playerMessage));
    }

    private IEnumerator RequestGemini(string playerMessage)
    {
        isWaitingResponse = true;
        SetInputInteractable(false);

        AddMessageToUI("", "생각중...");

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";

        GeminiRequest requestData = new GeminiRequest
        {
            system_instruction = new GeminiContent("user", systemPrompt),
            contents = conversationHistory.ToArray(),
            generationConfig = new GeminiGenerationConfig
            {
                temperature = 0.8f,
                maxOutputTokens = 2048
            }
        };

        string json = JsonUtility.ToJson(requestData);

        using UnityWebRequest request = new UnityWebRequest(url, "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-goog-api-key", apiKey);

        yield return request.SendWebRequest();

        RemoveLastMessageIfLoading();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorMessage = ParseGeminiError(request.downloadHandler.text);
            AddMessageToUI("AI 오류", errorMessage);
        }
        else
        {
            string aiText = ParseGeminiResponse(request.downloadHandler.text);

            if (string.IsNullOrEmpty(aiText))
                aiText = "응답을 받았지만 텍스트를 찾을 수 없습니다.";

            AddMessageToUI("[NPC]", aiText);

            conversationHistory.Add(new GeminiContent("model", aiText));
            TrimHistory();
        }

        SetInputInteractable(true);
        isWaitingResponse = false;

        if (inputField != null)
        {
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    private void AddMessageToUI(string speaker, string message)
    {
        if (messageTextPrefab == null || messageContent == null)
            return;

        TextMeshProUGUI newText = Instantiate(messageTextPrefab, messageContent);
        newText.gameObject.SetActive(true);
        if (speaker == "AI")
        {
            newText.text = $"<b>{speaker}</b>\n<color=yellow>{message}</color>";
        }
        else
        {
            newText.text = $"<b>{speaker}</b>\n{message}";
        }

        StartCoroutine(ScrollToBottom());
    }

    private void RemoveLastMessageIfLoading()
    {
        if (messageContent == null)
            return;

        if (messageContent.childCount <= 0)
            return;

        Transform lastChild = messageContent.GetChild(messageContent.childCount - 1);
        TextMeshProUGUI text = lastChild.GetComponent<TextMeshProUGUI>();

        if (text != null && text.text.Contains("답변 생성 중"))
        {
            Destroy(lastChild.gameObject);
        }
    }

    private IEnumerator ScrollToBottom()
    {
        yield return null;

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    private void SetInputInteractable(bool value)
    {
        if (inputField != null)
            inputField.interactable = value;

        if (sendButton != null)
            sendButton.interactable = value;
    }

    private void TrimHistory()
    {
        while (conversationHistory.Count > maxHistoryCount)
        {
            conversationHistory.RemoveAt(0);
        }
    }

    private string ParseGeminiResponse(string json)
    {
        try
        {
            GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(json);

            if (response == null ||
                response.candidates == null ||
                response.candidates.Length == 0 ||
                response.candidates[0].content == null ||
                response.candidates[0].content.parts == null)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();

            foreach (GeminiPart part in response.candidates[0].content.parts)
            {
                if (!string.IsNullOrEmpty(part.text))
                    builder.Append(part.text);
            }

            return builder.ToString().Trim();
        }
        catch
        {
            return "";
        }
    }

    private string ParseGeminiError(string json)
    {
        try
        {
            GeminiErrorResponse errorResponse = JsonUtility.FromJson<GeminiErrorResponse>(json);

            if (errorResponse != null && errorResponse.error != null)
                return errorResponse.error.message;
        }
        catch
        {
            // ignored
        }

        return "Gemini API 요청에 실패했습니다.";
    }
}

[Serializable]
public class GeminiRequest
{
    public GeminiContent system_instruction;
    public GeminiContent[] contents;
    public GeminiGenerationConfig generationConfig;
}

[Serializable]
public class GeminiGenerationConfig
{
    public float temperature;
    public int maxOutputTokens;
}

[Serializable]
public class GeminiContent
{
    public string role;
    public GeminiPart[] parts;

    public GeminiContent(string role, string text)
    {
        this.role = role;
        this.parts = new GeminiPart[]
        {
            new GeminiPart { text = text }
        };
    }
}

[Serializable]
public class GeminiPart
{
    public string text;
}

[Serializable]
public class GeminiResponse
{
    public GeminiCandidate[] candidates;
}

[Serializable]
public class GeminiCandidate
{
    public GeminiContent content;
    public string finishReason;
}

[Serializable]
public class GeminiErrorResponse
{
    public GeminiError error;
}

[Serializable]
public class GeminiError
{
    public int code;
    public string message;
    public string status;
}