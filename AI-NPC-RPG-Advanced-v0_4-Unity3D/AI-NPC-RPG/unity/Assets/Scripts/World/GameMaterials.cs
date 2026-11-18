using UnityEngine;

/// <summary>
/// Creates materials that work in URP and the Built-in pipeline without any asset setup.
/// NOTE for standalone builds: add "Universal Render Pipeline/Lit" (or "Standard") and
/// "Sprites/Default" to Project Settings > Graphics > Always Included Shaders.
/// </summary>
public static class GameMaterials
{
    static Shader lit, unlit;

    static Shader Lit
    {
        get
        {
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Diffuse");
            if (lit == null) lit = Shader.Find("Sprites/Default");
            return lit;
        }
    }

    static Shader Unlit
    {
        get
        {
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            return unlit;
        }
    }

    public static Material Make(Color c, float smoothness = 0.05f, Texture tex = null)
    {
        var m = new Material(Lit);
        m.color = c;
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
        return m;
    }

    public static Material Glow(Color c, float intensity = 2.5f)
    {
        var m = Make(c, 0.3f);
        SetEmission(m, c * intensity);
        return m;
    }

    public static Material MakeUnlit(Color c)
    {
        var m = new Material(Unlit);
        m.color = c;
        return m;
    }

    public static void SetEmission(Material m, Color c)
    {
        if (m == null) return;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
    }
}
