using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcChatUI : MonoBehaviour {
    [SerializeField] private NpcChatClient client;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text conversation;
    [SerializeField] private Button sendButton;

    private void OnEnable() {
        client.OnNpcReply += HandleReply;
        client.OnError += HandleError;
        sendButton.onClick.AddListener(Send);
    }
    private void OnDisable() {
        client.OnNpcReply -= HandleReply;
        client.OnError -= HandleError;
        sendButton.onClick.RemoveListener(Send);
    }
    private void Send() {
        if (string.IsNullOrWhiteSpace(input.text)) return;
        conversation.text += "\n<color=#B6F27C>YOU</color>\n" + input.text + "\n";
        client.SendMessageToNpc(input.text);
        input.text = "";
    }
    private void HandleReply(string reply) {
        conversation.text += "\n<color=#B6F27C>NPC</color>\n" + reply + "\n";
    }
    private void HandleError(string error) {
        conversation.text += "\n<color=#E5A46B>" + error + "</color>\n";
    }
}
