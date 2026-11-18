using System.Collections.Generic;
using UnityEngine;

/// <summary>Chat panel for talking to NPCs. Uses the AI backend when it is running,
/// and falls back to built-in local dialogue so the game always works offline.</summary>
public class DialogueUI : MonoBehaviour
{
    class Line { public bool player; public string speaker, text; }

    readonly List<Line> lines = new List<Line>();
    NpcChatClient chat;
    NpcActor npc;
    string input = "";
    Vector2 scroll;
    bool waiting, focusPending;
    int openFrame;
    string lastPlayerMessage = "";

    static readonly string[] Suggestions = {
        "Tell me about the village.", "Do you have a quest?", "What is happening in the forest?" };

    public bool IsOpen { get; private set; }
    public NpcActor Current { get { return npc; } }

    public void Init(NpcChatClient client)
    {
        chat = client;
        chat.OnNpcReply += HandleReply;
        chat.OnError += HandleError;
    }

    void OnDestroy()
    {
        if (chat != null) { chat.OnNpcReply -= HandleReply; chat.OnError -= HandleError; }
    }

    public void Open(NpcActor actor, string scriptedLine)
    {
        npc = actor;
        lines.Clear();
        input = ""; waiting = false;
        AddNpc(string.IsNullOrEmpty(scriptedLine) ? actor.greeting : scriptedLine);
        IsOpen = true; focusPending = true; openFrame = Time.frameCount;
        GameInput.Blocked = true;
        chat.SetNpc(actor.npcId);
    }

    public void Close()
    {
        IsOpen = false; npc = null;
        GameInput.Blocked = false;
        GUIUtility.keyboardControl = 0;
    }

    public void NpcSays(string text) { if (IsOpen) AddNpc(text); }

    void AddNpc(string text)
    {
        lines.Add(new Line { player = false, speaker = npc != null ? npc.displayName.ToUpper() : "NPC", text = text });
        scroll.y = float.MaxValue;
    }

    void AddPlayer(string text)
    {
        lines.Add(new Line { player = true, speaker = "YOU", text = text });
        scroll.y = float.MaxValue;
    }

    void HandleReply(string reply) { waiting = false; if (IsOpen) AddNpc(reply); }

    void HandleError(string error)
    {
        waiting = false;
        if (!IsOpen || npc == null) return;
        var gm = GameManager.I;
        AddNpc(LocalDialogue.Reply(npc.npcId, lastPlayerMessage, gm != null ? gm.stage : 0));
    }

    void Send(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || waiting || npc == null) return;
        var gm = GameManager.I;
        AddPlayer(text);
        lastPlayerMessage = text;
        waiting = true;
        chat.SetNpc(npc.npcId);
        if (gm != null) { chat.questStage = gm.stage; chat.playerLevel = gm.level; }
        chat.SendMessageToNpc(text);
    }

    void Update()
    {
        if (!IsOpen || npc == null) return;
        var gm = GameManager.I;
        if (gm != null && (gm.PlayerPos - npc.transform.position).magnitude > 10f) Close();
    }

    void OnGUI()
    {
        if (!IsOpen || npc == null) return;
        float s = UiKit.Scale;
        float w = Mathf.Min(780f * s, Screen.width - 40f), h = 340f * s;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 48f * s, w, h);
        UiKit.Box(r);

        // header
        Rect portrait = new Rect(r.x + 14f * s, r.y + 12f * s, 52f * s, 52f * s);
        UiKit.Fill(portrait, new Color(0.18f, 0.3f, 0.42f, 1f));
        UiKit.Text(portrait, npc.Initial, Mathf.RoundToInt(30f * s), UiKit.Gold, TextAnchor.MiddleCenter, true);
        UiKit.Text(new Rect(portrait.xMax + 12f * s, r.y + 12f * s, 400f * s, 26f * s), npc.displayName, Mathf.RoundToInt(20f * s), UiKit.Gold, TextAnchor.UpperLeft, true);
        UiKit.Text(new Rect(portrait.xMax + 12f * s, r.y + 40f * s, 400f * s, 20f * s), npc.role.ToUpper(), Mathf.RoundToInt(11f * s), new Color(1f, 1f, 1f, 0.65f));
        string status = chat.Checked ? (chat.ServerOnline ? (chat.AiEnabled ? "AI CONNECTED" : "LOCAL MODE") : "OFFLINE · LOCAL DIALOGUE") : "";
        UiKit.Text(new Rect(r.xMax - 330f * s, r.y + 14f * s, 270f * s, 20f * s), status, Mathf.RoundToInt(11f * s), UiKit.Green, TextAnchor.UpperRight, true);
        if (GUI.Button(new Rect(r.xMax - 46f * s, r.y + 10f * s, 34f * s, 30f * s), "X", UiKit.Button(Mathf.RoundToInt(14f * s)))) { Close(); return; }

        // messages
        Rect area = new Rect(r.x + 14f * s, r.y + 74f * s, r.width - 28f * s, r.height - 74f * s - 100f * s);
        GUILayout.BeginArea(area);
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < lines.Count; i++)
        {
            Line l = lines[i];
            GUIStyle sp = UiKit.Label(Mathf.RoundToInt(10f * s), TextAnchor.UpperLeft, true, false);
            sp.normal.textColor = l.player ? UiKit.Green : UiKit.Gold;
            GUILayout.Label(l.speaker, sp);
            GUIStyle tx = UiKit.Label(Mathf.RoundToInt(14f * s), TextAnchor.UpperLeft, false, true);
            tx.normal.textColor = Color.white;
            GUILayout.Label(l.text, tx);
            GUILayout.Space(6f * s);
        }
        if (waiting)
        {
            GUIStyle tx = UiKit.Label(Mathf.RoundToInt(13f * s), TextAnchor.UpperLeft, false, false);
            tx.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            GUILayout.Label(npc.displayName + " is thinking…", tx);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // suggestion buttons
        float bx = r.x + 14f * s, by = r.yMax - 92f * s;
        float bw = (r.width - 28f * s - 16f * s) / 3f;
        var small = UiKit.Button(Mathf.RoundToInt(11f * s));
        for (int i = 0; i < Suggestions.Length; i++)
            if (GUI.Button(new Rect(bx + i * (bw + 8f * s), by, bw, 30f * s), Suggestions[i], small)) Send(Suggestions[i]);

        // input row
        Event e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "chat_input")
        {
            Send(input); input = ""; e.Use();
        }
        float iy = r.yMax - 50f * s;
        GUI.SetNextControlName("chat_input");
        input = GUI.TextField(new Rect(bx, iy, r.width - 28f * s - 110f * s, 36f * s), input, 300, UiKit.Field(Mathf.RoundToInt(14f * s)));
        if (GUI.Button(new Rect(r.xMax - 14f * s - 100f * s, iy, 100f * s, 36f * s), "SEND", UiKit.Button(Mathf.RoundToInt(13f * s))))
        {
            Send(input); input = "";
        }
        if (focusPending && Event.current.type == EventType.Repaint && Time.frameCount > openFrame + 1)
        {
            GUI.FocusControl("chat_input"); focusPending = false;
        }
    }
}

/// <summary>Offline replies mirroring the backend's local fallback, aware of quest progress.</summary>
public static class LocalDialogue
{
    static bool Has(string t, params string[] words)
    {
        for (int i = 0; i < words.Length; i++) if (t.Contains(words[i])) return true;
        return false;
    }

    public static string Reply(string npc, string message, int stage)
    {
        string t = (message ?? "").ToLowerInvariant();
        if (Has(t, "quest", "mission", "help", "task"))
        {
            if (npc == "elder_mira")
            {
                if (stage == 0) return "There is a task only a brave traveler can take. Walk the northern road to the ruins and find the source of the blue beacon. Bring back any symbol you discover.";
                if (stage == 1) return "The Ancient Sigil rests on the altar beneath the beacon, far to the north. Follow the road and do not linger after dark.";
                if (stage == 2) return "You have the Sigil! Bring it to me and I will tell you what it means.";
                if (stage == 3) return "A Forest Wraith has come to the eastern pond. Strike it down before it frightens the village.";
                return "The village is safe for now, thanks to you. Rest, traveler.";
            }
            if (npc == "kael") return "Follow the road north and watch for imps in the forest. If the woods go silent, turn back. Carry a potion.";
            return "A merchant left a map here. Ask Kael about the northern trail, then tell me what hides near the ruins.";
        }
        if (Has(t, "forest", "woods", "ruins", "beacon", "north"))
        {
            if (npc == "elder_mira") return "The beacon is older than Brindlewood itself. The archive says it burns only when the seal beneath the observatory weakens.";
            if (npc == "kael") return "I saw blue light between the trees last night. Three sets of tracks went in, none came back. Something is wrong up there.";
            return "People say the trees whisper near the northern trail. I say people should stop wandering there after sunset!";
        }
        if (Has(t, "village", "brindlewood"))
        {
            if (npc == "elder_mira") return "Brindlewood has stood at the edge of the old forest for six hundred years. We keep our archive, our lamps and our promises.";
            if (npc == "kael") return "Quiet folk, good roofs, and a well that never runs dry. I just wish the forest would behave.";
            return "The best stew for fifty miles is served right here. The second best is served by my rival, but we don't talk about her.";
        }
        if (Has(t, "name", "who are you")) return "I am " + Name(npc) + ". " + Role(npc) + " of Brindlewood. What brings you here, traveler?";
        if (Has(t, "hello", "hi", "hey", "greetings")) return "Well met, traveler. I am " + Name(npc) + ". What would you like to know about Brindlewood?";
        if (npc == "elder_mira") return "There is more to this village than its quiet streets. Ask me about the old stories, or speak with Kael about the northern forest.";
        if (npc == "kael") return "Keep your eyes open out there. The forest has been acting strangely. Ask me about the tracks if you want to know more.";
        return "Welcome to the Lantern & Loaf! Rumors are free, but a warm meal costs a coin. Have you heard about the old well?";
    }

    static string Name(string id) { return id == "elder_mira" ? "Elder Mira" : id == "kael" ? "Kael" : "Sera"; }
    static string Role(string id) { return id == "elder_mira" ? "Village elder" : id == "kael" ? "Wandering ranger" : "Innkeeper"; }
}
