using System.Collections.Generic;
using UnityEngine;

/// <summary>Small helpers for drawing the game UI with IMGUI (no packages or prefabs needed).
/// Only call from inside OnGUI.</summary>
public static class UiKit
{
    public static readonly Color Gold = new Color(0.95f, 0.8f, 0.4f);
    public static readonly Color Panel = new Color(0.05f, 0.07f, 0.1f, 0.78f);
    public static readonly Color Green = new Color(0.71f, 0.95f, 0.49f);

    static readonly Dictionary<string, GUIStyle> labelCache = new Dictionary<string, GUIStyle>();
    static readonly Dictionary<int, Texture2D> texCache = new Dictionary<int, Texture2D>();

    public static float Scale { get { return Mathf.Clamp(Screen.height / 900f, 0.75f, 2f); } }

    public static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Bar(Rect r, float value, Color fg, Color bg)
    {
        Fill(r, bg);
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), fg);
    }

    public static void Box(Rect r)
    {
        Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(1f, 1f, 1f, 0.12f));
        Fill(r, Panel);
    }

    public static Texture2D Tex(Color c)
    {
        Color32 k = c;
        int key = (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
        Texture2D t;
        if (!texCache.TryGetValue(key, out t) || t == null)
        {
            t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c); t.Apply();
            texCache[key] = t;
        }
        return t;
    }

    public static GUIStyle Label(int size, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
    {
        string key = size + "|" + anchor + "|" + bold + "|" + wrap;
        GUIStyle st;
        if (!labelCache.TryGetValue(key, out st) || st == null)
        {
            st = new GUIStyle(GUI.skin.label);
            st.fontSize = size; st.alignment = anchor; st.wordWrap = wrap;
            st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            st.richText = false;
            labelCache[key] = st;
        }
        return st;
    }

    /// <summary>Text with a soft drop shadow.</summary>
    public static void Text(Rect r, string s, int size, Color c, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false)
    {
        GUIStyle st = Label(size, anchor, bold, wrap);
        st.normal.textColor = new Color(0f, 0f, 0f, 0.85f * c.a);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), s, st);
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    public static GUIStyle Button(int size)
    {
        string key = "btn" + size;
        GUIStyle st;
        if (!labelCache.TryGetValue(key, out st) || st == null)
        {
            st = new GUIStyle(GUI.skin.button);
            st.fontSize = size; st.fontStyle = FontStyle.Bold; st.alignment = TextAnchor.MiddleCenter;
            st.normal.background = Tex(new Color(0.16f, 0.22f, 0.3f, 0.95f));
            st.hover.background = Tex(new Color(0.24f, 0.34f, 0.46f, 1f));
            st.active.background = Tex(new Color(0.3f, 0.45f, 0.3f, 1f));
            st.normal.textColor = Color.white; st.hover.textColor = Gold; st.active.textColor = Green;
            labelCache[key] = st;
        }
        return st;
    }

    public static GUIStyle Field(int size)
    {
        string key = "fld" + size;
        GUIStyle st;
        if (!labelCache.TryGetValue(key, out st) || st == null)
        {
            st = new GUIStyle(GUI.skin.textField);
            st.fontSize = size; st.alignment = TextAnchor.MiddleLeft;
            Texture2D bg = Tex(new Color(0.08f, 0.1f, 0.14f, 1f));
            st.normal.background = bg; st.focused.background = Tex(new Color(0.12f, 0.16f, 0.22f, 1f)); st.hover.background = bg;
            st.normal.textColor = Color.white; st.focused.textColor = Color.white; st.hover.textColor = Color.white;
            labelCache[key] = st;
        }
        return st;
    }
}
