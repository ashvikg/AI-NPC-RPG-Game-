using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class NpcChatRequest {
    public string session_id, npc_id, message, player_name;
}
[Serializable] public class NpcChatResponse {
    public string reply, npc;
    public bool ai_enabled;
}
[Serializable] public class ResetRequest { public string session_id; }

public class NpcChatClient : MonoBehaviour {
    [Header("Backend")]
    [SerializeField] private string apiUrl = "http://127.0.0.1:8000";
    [SerializeField] private string npcId = "elder_mira";
    [SerializeField] private string playerName = "Traveler";
    [SerializeField] private string sessionId = "";

    public event Action<string> OnNpcReply;
    public event Action<string> OnError;
    public bool IsBusy { get; private set; }

    private void Awake() {
        if (string.IsNullOrWhiteSpace(sessionId))
            sessionId = "unity-" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    public void SendMessageToNpc(string message) {
        if (IsBusy || string.IsNullOrWhiteSpace(message)) return;
        StartCoroutine(PostChat(message.Trim()));
    }

    private IEnumerator PostChat(string message) {
        IsBusy = true;
        var payload = new NpcChatRequest {
            session_id = sessionId, npc_id = npcId,
            player_name = playerName, message = message
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
                OnNpcReply?.Invoke(data.reply);
            } else {
                OnError?.Invoke("NPC connection failed: " + request.error);
            }
        }
        IsBusy = false;
    }

    public void SetNpc(string newNpcId) {
        if (!string.IsNullOrWhiteSpace(newNpcId)) npcId = newNpcId;
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
