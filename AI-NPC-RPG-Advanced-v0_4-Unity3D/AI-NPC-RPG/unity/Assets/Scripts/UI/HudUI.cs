using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws the whole in-game HUD: health/XP, quest tracker, minimap, world labels,
/// enemy health bars, floating damage numbers, toasts and the pause menu.</summary>
public class HudUI : MonoBehaviour
{
    class FloatItem { public Vector3 pos; public string text; public Color color; public float born; }
    readonly List<FloatItem> floats = new List<FloatItem>();
    string seedInput = "";

    public void AddFloat(Vector3 pos, string text, Color c)
    {
        floats.Add(new FloatItem { pos = pos, text = text, color = c, born = Time.time });
    }

    bool ToScreen(Camera cam, Vector3 w, out Vector2 p)
    {
        Vector3 sp = cam.WorldToScreenPoint(w);
        p = new Vector2(sp.x, Screen.height - sp.y);
        return sp.z > 0.1f;
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null || gm.world == null || gm.cam == null) return;
        float s = UiKit.Scale;

        DrawWorldLabels(gm, s);
        DrawNpcMarkers(gm, s);
        DrawEnemyBars(gm, s);
        DrawFloats(gm, s);
        DrawStats(gm, s);
        DrawQuest(gm, s);
        DrawMinimap(gm, s);
        DrawPrompt(gm, s);
        DrawToasts(gm, s);

        UiKit.Text(new Rect(16f * s, Screen.height - 30f * s, 900f * s, 22f * s),
            "WASD move · SHIFT sprint · SPACE jump · E interact · F / click attack · H potion · hold RIGHT MOUSE to look · ESC menu",
            Mathf.RoundToInt(11f * s), new Color(1f, 1f, 1f, 0.55f));

        if (gm.hurtTimer > 0f)
            UiKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.8f, 0f, 0f, Mathf.Clamp01(gm.hurtTimer) * 0.35f));

        if (gm.Paused) DrawPause(gm, s);
    }

    // ------------------------------------------------------------- world-space UI
    void DrawWorldLabels(GameManager gm, float s)
    {
        Vector3 cp = gm.cam.transform.position;
        for (int i = 0; i < WorldLabel.All.Count; i++)
        {
            var l = WorldLabel.All[i];
            Vector3 wp = l.WorldPos;
            float d = Vector3.Distance(cp, wp);
            if (d > l.maxDistance) continue;
            Vector2 p;
            if (!ToScreen(gm.cam, wp, out p)) continue;
            float a = 1f - Mathf.Pow(d / l.maxDistance, 2f);
            Color c = l.color; c.a = a;
            UiKit.Text(new Rect(p.x - 150f * s, p.y - 10f * s, 300f * s, 20f * s), l.text, Mathf.RoundToInt(12f * s), c, TextAnchor.MiddleCenter, true);
        }
    }

    void DrawNpcMarkers(GameManager gm, float s)
    {
        for (int i = 0; i < NpcActor.All.Count; i++)
        {
            var n = NpcActor.All[i];
            string m = gm.QuestMarker(n.npcId);
            if (m.Length == 0) continue;
            if (Vector3.Distance(gm.cam.transform.position, n.transform.position) > 45f) continue;
            Vector2 p;
            if (!ToScreen(gm.cam, n.transform.position + Vector3.up * (2.9f + Mathf.Sin(Time.time * 3f) * 0.12f), out p)) continue;
            UiKit.Text(new Rect(p.x - 30f * s, p.y - 24f * s, 60f * s, 48f * s), m, Mathf.RoundToInt(36f * s), UiKit.Gold, TextAnchor.MiddleCenter, true);
        }
    }

    void DrawEnemyBars(GameManager gm, float s)
    {
        for (int i = 0; i < EnemyAI.All.Count; i++)
        {
            var e = EnemyAI.All[i];
            float d = Vector3.Distance(gm.cam.transform.position, e.transform.position);
            if (d > (e.isBoss ? 70f : 28f)) continue;
            Vector2 p;
            if (!ToScreen(gm.cam, e.transform.position + Vector3.up * (e.headHeight + 0.4f), out p)) continue;
            float w = (e.isBoss ? 120f : 70f) * s;
            UiKit.Text(new Rect(p.x - w, p.y - 22f * s, w * 2f, 18f * s), e.displayName.ToUpper(), Mathf.RoundToInt(11f * s),
                       e.isBoss ? new Color(0.6f, 1f, 1f) : new Color(1f, 0.7f, 0.5f), TextAnchor.MiddleCenter, true);
            UiKit.Bar(new Rect(p.x - w * 0.5f, p.y - 4f * s, w, 7f * s), e.hp / (float)e.maxHp, new Color(0.85f, 0.2f, 0.2f), new Color(0f, 0f, 0f, 0.6f));
        }
    }

    void DrawFloats(GameManager gm, float s)
    {
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            float age = Time.time - f.born;
            if (age > 1.1f) { floats.RemoveAt(i); continue; }
            Vector2 p;
            if (!ToScreen(gm.cam, f.pos + Vector3.up * age * 1.3f, out p)) continue;
            Color c = f.color; c.a = 1f - age / 1.1f;
            UiKit.Text(new Rect(p.x - 60f * s, p.y - 14f * s, 120f * s, 28f * s), f.text, Mathf.RoundToInt(20f * s), c, TextAnchor.MiddleCenter, true);
        }
    }

    // ------------------------------------------------------------------- panels
    void DrawStats(GameManager gm, float s)
    {
        Rect r = new Rect(16f * s, 16f * s, 300f * s, 124f * s);
        UiKit.Box(r);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 6f * s, 200f * s, 18f * s), "BRINDLEWOOD", Mathf.RoundToInt(12f * s), UiKit.Gold, TextAnchor.UpperLeft, true);
        Rect hb = new Rect(r.x + 12f * s, r.y + 28f * s, r.width - 24f * s, 20f * s);
        UiKit.Bar(hb, gm.hp / (float)gm.MaxHp, new Color(0.82f, 0.22f, 0.25f), new Color(0f, 0f, 0f, 0.55f));
        UiKit.Text(hb, "HP " + gm.hp + " / " + gm.MaxHp, Mathf.RoundToInt(12f * s), Color.white, TextAnchor.MiddleCenter, true);
        Rect xb = new Rect(r.x + 12f * s, r.y + 54f * s, r.width - 24f * s, 8f * s);
        UiKit.Bar(xb, gm.xp / (float)gm.XpToNext, new Color(0.45f, 0.75f, 1f), new Color(0f, 0f, 0f, 0.55f));
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 64f * s, r.width - 24f * s, 18f * s),
                   "LEVEL " + gm.level + "  ·  XP " + gm.xp + " / " + gm.XpToNext, Mathf.RoundToInt(12f * s), Color.white);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 86f * s, 140f * s, 18f * s), "GOLD  " + gm.gold, Mathf.RoundToInt(13f * s), UiKit.Gold, TextAnchor.UpperLeft, true);
        UiKit.Text(new Rect(r.x + 150f * s, r.y + 86f * s, 140f * s, 18f * s), "POTIONS  " + gm.potions, Mathf.RoundToInt(13f * s), UiKit.Green, TextAnchor.UpperLeft, true);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 104f * s, r.width - 24f * s, 18f * s),
                   "ITEMS: " + (gm.items.Count == 0 ? "none" : string.Join(", ", gm.items.ToArray())), Mathf.RoundToInt(10f * s), new Color(1f, 1f, 1f, 0.7f));
    }

    void DrawQuest(GameManager gm, float s)
    {
        Rect r = new Rect(16f * s, 148f * s, 300f * s, 104f * s);
        UiKit.Box(r);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 6f * s, r.width - 24f * s, 18f * s), "THE SLEEPING BEACON", Mathf.RoundToInt(12f * s), UiKit.Gold, TextAnchor.UpperLeft, true);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 24f * s, r.width - 24f * s, 16f * s), GameManager.StatusText[gm.stage], Mathf.RoundToInt(10f * s), UiKit.Green, TextAnchor.UpperLeft, true);
        UiKit.Text(new Rect(r.x + 12f * s, r.y + 42f * s, r.width - 24f * s, 40f * s), GameManager.Objectives[gm.stage], Mathf.RoundToInt(12f * s), Color.white, TextAnchor.UpperLeft, false, true);
        UiKit.Bar(new Rect(r.x + 12f * s, r.y + r.height - 14f * s, r.width - 24f * s, 6f * s), GameManager.Progress[gm.stage], UiKit.Gold, new Color(0f, 0f, 0f, 0.55f));
    }

    void DrawMinimap(GameManager gm, float s)
    {
        float size = 190f * s;
        Rect r = new Rect(Screen.width - size - 16f * s, 16f * s, size, size);
        UiKit.Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.05f, 0.07f, 0.1f, 0.9f));
        Texture2D tex = gm.world.MapTexture;
        Vector3 pp = gm.PlayerPos;
        const float win = 110f;
        if (tex != null)
        {
            float u0 = (pp.x - win * 0.5f + WorldGenerator.Size * 0.5f) / WorldGenerator.Size;
            float v0 = (pp.z - win * 0.5f + WorldGenerator.Size * 0.5f) / WorldGenerator.Size;
            float uw = win / WorldGenerator.Size;
            GUI.BeginGroup(r);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, r.width, r.height), tex, new Rect(u0, v0, uw, uw));

            // NPCs
            for (int i = 0; i < NpcActor.All.Count; i++) MapDot(NpcActor.All[i].transform.position, pp, win, r, 6f * s, UiKit.Gold);
            // enemies
            for (int i = 0; i < EnemyAI.All.Count; i++)
                if ((EnemyAI.All[i].transform.position - pp).sqrMagnitude < 40f * 40f) MapDot(EnemyAI.All[i].transform.position, pp, win, r, 5f * s, new Color(0.95f, 0.2f, 0.2f));
            // quest target (pulsing)
            Vector3 qt;
            if (gm.QuestTarget(out qt))
            {
                float pulse = 8f + Mathf.Sin(Time.unscaledTime * 5f) * 2.5f;
                MapDot(qt, pp, win, r, pulse * s, new Color(0.4f, 0.9f, 1f));
            }
            // player + heading tick
            Vector2 c = new Vector2(r.width * 0.5f, r.height * 0.5f);
            UiKit.Fill(new Rect(c.x - 4f * s, c.y - 4f * s, 8f * s, 8f * s), Color.white);
            Vector3 f = gm.PlayerForward;
            Vector2 tip = c + new Vector2(f.x, -f.z) * 9f * s;
            UiKit.Fill(new Rect(tip.x - 2.5f * s, tip.y - 2.5f * s, 5f * s, 5f * s), UiKit.Green);
            GUI.EndGroup();
        }
        UiKit.Text(new Rect(r.x, r.y + 1f, r.width, 16f * s), "N", Mathf.RoundToInt(11f * s), Color.white, TextAnchor.UpperCenter, true);
        UiKit.Text(new Rect(r.x, r.yMax + 6f * s, r.width, 18f * s), gm.dayNight.ClockText, Mathf.RoundToInt(12f * s), Color.white, TextAnchor.UpperRight, true);
        UiKit.Text(new Rect(r.x, r.yMax + 24f * s, r.width, 18f * s), "WORLD SEED " + gm.seed + (gm.chat != null && gm.chat.Checked ? (gm.chat.ServerOnline ? (gm.chat.AiEnabled ? " · AI" : " · LOCAL AI") : " · OFFLINE") : ""),
                   Mathf.RoundToInt(10f * s), new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperRight);
    }

    static void MapDot(Vector3 world, Vector3 center, float win, Rect r, float size, Color c)
    {
        float x = (world.x - center.x) / win + 0.5f;
        float y = 1f - ((world.z - center.z) / win + 0.5f);
        x = Mathf.Clamp(x, 0.03f, 0.97f); y = Mathf.Clamp(y, 0.03f, 0.97f);
        UiKit.Fill(new Rect(x * r.width - size * 0.5f, y * r.height - size * 0.5f, size, size), c);
    }

    void DrawPrompt(GameManager gm, float s)
    {
        if (string.IsNullOrEmpty(gm.Prompt) || gm.Paused) return;
        Rect r = new Rect(Screen.width * 0.5f - 260f * s, Screen.height - 110f * s, 520f * s, 34f * s);
        UiKit.Box(r);
        UiKit.Text(r, gm.Prompt, Mathf.RoundToInt(14f * s), Color.white, TextAnchor.MiddleCenter, true);
    }

    void DrawToasts(GameManager gm, float s)
    {
        float y = 20f * s;
        for (int i = gm.Toasts.Count - 1; i >= 0 && i >= gm.Toasts.Count - 4; i--)
        {
            var t = gm.Toasts[i];
            float age = Time.unscaledTime - t.born;
            float a = Mathf.Clamp01(Mathf.Min(1f, (4.5f - age) * 1.5f));
            Rect r = new Rect(Screen.width * 0.5f - 320f * s, y, 640f * s, 30f * s);
            UiKit.Fill(r, new Color(0.05f, 0.07f, 0.1f, 0.7f * a));
            UiKit.Text(r, t.text, Mathf.RoundToInt(14f * s), new Color(1f, 0.95f, 0.75f, a), TextAnchor.MiddleCenter, true);
            y += 34f * s;
        }
    }

    // -------------------------------------------------------------------- pause
    void DrawPause(GameManager gm, float s)
    {
        UiKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        float w = 420f * s, h = 430f * s;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        UiKit.Box(r);
        UiKit.Text(new Rect(r.x, r.y + 14f * s, r.width, 36f * s), "PAUSED", Mathf.RoundToInt(28f * s), UiKit.Gold, TextAnchor.MiddleCenter, true);
        float bx = r.x + 30f * s, bw = r.width - 60f * s, bh = 40f * s, y = r.y + 66f * s;
        var btn = UiKit.Button(Mathf.RoundToInt(14f * s));

        if (GUI.Button(new Rect(bx, y, bw, bh), "RESUME", btn)) gm.SetPaused(false);
        y += bh + 10f * s;
        if (GUI.Button(new Rect(bx, y, bw, bh), "NEW RANDOM WORLD", btn)) gm.NewWorld(Random.Range(1, 999999999));
        y += bh + 10f * s;
        if (GUI.Button(new Rect(bx, y, bw, bh), "RESTART THIS WORLD", btn)) gm.NewWorld(gm.seed);
        y += bh + 18f * s;

        UiKit.Text(new Rect(bx, y, bw, 18f * s), "WORLD SEED (number or any word)", Mathf.RoundToInt(11f * s), new Color(1f, 1f, 1f, 0.7f));
        y += 20f * s;
        seedInput = GUI.TextField(new Rect(bx, y, bw * 0.62f, 36f * s), seedInput, 24, UiKit.Field(Mathf.RoundToInt(14f * s)));
        if (GUI.Button(new Rect(bx + bw * 0.66f, y, bw * 0.34f, 36f * s), "GENERATE", btn))
        {
            if (seedInput.Trim().Length > 0) gm.NewWorld(GameManager.ParseSeed(seedInput));
        }
        y += 54f * s;
        if (GUI.Button(new Rect(bx, y, bw, bh), "SAVE & QUIT", btn)) gm.QuitGame();
        UiKit.Text(new Rect(r.x, r.yMax - 28f * s, r.width, 20f * s), "Progress autosaves. New worlds reset quest progress.",
                   Mathf.RoundToInt(10f * s), new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
    }
}
