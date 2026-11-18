using UnityEngine;

/// <summary>Builds a low-poly humanoid from code and animates walking and sword swings.</summary>
public class CharacterModel : MonoBehaviour
{
    public enum HairStyle { Short, Hood, Long, Bald }

    public class Look
    {
        public Color tunic = new Color(0.25f, 0.45f, 0.75f);
        public Color pants = new Color(0.25f, 0.2f, 0.18f);
        public Color skin = new Color(0.92f, 0.74f, 0.58f);
        public Color hairColor = new Color(0.3f, 0.2f, 0.12f);
        public Color boots = new Color(0.2f, 0.14f, 0.1f);
        public Color accent = new Color(0.8f, 0.7f, 0.3f);
        public HairStyle hair = HairStyle.Short;
        public bool robe, cape, sword, staff;
        public float scale = 1f;
    }

    public Transform legL, legR, armL, armR;
    public float walkAmount;
    bool hasSword;
    float phase, swingT = -1f, swingDuration = 0.3f;

    public void Swing(float duration)
    {
        swingDuration = Mathf.Max(0.05f, duration);
        swingT = 0f;
    }

    public static CharacterModel Build(Transform parent, Look look)
    {
        var root = new GameObject("Model");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * look.scale;
        var cm = root.AddComponent<CharacterModel>();
        cm.hasSword = look.sword;

        // sub-meshes: 0 tunic, 1 pants, 2 skin, 3 hair, 4 boots, 5 accent, 6 eyes
        var mats = new Material[] {
            GameMaterials.Make(look.tunic), GameMaterials.Make(look.pants), GameMaterials.Make(look.skin),
            GameMaterials.Make(look.hairColor), GameMaterials.Make(look.boots), GameMaterials.Make(look.accent),
            GameMaterials.Make(new Color(0.06f, 0.06f, 0.08f)) };

        var body = new MeshBuilder(7);
        body.Box(new Vector3(0f, 1.15f, 0f), new Vector3(0.5f, 0.62f, 0.28f), 0);
        body.Box(new Vector3(0f, 0.86f, 0f), new Vector3(0.52f, 0.09f, 0.30f), 5);
        if (look.robe) body.Cylinder(new Vector3(0f, 0.12f, 0f), 0.40f, 0.27f, 0.82f, 8, 0, false);
        body.Blob(new Vector3(0f, 1.62f, 0f), new Vector3(0.2f, 0.22f, 0.2f), 5, 8, 2, null, 0f);
        body.Box(new Vector3(-0.07f, 1.64f, 0.19f), new Vector3(0.04f, 0.05f, 0.04f), 6);
        body.Box(new Vector3(0.07f, 1.64f, 0.19f), new Vector3(0.04f, 0.05f, 0.04f), 6);
        switch (look.hair)
        {
            case HairStyle.Short:
                body.Blob(new Vector3(0f, 1.70f, -0.02f), new Vector3(0.215f, 0.17f, 0.215f), 4, 8, 3, null, 0f); break;
            case HairStyle.Long:
                body.Blob(new Vector3(0f, 1.70f, -0.02f), new Vector3(0.215f, 0.17f, 0.215f), 4, 8, 3, null, 0f);
                body.Box(new Vector3(0f, 1.45f, -0.13f), new Vector3(0.34f, 0.5f, 0.1f), 3); break;
            case HairStyle.Hood:
                body.Blob(new Vector3(0f, 1.66f, -0.08f), new Vector3(0.26f, 0.27f, 0.26f), 5, 8, 5, null, 0f);
                body.Cone(new Vector3(0f, 1.85f, -0.12f), 0.16f, 0.32f, 6, 5); break;
        }
        if (look.cape) body.Box(new Vector3(0f, 1.05f, -0.17f), new Vector3(0.46f, 0.9f, 0.05f), 5);
        AddRenderer(root.transform, "Body", body.ToMesh("Body"), mats, Vector3.zero);

        // legs
        var leg = new MeshBuilder(7);
        leg.Box(new Vector3(0f, -0.4f, 0f), new Vector3(0.2f, 0.8f, 0.22f), 1);
        leg.Box(new Vector3(0f, -0.76f, 0.04f), new Vector3(0.22f, 0.18f, 0.32f), 4);
        Mesh legMesh = leg.ToMesh("Leg");
        cm.legL = AddRenderer(root.transform, "LegL", legMesh, mats, new Vector3(-0.13f, 0.85f, 0f));
        cm.legR = AddRenderer(root.transform, "LegR", legMesh, mats, new Vector3(0.13f, 0.85f, 0f));

        // arms
        var arm = new MeshBuilder(7);
        arm.Box(new Vector3(0f, -0.3f, 0f), new Vector3(0.14f, 0.62f, 0.14f), 0);
        arm.Blob(new Vector3(0f, -0.66f, 0f), new Vector3(0.08f, 0.08f, 0.08f), 3, 5, 2, null, 0f);
        Mesh armMesh = arm.ToMesh("Arm");
        cm.armL = AddRenderer(root.transform, "ArmL", armMesh, mats, new Vector3(-0.33f, 1.42f, 0f));
        cm.armR = AddRenderer(root.transform, "ArmR", armMesh, mats, new Vector3(0.33f, 1.42f, 0f));

        if (look.sword)
        {
            var sw = new MeshBuilder(2);
            sw.Box(new Vector3(0f, -0.7f, 0f), new Vector3(0.06f, 0.18f, 0.06f), 0);
            sw.Box(new Vector3(0f, -0.8f, 0f), new Vector3(0.3f, 0.05f, 0.08f), 1);
            sw.Box(new Vector3(0f, -1.32f, 0f), new Vector3(0.07f, 1.0f, 0.02f), 1);
            AddRenderer(cm.armR, "Sword", sw.ToMesh("Sword"),
                new Material[] { GameMaterials.Make(new Color(0.35f, 0.22f, 0.12f)), GameMaterials.Make(new Color(0.78f, 0.82f, 0.88f), 0.6f) }, Vector3.zero);
        }
        if (look.staff)
        {
            var st = new MeshBuilder(2);
            st.Cylinder(Vector3.zero, 0.045f, 0.04f, 1.8f, 5, 0);
            st.Blob(new Vector3(0f, 1.9f, 0f), new Vector3(0.12f, 0.12f, 0.12f), 4, 6, 1, null, 0f);
            AddRenderer(root.transform, "Staff", st.ToMesh("Staff"),
                new Material[] { GameMaterials.Make(new Color(0.38f, 0.26f, 0.15f)), GameMaterials.Glow(new Color(0.5f, 0.9f, 1f), 2.2f) },
                new Vector3(0.5f, 0f, 0.12f));
        }
        return cm;
    }

    static Transform AddRenderer(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go.transform;
    }

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }

    void Update()
    {
        if (legL == null) return;
        bool moving = walkAmount > 0.02f;
        if (moving) phase += Time.deltaTime * (5f + 6f * walkAmount);
        float s = moving ? Mathf.Sin(phase) * 38f * walkAmount : 0f;
        legL.localRotation = Quaternion.Euler(s, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-s, 0f, 0f);
        armL.localRotation = Quaternion.Euler(-s * 0.8f, 0f, 0f);

        float idleR = hasSword ? -25f : 0f;
        if (swingT >= 0f)
        {
            swingT += Time.deltaTime / swingDuration;
            float k = Mathf.Clamp01(swingT);
            float ang = k < 0.3f
                ? Mathf.Lerp(idleR, -150f, k / 0.3f)
                : Mathf.Lerp(-150f, -15f, Mathf.SmoothStep(0f, 1f, (k - 0.3f) / 0.7f));
            armR.localRotation = Quaternion.Euler(ang, 0f, 0f);
            if (swingT >= 1.25f) swingT = -1f;
        }
        else armR.localRotation = Quaternion.Euler(idleR + s * 0.8f, 0f, 0f);
    }
}
