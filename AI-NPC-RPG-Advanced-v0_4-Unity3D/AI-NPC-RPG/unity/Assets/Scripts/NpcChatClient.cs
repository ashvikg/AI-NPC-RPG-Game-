using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class NpcChatRequest {
    public string session_id, npc_id, message, player_name;
    public int quest_stage, player_level;
}
[Serializable] public class NpcChatResponse {
    public string reply, npc;
    public bool ai_enabled;
}
[Serializable] public class ResetRequest { public string session_id; }
[Serializable] public class HealthResponse { public string status; public bool ai_enabled; }

/// <summary>Talks to the FastAPI dialogue backend. Works from the Inspector or created at runtime by GameManager.</summary>
public class NpcChatClient : MonoBehaviour {
    const string SessionKey = "brindlewood3d-session";

    [Header("Backend")]
    [SerializeField] private string apiUrl = "http://127.0.0.1:8000";
    [SerializeField] private string npcId = "elder_mira";
    [SerializeField] private string playerName = "Traveler";
    [SerializeField] private string sessionId = "";

    [Header("Game context sent with every message (set by GameManager)")]
    public int questStage;
    public int playerLevel = 1;

    public event Action<string> OnNpcReply;
    public event Action<string> OnError;
    public bool IsBusy { get; private set; }
    public bool Checked { get; private set; }
    public bool ServerOnline { get; private set; }
    public bool AiEnabled { get; private set; }

    private void Awake() {
        if (string.IsNullOrWhiteSpace(sessionId)) sessionId = PlayerPrefs.GetString(SessionKey, "");
        if (string.IsNullOrWhiteSpace(sessionId)) {
            sessionId = "unity-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(SessionKey, sessionId);
        }
    }

    public void Configure(string url) { if (!string.IsNullOrWhiteSpace(url)) apiUrl = url.TrimEnd('/'); }

    public void SendMessageToNpc(string message) {
        if (IsBusy || string.IsNullOrWhiteSpace(message)) return;
        StartCoroutine(PostChat(message.Trim()));
    }

    private IEnumerator PostChat(string message) {
        IsBusy = true;
        var payload = new NpcChatRequest {
            session_id = sessionId, npc_id = npcId, player_name = playerName, message = message,
            quest_stage = questStage, player_level = playerLevel
        };
        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using (var request = new UnityWebRequest(apiUrl + "/api/chat", "POST")) {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 40;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) {
                var data = JsonUtility.FromJson<NpcChatResponse>(request.downloadHandler.text);
                ServerOnline = true; AiEnabled = data.ai_enabled; Checked = true;
                IsBusy = false;
                OnNpcReply?.Invoke(data.reply);
            } else {
                ServerOnline = false; Checked = true;
                IsBusy = false;
                OnError?.Invoke("NPC connection failed: " + request.error);
            }
        }
        IsBusy = false;
    }

    public void SetNpc(string newNpcId) {
        if (!string.IsNullOrWhiteSpace(newNpcId)) npcId = newNpcId;
    }

    /// <summary>Pings /health so the HUD can show server / AI status.</summary>
    public IEnumerator CheckHealth() {
        using (var request = UnityWebRequest.Get(apiUrl + "/health")) {
            request.timeout = 4;
            yield return request.SendWebRequest();
            Checked = true;
            if (request.result == UnityWebRequest.Result.Success) {
                var h = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                ServerOnline = true; AiEnabled = h != null && h.ai_enabled;
            } else {
                ServerOnline = false; AiEnabled = false;
            }
        }
    }

    public void ResetConversation() {
        StartCoroutine(PostReset());
    }

    private IEnumerator PostReset() {
        var payload = new ResetRequest { session_id = sessionId };
        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using (var request = new UnityWebRequest(apiUrl + "/api/session/reset", "POST")) {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
        }
    }
}
