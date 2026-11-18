using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public int version = 1, seed, stage, hp, gold, potions, xp, level, wraithHp, day;
    public bool wraithDead;
    public string[] items;
    public float px, py, pz, hour;
}

/// <summary>
/// Owns game state (quest, health, inventory), builds the world and actors, handles save/load and pausing.
/// Created automatically by GameBootstrap, so no scene setup is needed.
/// </summary>
public class GameManager : MonoBehaviour
{
    public class ToastItem { public string text; public float born; }

    public static GameManager I;
    const string SaveKey = "brindlewood3d-save-v1";

    public static readonly string[] Objectives = {
        "Speak with Elder Mira in front of the Elder's Hall to begin your adventure.",
        "Follow the northern road to the ruins beneath the blue beacon and take the Ancient Sigil (E).",
        "Return to Elder Mira and report your discovery.",
        "Defeat the Forest Wraith by the eastern pond.",
        "Quest complete! Brindlewood is safer. Explore, hunt imps, or generate a new world (Esc)."
    };
    public static readonly string[] StatusText = { "NOT STARTED", "IN PROGRESS", "RETURN TO MIRA", "DEFEAT THE WRAITH", "COMPLETED" };
    public static readonly float[] Progress = { 0f, 0.25f, 0.55f, 0.8f, 1f };

    // --- systems (created at runtime)
    public WorldGenerator world;
    public DayNightCycle dayNight;
    public ThirdPersonCamera camRig;
    public Camera cam;
    public NpcChatClient chat;
    public HudUI hud;
    public DialogueUI dialogue;
    public PlayerController player;

    // --- game state
    public int seed, stage, hp = 100, gold, potions = 2, xp, level = 1, wraithHp = 30;
    public bool wraithDead;
    public List<string> items = new List<string>();
    public string Prompt = "";
    public float hurtTimer;
    public readonly List<ToastItem> Toasts = new List<ToastItem>();

    public bool Paused { get; private set; }
    public bool PlayerDead { get; private set; }
    public bool Loading { get; private set; }

    Light sun, moon;
    EnemyAI wraith;
    int epoch;
    float nextAutosave;

    public int MaxHp { get { return 100 + (level - 1) * 10; } }
    public int XpToNext { get { return 100 * level; } }
    public int PlayerDamage { get { return 10 + 2 * (level - 1); } }
    public float Night { get { return dayNight != null ? dayNight.NightAmount : 0f; } }
    public Vector3 PlayerPos { get { return player != null ? player.transform.position : Vector3.zero; } }
    public Vector3 PlayerForward { get { return player != null ? player.transform.forward : Vector3.forward; } }

    // ------------------------------------------------------------------ lifecycle
    void Awake() { I = this; }

    void Start() { StartCoroutine(Boot()); }

    IEnumerator Boot()
    {
        SetupScene();
        chat = gameObject.AddComponent<NpcChatClient>();
        dayNight = gameObject.AddComponent<DayNightCycle>();
        dayNight.Init(cam, sun, moon);
        world = gameObject.AddComponent<WorldGenerator>();
        hud = gameObject.AddComponent<HudUI>();
        dialogue = gameObject.AddComponent<DialogueUI>();
        dialogue.Init(chat);
        StartCoroutine(chat.CheckHealth());

        SaveData sd = LoadSave();
        int s = sd != null ? sd.seed : Random.Range(1, 999999999);
        yield return BuildWorld(s, sd);
    }

    static Light[] FindLights()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        return FindObjectsOfType<Light>();
#endif
    }

    void SetupScene()
    {
        foreach (Light l in FindLights())
            if (l.type == LightType.Directional) Destroy(l.gameObject);

        var sunGo = new GameObject("Sun");
        sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.9f;
        var moonGo = new GameObject("Moon");
        moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional; moon.shadows = LightShadows.None;

        cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera");
            cg.tag = "MainCamera";
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.fieldOfView = 60f; cam.nearClipPlane = 0.2f;
        camRig = cam.GetComponent<ThirdPersonCamera>();
        if (camRig == null) camRig = cam.gameObject.AddComponent<ThirdPersonCamera>();
    }

    // ------------------------------------------------------------ world building
    IEnumerator BuildWorld(int newSeed, SaveData sd)
    {
        Loading = true;
        GameInput.Blocked = true;
        yield return null;
        yield return null;   // let the loading screen render before the heavy work

        epoch++;
        seed = newSeed;
        ClearActors();
        world.Generate(seed);
        dayNight.windowMat = world.WindowMat;
        dayNight.lampMat = world.LampMat;

        if (player == null)
        {
            var pgo = PlayerController.Create(camRig);
            player = pgo.GetComponent<PlayerController>();
            camRig.target = pgo.transform;
        }
        SpawnNpcs();
        SpawnImps();

        Vector3 pos = world.PlayerSpawn;
        if (sd != null && sd.seed == seed && !(sd.px == 0f && sd.pz == 0f))
            pos = new Vector3(sd.px, world.HeightAt(sd.px, sd.pz) + 0.6f, sd.pz);
        player.Teleport(pos);
        camRig.SnapBehind(0f);

        SyncQuestWorld();
        Loading = false;
        GameInput.Blocked = dialogue.IsOpen || Paused;
        Toast("Welcome to Brindlewood. Speak with Elder Mira in front of the Elder's Hall.");
        Save();
    }

    void ClearActors()
    {
        if (dialogue != null && dialogue.IsOpen) dialogue.Close();
        foreach (var n in new List<NpcActor>(NpcActor.All)) Destroy(n.gameObject);
        foreach (var e in new List<EnemyAI>(EnemyAI.All)) Destroy(e.gameObject);
        if (wraith != null) Destroy(wraith.gameObject);
        wraith = null;
    }

    static float YawToward(Vector3 from, Vector3 to)
    {
        return Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
    }

    void SpawnNpcs()
    {
        Transform pt = player.transform;
        Vector3 c = world.VillageCenter;

        NpcActor.Create("elder_mira", "Elder Mira", "Village elder",
            "Ah, a traveler. Brindlewood has not seen many new faces lately. The northern forest has grown restless... perhaps you can help us.",
            world.HallDoor, YawToward(world.HallDoor, c),
            new CharacterModel.Look {
                tunic = new Color(0.45f, 0.30f, 0.66f), robe = true, staff = true,
                hair = CharacterModel.HairStyle.Long, hairColor = new Color(0.9f, 0.9f, 0.93f),
                accent = new Color(0.9f, 0.75f, 0.3f), skin = new Color(0.88f, 0.7f, 0.56f), scale = 0.97f }, pt);

        NpcActor.Create("kael", "Kael", "Wandering ranger",
            "Keep your voice low. I have been tracking something near the northern ruins. What do you need?",
            world.KaelSpot, YawToward(world.KaelSpot, c),
            new CharacterModel.Look {
                tunic = new Color(0.26f, 0.40f, 0.26f), pants = new Color(0.30f, 0.22f, 0.15f),
                hair = CharacterModel.HairStyle.Hood, accent = new Color(0.20f, 0.30f, 0.20f), cape = true, scale = 1.05f }, pt);

        NpcActor.Create("innkeeper_sera", "Sera", "Innkeeper",
            "Welcome, welcome! The Lantern & Loaf is warm, the stew is hot, and the rumors are practically free.",
            world.InnDoor, YawToward(world.InnDoor, c),
            new CharacterModel.Look {
                tunic = new Color(0.72f, 0.26f, 0.22f), robe = true,
                hair = CharacterModel.HairStyle.Short, hairColor = new Color(0.55f, 0.30f, 0.12f),
                accent = new Color(0.96f, 0.93f, 0.86f), scale = 0.95f }, pt);
    }

    void SpawnImps()
    {
        var rng = new System.Random(seed + 99);
        for (int i = 0; i < 10; i++)
        {
            Vector3 p;
            if (world.TryRandomLand(rng, 45f, out p)) EnemyAI.CreateImp(p);
        }
    }

    IEnumerator RespawnImp(Vector3 home, int myEpoch)
    {
        yield return new WaitForSeconds(70f);
        if (myEpoch == epoch && world != null) EnemyAI.CreateImp(home);
    }

    /// <summary>Makes the world match the quest stage (sigil visible, wraith present).</summary>
    void SyncQuestWorld()
    {
        if (world.SigilObject != null) world.SigilObject.SetActive(stage == 1);
        if (stage == 3 && !wraithDead && wraith == null)
            wraith = EnemyAI.CreateWraith(world.WraithSpawn, Mathf.Max(1, wraithHp));
        if (stage != 3 && wraith != null) { Destroy(wraith.gameObject); wraith = null; }
        if (chat != null) { chat.questStage = stage; chat.playerLevel = level; }
    }

    // ------------------------------------------------------------------- update
    void Update()
    {
        if (GameInput.EscapeDown && !Loading)
        {
            if (dialogue != null && dialogue.IsOpen) dialogue.Close();
            else SetPaused(!Paused);
        }
        if (hurtTimer > 0f) hurtTimer -= Time.unscaledDeltaTime * 2f;
        for (int i = Toasts.Count - 1; i >= 0; i--)
            if (Time.unscaledTime - Toasts[i].born > 4.5f) Toasts.RemoveAt(i);

        if (world == null || player == null || Loading) { Prompt = ""; return; }

        UpdatePrompt();
        if (!Paused && Time.unscaledTime > nextAutosave) { nextAutosave = Time.unscaledTime + 10f; Save(); }
    }

    void UpdatePrompt()
    {
        Prompt = "";
        if (Paused || PlayerDead || dialogue.IsOpen) return;
        Vector3 pp = PlayerPos;
        if (stage == 1 && world.SigilObject != null && Flat(pp - world.SigilPos).magnitude < 4.5f)
            Prompt = "PRESS E TO COLLECT THE ANCIENT SIGIL";
        else
        {
            NpcActor n = NearestNpc(4f);
            if (n != null) Prompt = "PRESS E TO TALK TO " + n.displayName.ToUpper();
            else if (stage == 3 && wraith != null && Flat(pp - wraith.transform.position).magnitude < 9f)
                Prompt = "PRESS F OR CLICK TO ATTACK THE WRAITH";
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    NpcActor NearestNpc(float range)
    {
        NpcActor best = null; float bd = range;
        for (int i = 0; i < NpcActor.All.Count; i++)
        {
            float d = Flat(NpcActor.All[i].transform.position - PlayerPos).magnitude;
            if (d < bd) { bd = d; best = NpcActor.All[i]; }
        }
        return best;
    }

    NpcActor FindNpc(string id)
    {
        for (int i = 0; i < NpcActor.All.Count; i++) if (NpcActor.All[i].npcId == id) return NpcActor.All[i];
        return null;
    }

    public void SetPaused(bool p)
    {
        Paused = p;
        Time.timeScale = p ? 0f : 1f;
        GameInput.Blocked = p || (dialogue != null && dialogue.IsOpen);
        if (p) Save();
    }

    // -------------------------------------------------------------- quest logic
    public string QuestMarker(string npcId)
    {
        if (npcId != "elder_mira") return "";
        if (stage == 0) return "!";
        if (stage == 2) return "?";
        return "";
    }

    public bool QuestTarget(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (stage == 0 || stage == 2) { var m = FindNpc("elder_mira"); if (m != null) { pos = m.transform.position; return true; } return false; }
        if (stage == 1) { pos = world.SigilPos; return true; }
        if (stage == 3 && wraith != null) { pos = wraith.transform.position; return true; }
        return false;
    }

    public void TryInteract()
    {
        if (Loading || PlayerDead || Paused || dialogue.IsOpen) return;
        Vector3 pp = PlayerPos;
        if (stage == 1 && world.SigilObject != null && Flat(pp - world.SigilPos).magnitude < 4.5f) { CollectSigil(); return; }
        NpcActor n = NearestNpc(4f);
        if (n == null) { Toast("Move closer to an NPC or quest marker."); return; }
        dialogue.Open(n, AdvanceQuestDialogue(n));
    }

    void CollectSigil()
    {
        stage = 2;
        items.Add("Ancient Sigil");
        SparkBurst.Spawn(world.SigilPos, new Color(0.4f, 0.9f, 1f), 30);
        Toast("You found the Ancient Sigil! Its blue light pulses in your hand. Return it to Elder Mira.");
        SyncQuestWorld();
        Save();
    }

    /// <summary>Applies quest transitions for a conversation and returns the opening line ("" = use greeting).</summary>
    string AdvanceQuestDialogue(NpcActor n)
    {
        if (n.npcId == "elder_mira")
        {
            if (stage == 0)
            {
                stage = 1; SyncQuestWorld(); Save();
                return "The northern beacon has awakened. Follow the road to the ruins and bring me the Ancient Sigil that rests beneath it.";
            }
            if (stage == 1) return "The Ancient Sigil lies on the altar beneath the beacon, far to the north. Follow the road and be careful in the woods.";
            if (stage == 2)
            {
                stage = 3; gold += 15; items.Add("Village Token");
                AddXp(25);
                SyncQuestWorld(); Save();
                Toast("Quest updated: +15 gold, +25 XP, Village Token received.");
                return "Thank you! Here are 15 gold, 25 XP and a Village Token. A Forest Wraith has appeared near the eastern pond. Defeat it!";
            }
            if (stage == 3) return "The wraith lurks by the eastern pond. Get close and strike with F or a click.";
            return "You saved Brindlewood. The beacon's deeper mystery awaits in the forest.";
        }
        if (n.npcId == "kael" && stage == 3)
            return "The wraith is vulnerable. Get close and strike. Press H to drink a healing potion if it hurts you.";
        if (n.npcId == "innkeeper_sera" && stage >= 1)
            return "Mira's quest has stirred the village. I have a feeling your adventure is only beginning.";
        return "";
    }

    public void OnEnemyKilled(EnemyAI e)
    {
        if (e.isBoss)
        {
            wraithDead = true; wraithHp = 0; stage = 4; wraith = null;
            gold += e.goldReward; items.Add("Wraith Essence");
            AddXp(e.xpReward);
            Toast("The Forest Wraith dissolves into silver sparks! +30 gold, +75 XP, Wraith Essence collected.");
            SyncQuestWorld();
        }
        else
        {
            gold += e.goldReward;
            AddXp(e.xpReward);
            Float(e.transform.position + Vector3.up * 2.4f, "+" + e.xpReward + " XP", new Color(0.5f, 0.85f, 1f));
            StartCoroutine(RespawnImp(e.home, epoch));
        }
        Save();
    }

    void AddXp(int n)
    {
        xp += n;
        while (xp >= XpToNext)
        {
            xp -= XpToNext;
            level++;
            hp = MaxHp;
            Toast("LEVEL UP! You are now level " + level + ". Health restored.");
        }
        if (chat != null) chat.playerLevel = level;
    }

    // ------------------------------------------------------------------- combat
    public void DamagePlayer(int dmg, string source, Vector3 from)
    {
        if (PlayerDead || Loading) return;
        hp = Mathf.Max(0, hp - dmg);
        hurtTimer = 1f;
        player.Knockback(from);
        Float(PlayerPos + Vector3.up * 2.2f, "-" + dmg, new Color(1f, 0.35f, 0.35f));
        if (hp <= 0) StartCoroutine(Defeat(source));
    }

    IEnumerator Defeat(string source)
    {
        PlayerDead = true;
        Toast("You were struck down by the " + source + "...");
        yield return new WaitForSeconds(1.6f);
        gold = Mathf.FloorToInt(gold * 0.9f);
        hp = Mathf.Max(1, MaxHp / 2);
        player.Teleport(world.PlayerSpawn);
        PlayerDead = false;
        Toast("You wake in the village, a little poorer and wiser.");
        Save();
    }

    public void RespawnAtVillage(bool announce)
    {
        player.Teleport(world.PlayerSpawn);
        if (announce) Toast("You return to the village.");
    }

    public void UsePotion()
    {
        if (Loading || PlayerDead) return;
        if (potions <= 0) { Toast("NO POTIONS LEFT"); return; }
        if (hp >= MaxHp) { Toast("HEALTH IS ALREADY FULL"); return; }
        potions--;
        int old = hp;
        hp = Mathf.Min(MaxHp, hp + 35);
        Float(PlayerPos + Vector3.up * 2.2f, "+" + (hp - old), new Color(0.5f, 1f, 0.5f));
        Toast("POTION USED · +" + (hp - old) + " HP");
    }

    // ---------------------------------------------------------------- UI helpers
    public void Toast(string msg) { Toasts.Add(new ToastItem { text = msg, born = Time.unscaledTime }); }

    public void Float(Vector3 pos, string text, Color c) { if (hud != null) hud.AddFloat(pos, text, c); }

    // ------------------------------------------------------------ save / new game
    public static int ParseSeed(string s)
    {
        s = (s ?? "").Trim();
        int v;
        if (int.TryParse(s, out v)) return Mathf.Max(1, Mathf.Abs(v == int.MinValue ? 1 : v));
        int h = 17;
        for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
        return Mathf.Max(1, Mathf.Abs(h == int.MinValue ? 1 : h));
    }

    void ResetProgress()
    {
        stage = 0; hp = 100; gold = 0; potions = 2; xp = 0; level = 1; wraithHp = 30; wraithDead = false;
        items.Clear();
        hurtTimer = 0f; PlayerDead = false;
    }

    public void NewWorld(int newSeed)
    {
        if (Loading) return;
        ResetProgress();
        PlayerPrefs.DeleteKey(SaveKey);
        if (dayNight != null) { dayNight.hour = 8.7f; dayNight.day = 1; }
        if (chat != null) chat.ResetConversation();
        SetPaused(false);
        StartCoroutine(BuildWorld(newSeed, null));
    }

    public void Save()
    {
        if (world == null || player == null || Loading) return;
        var sd = new SaveData
        {
            seed = seed, stage = stage, hp = hp, gold = gold, potions = potions, xp = xp, level = level,
            wraithHp = wraithHp, wraithDead = wraithDead, items = items.ToArray(),
            px = PlayerPos.x, py = PlayerPos.y, pz = PlayerPos.z,
            hour = dayNight != null ? dayNight.hour : 8.7f, day = dayNight != null ? dayNight.day : 1
        };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(sd));
        PlayerPrefs.Save();
    }

    SaveData LoadSave()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return null;
        try
        {
            var sd = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (sd == null || sd.seed <= 0) return null;
            stage = Mathf.Clamp(sd.stage, 0, 4); hp = Mathf.Max(1, sd.hp); gold = sd.gold; potions = sd.potions;
            xp = sd.xp; level = Mathf.Max(1, sd.level); wraithHp = sd.wraithHp > 0 ? sd.wraithHp : 30; wraithDead = sd.wraithDead;
            items = new List<string>(sd.items ?? new string[0]);
            if (dayNight != null) { dayNight.hour = sd.hour; dayNight.day = Mathf.Max(1, sd.day); }
            return sd;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Save could not be loaded: " + ex.Message);
            return null;
        }
    }

    public void QuitGame()
    {
        Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnApplicationQuit() { Save(); }
    void OnApplicationPause(bool paused) { if (paused) Save(); }

    void OnGUI()
    {
        if (!Loading) return;
        GUI.depth = -100;
        float s = UiKit.Scale;
        UiKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.05f, 0.08f, 1f));
        UiKit.Text(new Rect(0, Screen.height * 0.5f - 40f * s, Screen.width, 50f * s), "GENERATING WORLD…", Mathf.RoundToInt(32f * s), UiKit.Gold, TextAnchor.MiddleCenter, true);
        UiKit.Text(new Rect(0, Screen.height * 0.5f + 10f * s, Screen.width, 30f * s), "SEED " + seed, Mathf.RoundToInt(14f * s), new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
    }
}
