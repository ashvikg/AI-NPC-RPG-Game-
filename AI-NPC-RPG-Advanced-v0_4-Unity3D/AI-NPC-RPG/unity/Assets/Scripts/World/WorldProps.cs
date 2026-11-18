using System.Collections.Generic;
using UnityEngine;

/// <summary>Floating text anchored to a world position (buildings, landmarks). Drawn by HudUI.</summary>
public class WorldLabel : MonoBehaviour
{
    public static readonly List<WorldLabel> All = new List<WorldLabel>();
    public string text;
    public Vector3 offset;
    public float maxDistance = 40f;
    public Color color = Color.white;
    public bool absolute;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    public Vector3 WorldPos { get { return absolute ? offset : transform.position + offset; } }
}

public class LightFlicker : MonoBehaviour
{
    Light l; float baseIntensity, seed;
    void Start() { l = GetComponent<Light>(); if (l != null) baseIntensity = l.intensity; seed = Random.value * 100f; }
    void Update()
    {
        if (l == null) return;
        l.intensity = baseIntensity * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 9f, seed));
    }
}

/// <summary>Rotates and bobs an object (the Ancient Sigil).</summary>
public class Spinner : MonoBehaviour
{
    public float baseY;
    public float spinSpeed = 70f;
    public float bobHeight = 0.25f;
    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 2f) * bobHeight;
        transform.position = p;
    }
}

public class CloudDrifter : MonoBehaviour
{
    public float speed = 2f;
    void Update()
    {
        Vector3 p = transform.position;
        p.x += speed * Time.deltaTime;
        if (p.x > 300f) p.x = -300f;
        transform.position = p;
    }
}

/// <summary>Short-lived burst of glowing cubes used for hits and defeats.</summary>
public class SparkBurst : MonoBehaviour
{
    static Mesh cube;
    static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();

    Transform[] bits; Vector3[] vel; float life;
    const float Lifetime = 0.8f;

    public static void Spawn(Vector3 pos, Color color, int count)
    {
        if (cube == null) { var mb = new MeshBuilder(1); mb.Box(Vector3.zero, Vector3.one * 0.14f); cube = mb.ToMesh("Spark"); }
        Color32 c32 = color;
        int key = (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
        Material m;
        if (!mats.TryGetValue(key, out m) || m == null) { m = GameMaterials.Glow(color, 2.5f); mats[key] = m; }

        var go = new GameObject("Sparks");
        go.transform.position = pos;
        var sb = go.AddComponent<SparkBurst>();
        sb.bits = new Transform[count];
        sb.vel = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var b = new GameObject("s");
            b.transform.SetParent(go.transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = cube;
            var r = b.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sb.bits[i] = b.transform;
            sb.vel[i] = Random.onUnitSphere * Random.Range(2f, 5f) + Vector3.up * 2f;
        }
    }

    void Update()
    {
        life += Time.deltaTime;
        float k = 1f - life / Lifetime;
        for (int i = 0; i < bits.Length; i++)
        {
            vel[i] += Vector3.down * 9f * Time.deltaTime;
            bits[i].position += vel[i] * Time.deltaTime;
            bits[i].localScale = Vector3.one * Mathf.Max(0.01f, k);
        }
        if (life >= Lifetime) Destroy(gameObject);
    }
}
