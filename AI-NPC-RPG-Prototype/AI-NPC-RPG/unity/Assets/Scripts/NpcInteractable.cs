using UnityEngine;

public class NpcInteractable : MonoBehaviour {
    [SerializeField] private string npcId = "elder_mira";
    [SerializeField] private NpcChatClient chatClient;
    [SerializeField] private GameObject dialoguePanel;

    public void Interact() {
        chatClient.SetNpc(npcId);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
    }
}
