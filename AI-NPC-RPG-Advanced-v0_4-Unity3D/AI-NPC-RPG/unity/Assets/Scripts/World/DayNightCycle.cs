using UnityEngine;

/// <summary>Sun + moon, sky colour, fog, stars and glowing windows/lamps at night.</summary>
public class DayNightCycle : MonoBehaviour
{
    public float dayLengthSeconds = 480f;
    public float hour = 8.7f;          // 08:42 like the original prototype
    public int day = 1;
    public Material windowMat, lampMat;

    public float NightAmount { get; private set; }

    Light sun, moon;
    Camera cam;
    Transform stars;

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    public void Init(Camera camera, Light sunLight, Light moonLight)
    {
        cam = camera; sun = sunLight; moon = moonLight;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.farClipPlane = 700f;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 320f;
        BuildStars();
        Apply();
    }

    void BuildStars()
    {
        var rng = new System.Random(77);
        var mb = new MeshBuilder(1);
        for (int i = 0; i < 380; i++)
        {
            Vector3 dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.9f + 0.05f, (float)rng.NextDouble() * 2f - 1f).normalized;
            Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, dir).normalized;
            float s = 0.6f + (float)rng.NextDouble() * 1.1f;
            Vector3 c = dir * 520f;
            mb.QuadBoth(c - right * s - up * s, c + right * s - up * s, c + right * s + up * s, c - right * s + up * s, 0);
        }
        var go = new GameObject("Stars");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Stars");
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = GameMaterials.MakeUnlit(Color.white);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        stars = go.transform;
    }

    void Update()
    {
        hour += Time.deltaTime * 24f / dayLengthSeconds;
        if (hour >= 24f) { hour -= 24f; day++; }
        Apply();
    }

    void LateUpdate()
    {
        if (stars != null && cam != null) stars.position = cam.transform.position;
    }

    public string ClockText
    {
        get
        {
            int h = Mathf.FloorToInt(hour), m = Mathf.FloorToInt((hour - h) * 60f);
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            return "DAY " + day.ToString("00") + " · " + h12.ToString("00") + ":" + m.ToString("00") + (h < 12 ? " AM" : " PM");
        }
    }

    void Apply()
    {
        if (sun == null || cam == null) return;
        float elev = Mathf.Sin((hour - 6f) / 24f * 2f * Mathf.PI);   // 0 at 06:00/18:00, 1 at noon, -1 at midnight
        float dayAmt = Smooth(-0.12f, 0.28f, elev);
        NightAmount = 1f - dayAmt;

        float angle = hour / 24f * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(angle, 160f, 0f);
        sun.intensity = Mathf.Lerp(0f, 1.15f, dayAmt);
        sun.color = Color.Lerp(new Color(1f, 0.55f, 0.30f), new Color(1f, 0.96f, 0.88f), Smooth(0.05f, 0.5f, elev));
        sun.enabled = dayAmt > 0.01f;

        moon.transform.rotation = Quaternion.Euler(angle + 180f, 160f, 0f);
        moon.intensity = Mathf.Lerp(0.4f, 0f, dayAmt);
        moon.color = new Color(0.55f, 0.65f, 1f);
        moon.enabled = dayAmt < 0.99f;

        RenderSettings.ambientLight = Color.Lerp(new Color(0.10f, 0.12f, 0.22f), new Color(0.52f, 0.58f, 0.66f), dayAmt);

        Color sky = Color.Lerp(new Color(0.02f, 0.03f, 0.08f), new Color(0.47f, 0.70f, 0.96f), dayAmt);
        float twilight = Mathf.Clamp01(1f - Mathf.Abs(elev) / 0.22f);
        sky = Color.Lerp(sky, new Color(0.96f, 0.55f, 0.36f), twilight * 0.65f);
        cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;

        if (stars != null) stars.gameObject.SetActive(NightAmount > 0.35f);

        float glow = Mathf.Lerp(0.15f, 2.4f, NightAmount);
        GameMaterials.SetEmission(windowMat, new Color(1f, 0.82f, 0.45f) * glow);
        GameMaterials.SetEmission(lampMat, new Color(1f, 0.80f, 0.40f) * glow);
    }
}
