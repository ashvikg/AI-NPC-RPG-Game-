using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds an entire playable world from a single integer seed:
/// noise terrain + painted ground texture, lakes, a village with an Elder's Hall and Inn,
/// winding roads, ancient ruins with a blue beacon, forests, rocks, bushes, flowers and clouds.
/// Everything is generated in code, so no scene setup or art assets are required.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    public const float Size = 220f;      // world is Size x Size metres, centred on (0,0)
    public const int Res = 256;          // terrain grid cells per side
    public const float WaterLevel = 0.6f;
    const float Half = Size * 0.5f;

    public int Seed { get; private set; }
    public Texture2D MapTexture { get; private set; }

    // Anchors the game logic uses to place actors and quest objects.
    public Vector3 PlayerSpawn, VillageCenter, HallDoor, InnDoor, KaelSpot, RuinsCenter, SigilPos, PondCenter, WraithSpawn;
    public GameObject SigilObject { get; private set; }
    public Material WindowMat { get; private set; }
    public Material LampMat { get; private set; }

    Transform root;
    float[] heights;
    float offX, offZ;
    Vector2 ruins2, pond2;
    readonly List<Vector2[]> pathSegs = new List<Vector2[]>();

    // shared materials
    Material[] wallMats, roofMats;
    Material woodMat, stoneMat, mossMat, darkWaterMat, grassBladeMat, cloudMat, beaconMat, glowMat;
    Material[] foliageMatsPine, foliageMatsOak, flowerMats;
    Material trunkMat, rockMat, bushMat;

    // mesh pools
    Mesh[] pineMeshes, oakMeshes, rockMeshes, bushMeshes, tuftMeshes;

    // -------------------------------------------------------------------- helpers
    static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    static float Flatten(float h, float dist, float inner, float outer, float target)
    {
        float k = 1f - Smooth(inner, outer, dist);
        return Mathf.Lerp(h, target, k);
    }

    float Perlin(float x, float z) { return Mathf.PerlinNoise(x + offX, z + offZ); }

    float Fbm(float x, float z)
    {
        float s = 0f, a = 0.5f, f = 1f, norm = 0f;
        for (int i = 0; i < 5; i++)
        {
            s += a * Perlin(x * f, z * f);
            norm += a; a *= 0.5f; f *= 2f;
        }
        return s / norm;
    }

    float PathDist(float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pathSegs.Count; i++)
        {
            Vector2 a = pathSegs[i][0], b = pathSegs[i][1];
            float abx = b.x - a.x, abz = b.y - a.y;
            float l2 = abx * abx + abz * abz;
            float t = l2 > 0f ? Mathf.Clamp01(((x - a.x) * abx + (z - a.y) * abz) / l2) : 0f;
            float dx = a.x + abx * t - x, dz = a.y + abz * t - z;
            float d2 = dx * dx + dz * dz;
            if (d2 < best) best = d2;
        }
        return Mathf.Sqrt(best);
    }

    // ------------------------------------------------------------- public queries
    public float HeightAt(float x, float z)
    {
        if (heights == null) return 0f;
        float fx = Mathf.Clamp((x + Half) / Size * Res, 0f, Res - 0.001f);
        float fz = Mathf.Clamp((z + Half) / Size * Res, 0f, Res - 0.001f);
        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        int w = Res + 1;
        float h00 = heights[iz * w + ix], h10 = heights[iz * w + ix + 1];
        float h01 = heights[(iz + 1) * w + ix], h11 = heights[(iz + 1) * w + ix + 1];
        if (tx + tz <= 1f) return h00 + (h10 - h00) * tx + (h01 - h00) * tz;
        return h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
    }

    public Vector3 GroundPoint(float x, float z) { return new Vector3(x, HeightAt(x, z), z); }

    public float Slope(float x, float z)
    {
        float dx = HeightAt(x + 1f, z) - HeightAt(x - 1f, z);
        float dz = HeightAt(x, z + 1f) - HeightAt(x, z - 1f);
        return Mathf.Sqrt(dx * dx + dz * dz) * 0.5f;
    }

    public bool IsWater(Vector3 p) { return HeightAt(p.x, p.z) < WaterLevel - 0.1f; }

    bool ValidNature(float x, float z, float villageRadius)
    {
        if (x * x + z * z < villageRadius * villageRadius) return false;
        if (PathDist(x, z) < 4.5f) return false;
        float rx = x - ruins2.x, rz = z - ruins2.y;
        if (rx * rx + rz * rz < 16f * 16f) return false;
        return true;
    }

    /// <summary>Random dry, gently sloping spot away from the village, ruins and pond.</summary>
    public bool TryRandomLand(System.Random rng, float minVillageDist, out Vector3 p)
    {
        for (int i = 0; i < 80; i++)
        {
            float x = Rand(rng, -Half + 20f, Half - 20f), z = Rand(rng, -Half + 20f, Half - 20f);
            if (!ValidNature(x, z, minVillageDist)) continue;
            float h = HeightAt(x, z);
            if (h < WaterLevel + 1f || h > 22f || Slope(x, z) > 0.5f) continue;
            float px = x - pond2.x, pz = z - pond2.y;
            if (px * px + pz * pz < 20f * 20f) continue;
            p = new Vector3(x, h, z);
            return true;
        }
        p = Vector3.zero;
        return false;
    }

    // --------------------------------------------------------------- generation
    public void Generate(int seed)
    {
        Seed = seed;
        if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
        root = new GameObject("World").transform;
        root.SetParent(transform, false);

        var rng = new System.Random(seed);
        offX = rng.Next(1000, 9000);
        offZ = rng.Next(1000, 9000);
        pathSegs.Clear();

        EnsureMaterials();
        PlanLayout(rng);
        BuildHeights();
        BuildTexture();
        BuildTerrain();
        BuildWater();
        BuildBoundary();
        BuildMeshPools(rng);
        BuildVillage(rng);
        BuildRuins(rng);
        BuildVegetation(rng);
        BuildClouds(rng);
        FindWraithSpawn();
        Physics.SyncTransforms();
    }

    void PlanLayout(System.Random rng)
    {
        VillageCenter = Vector3.zero;
        ruins2 = new Vector2(Rand(rng, -8f, 8f), Rand(rng, 58f, 68f));
        pond2 = new Vector2(Rand(rng, 42f, 52f), Rand(rng, -20f, -4f));

        AddPath(new Vector2(0f, -32f), Vector2.zero, 3, 2.5f, rng);
        AddPath(Vector2.zero, ruins2 + new Vector2(0f, -8f), 6, 5f, rng);
        AddPath(ruins2 + new Vector2(0f, -8f), ruins2, 1, 0f, rng);
        Vector2 shore = pond2 + (-pond2).normalized * 13f;
        AddPath(Vector2.zero, shore, 4, 4f, rng);
    }

    void AddPath(Vector2 a, Vector2 b, int pieces, float wiggle, System.Random rng)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 prev = a;
        for (int i = 1; i <= pieces; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / pieces);
            if (i < pieces) p += perp * Rand(rng, -wiggle, wiggle);
            pathSegs.Add(new Vector2[] { prev, p });
            prev = p;
        }
    }

    float TerrainFunc(float x, float z)
    {
        float d = Mathf.Sqrt(x * x + z * z);
        float pathK = 1f - Smooth(2.5f, 11f, PathDist(x, z));

        float n = Fbm(x * 0.008f, z * 0.008f);
        float ridge = 1f - Mathf.Abs(Fbm(x * 0.018f + 37f, z * 0.018f + 11f) * 2f - 1f);
        float amp = Mathf.Lerp(3f, 15f, Smooth(28f, 95f, d));
        float h = 4f + (n - 0.5f) * 2f * amp * (1f - 0.8f * pathK)
                  + ridge * ridge * 6f * Smooth(45f, 100f, d) * (1f - pathK);

        // mountain wall around the map edge
        h += Smooth(0.70f, 1f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / Half) * 40f;

        // keep roads above water
        h = Mathf.Lerp(h, Mathf.Max(h, 2.4f), pathK);

        h = Flatten(h, d, 22f, 40f, 3.6f);                                         // village
        h = Flatten(h, Vector2.Distance(new Vector2(x, z), ruins2), 10f, 24f, 5.2f); // ruins plateau
        float pk = 1f - Smooth(7f, 18f, Vector2.Distance(new Vector2(x, z), pond2));  // pond bowl
        h = Mathf.Lerp(h, -2.2f, pk);
        return h;
    }

    void BuildHeights()
    {
        int w = Res + 1;
        heights = new float[w * w];
        float step = Size / Res;
        for (int iz = 0; iz < w; iz++)
            for (int ix = 0; ix < w; ix++)
                heights[iz * w + ix] = TerrainFunc(-Half + ix * step, -Half + iz * step);
    }

    void BuildTexture()
    {
        const int T = 1024;
        var pix = new Color32[T * T];
        Color grassA = new Color(0.27f, 0.50f, 0.20f), grassB = new Color(0.36f, 0.58f, 0.22f);
        Color sand = new Color(0.78f, 0.72f, 0.50f), rock = new Color(0.46f, 0.45f, 0.43f);
        Color snow = new Color(0.93f, 0.95f, 0.97f), dirt = new Color(0.50f, 0.38f, 0.24f);
        Color stone = new Color(0.58f, 0.56f, 0.52f), ruinStone = new Color(0.40f, 0.42f, 0.44f);
        Color water = new Color(0.20f, 0.42f, 0.60f);

        for (int py = 0; py < T; py++)
        {
            float z = -Half + (py + 0.5f) / T * Size;
            for (int px = 0; px < T; px++)
            {
                float x = -Half + (px + 0.5f) / T * Size;
                float h = HeightAt(x, z);
                float slope = Slope(x, z);
                float coarse = Mathf.PerlinNoise(x * 0.05f + offX, z * 0.05f + offZ);
                float detail = Mathf.PerlinNoise(x * 0.6f + offX, z * 0.6f + offZ);

                Color c = Color.Lerp(grassA, grassB, coarse);
                c *= 0.9f + detail * 0.2f;
                c = Color.Lerp(c, sand, 1f - Smooth(WaterLevel + 0.3f, WaterLevel + 1.5f, h));
                c = Color.Lerp(c, rock * (0.85f + detail * 0.3f), Smooth(0.7f, 1.15f, slope));
                c = Color.Lerp(c, rock * (0.9f + detail * 0.2f), Smooth(20f, 30f, h) * 0.75f);
                c = Color.Lerp(c, snow, Smooth(36f, 46f, h));

                float pathK = 1f - Smooth(1.5f, 2.8f, PathDist(x, z));
                c = Color.Lerp(c, dirt * (0.9f + detail * 0.2f), pathK * 0.92f);

                float vd = Mathf.Sqrt(x * x + z * z);
                c = Color.Lerp(c, stone * (0.9f + detail * 0.2f), (1f - Smooth(6f, 9.5f, vd)) * 0.85f);

                float rd = Vector2.Distance(new Vector2(x, z), ruins2);
                c = Color.Lerp(c, ruinStone * (0.9f + detail * 0.2f), (1f - Smooth(5.5f, 8.5f, rd)) * 0.85f);

                if (h < WaterLevel) c = Color.Lerp(c, water, Smooth(WaterLevel, WaterLevel - 0.5f, h));
                c.a = 1f;
                pix[py * T + px] = c;
            }
        }

        if (MapTexture != null) Destroy(MapTexture);
        MapTexture = new Texture2D(T, T, TextureFormat.RGBA32, true);
        MapTexture.name = "WorldMap";
        MapTexture.wrapMode = TextureWrapMode.Clamp;
        MapTexture.filterMode = FilterMode.Bilinear;
        MapTexture.anisoLevel = 4;
        MapTexture.SetPixels32(pix);
        MapTexture.Apply(true);
    }

    void BuildTerrain()
    {
        int w = Res + 1;
        float step = Size / Res;
        var v = new Vector3[w * w];
        var uv = new Vector2[w * w];
        for (int z = 0; z < w; z++)
            for (int x = 0; x < w; x++)
            {
                v[z * w + x] = new Vector3(-Half + x * step, heights[z * w + x], -Half + z * step);
                uv[z * w + x] = new Vector2((float)x / Res, (float)z / Res);
            }
        var t = new int[Res * Res * 6];
        int k = 0;
        for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                int i00 = z * w + x, i10 = i00 + 1, i01 = i00 + w, i11 = i01 + 1;
                t[k++] = i00; t[k++] = i01; t[k++] = i10;
                t[k++] = i10; t[k++] = i01; t[k++] = i11;
            }
        var mesh = new Mesh();
        mesh.name = "Terrain";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = t;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        var go = new GameObject("Terrain");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = GameMaterials.Make(Color.white, 0.02f, MapTexture);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    void BuildWater()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Water";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0f, WaterLevel, 0f);
        go.transform.localScale = Vector3.one * (Size * 1.6f / 10f);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = GameMaterials.Make(new Color(0.12f, 0.38f, 0.56f), 0.9f);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void BuildBoundary()
    {
        var go = new GameObject("Boundary");
        go.transform.SetParent(root, false);
        float e = Half + 3f, len = Size + 20f;
        AddWall(go, new Vector3(e, 50f, 0f), new Vector3(4f, 200f, len));
        AddWall(go, new Vector3(-e, 50f, 0f), new Vector3(4f, 200f, len));
        AddWall(go, new Vector3(0f, 50f, e), new Vector3(len, 200f, 4f));
        AddWall(go, new Vector3(0f, 50f, -e), new Vector3(len, 200f, 4f));
    }

    static void AddWall(GameObject parent, Vector3 pos, Vector3 size)
    {
        var c = parent.AddComponent<BoxCollider>();
        c.center = pos; c.size = size;
    }

    // ----------------------------------------------------------------- materials
    void EnsureMaterials()
    {
        if (wallMats != null) return;
        wallMats = new Material[] {
            GameMaterials.Make(new Color(0.86f, 0.80f, 0.66f)), GameMaterials.Make(new Color(0.78f, 0.60f, 0.45f)),
            GameMaterials.Make(new Color(0.72f, 0.72f, 0.68f)), GameMaterials.Make(new Color(0.62f, 0.72f, 0.78f)) };
        roofMats = new Material[] {
            GameMaterials.Make(new Color(0.62f, 0.22f, 0.18f)), GameMaterials.Make(new Color(0.42f, 0.28f, 0.20f)),
            GameMaterials.Make(new Color(0.30f, 0.33f, 0.38f)) };
        woodMat = GameMaterials.Make(new Color(0.40f, 0.27f, 0.16f));
        stoneMat = GameMaterials.Make(new Color(0.55f, 0.55f, 0.55f));
        mossMat = GameMaterials.Make(new Color(0.42f, 0.50f, 0.40f));
        darkWaterMat = GameMaterials.Make(new Color(0.08f, 0.20f, 0.30f), 0.9f);
        WindowMat = GameMaterials.Glow(new Color(1f, 0.82f, 0.45f), 0.15f);
        LampMat = GameMaterials.Glow(new Color(1f, 0.80f, 0.40f), 0.3f);
        glowMat = GameMaterials.Glow(new Color(0.35f, 0.85f, 1f), 2.5f);
        beaconMat = GameMaterials.Glow(new Color(0.40f, 0.85f, 1f), 3f);
        grassBladeMat = GameMaterials.Make(new Color(0.30f, 0.58f, 0.22f));
        cloudMat = GameMaterials.Make(new Color(1f, 1f, 1f), 0f);
        trunkMat = GameMaterials.Make(new Color(0.35f, 0.24f, 0.14f));
        rockMat = GameMaterials.Make(new Color(0.50f, 0.50f, 0.50f));
        bushMat = GameMaterials.Make(new Color(0.20f, 0.42f, 0.18f));
        foliageMatsPine = new Material[] { trunkMat, GameMaterials.Make(new Color(0.10f, 0.32f, 0.20f)), GameMaterials.Make(new Color(0.14f, 0.38f, 0.22f)) };
        foliageMatsOak = new Material[] { trunkMat, GameMaterials.Make(new Color(0.18f, 0.46f, 0.18f)), GameMaterials.Make(new Color(0.30f, 0.54f, 0.20f)) };
        flowerMats = new Material[] {
            grassBladeMat, GameMaterials.Make(new Color(0.85f, 0.18f, 0.22f)), GameMaterials.Make(new Color(0.95f, 0.80f, 0.20f)),
            GameMaterials.Make(new Color(0.95f, 0.95f, 0.92f)), GameMaterials.Make(new Color(0.62f, 0.35f, 0.80f)) };
    }

    // --------------------------------------------------------------- mesh pools
    void BuildMeshPools(System.Random rng)
    {
        pineMeshes = new Mesh[10];
        for (int i = 0; i < pineMeshes.Length; i++)
        {
            var mb = new MeshBuilder(3);
            float h = Rand(rng, 0.9f, 1.3f);
            int sub = rng.Next(2) == 0 ? 1 : 2;
            mb.Cylinder(Vector3.zero, 0.26f, 0.18f, 1.8f * h, 6, 0);
            int tiers = 3 + rng.Next(2);
            for (int t = 0; t < tiers; t++)
                mb.Cone(new Vector3(0f, (1.0f + t * 1.0f) * h, 0f), (2.0f - t * 0.4f) * Rand(rng, 0.9f, 1.1f), 1.9f * h, 7, sub);
            pineMeshes[i] = mb.ToMesh("Pine");
        }
        oakMeshes = new Mesh[8];
        for (int i = 0; i < oakMeshes.Length; i++)
        {
            var mb = new MeshBuilder(3);
            int sub = rng.Next(2) == 0 ? 1 : 2;
            mb.Cylinder(Vector3.zero, 0.32f, 0.22f, 2.3f, 6, 0);
            mb.Blob(new Vector3(0f, 3.2f, 0f), new Vector3(1.8f, 1.5f, 1.8f), 4, 7, sub, rng, 0.18f);
            mb.Blob(new Vector3(Rand(rng, 0.7f, 1.1f), 2.7f, Rand(rng, 0.2f, 0.6f)), new Vector3(1.2f, 1.0f, 1.2f), 4, 6, sub, rng, 0.18f);
            mb.Blob(new Vector3(-Rand(rng, 0.7f, 1.1f), 2.8f, -Rand(rng, 0.2f, 0.6f)), new Vector3(1.2f, 1.0f, 1.2f), 4, 6, sub, rng, 0.18f);
            oakMeshes[i] = mb.ToMesh("Oak");
        }
        rockMeshes = new Mesh[6];
        for (int i = 0; i < rockMeshes.Length; i++)
        {
            var mb = new MeshBuilder(1);
            mb.Blob(new Vector3(0f, 0.3f, 0f), new Vector3(Rand(rng, 0.8f, 1.1f), Rand(rng, 0.55f, 0.8f), Rand(rng, 0.8f, 1.1f)), 4, 7, 0, rng, 0.28f);
            rockMeshes[i] = mb.ToMesh("Rock");
        }
        bushMeshes = new Mesh[4];
        for (int i = 0; i < bushMeshes.Length; i++)
        {
            var mb = new MeshBuilder(1);
            mb.Blob(new Vector3(0f, 0.35f, 0f), new Vector3(0.75f, 0.5f, 0.75f), 4, 6, 0, rng, 0.2f);
            mb.Blob(new Vector3(0.45f, 0.25f, 0.2f), new Vector3(0.5f, 0.35f, 0.5f), 3, 5, 0, rng, 0.2f);
            bushMeshes[i] = mb.ToMesh("Bush");
        }
        tuftMeshes = new Mesh[5];
        for (int i = 0; i < tuftMeshes.Length; i++)
        {
            var mb = new MeshBuilder(5);
            for (int b = 0; b < 5; b++)
            {
                float ang = Rand(rng, 0f, Mathf.PI * 2f), rad = Rand(rng, 0f, 0.22f);
                Vector3 bp = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                float bh = Rand(rng, 0.3f, 0.6f);
                Vector3 tip = bp + new Vector3(Rand(rng, -0.12f, 0.12f), bh, Rand(rng, -0.12f, 0.12f));
                mb.TriRaw(bp + new Vector3(-0.04f, 0f, 0f), bp + new Vector3(0.04f, 0f, 0f), tip, 0);
                mb.TriRaw(bp + new Vector3(-0.04f, 0f, 0f), tip, bp + new Vector3(0.04f, 0f, 0f), 0);
            }
            if (i > 0) mb.Blob(new Vector3(0.08f, 0.5f, 0.05f), new Vector3(0.09f, 0.07f, 0.09f), 3, 5, i, null, 0f);
            tuftMeshes[i] = mb.ToMesh("Tuft");
        }
    }

    GameObject Prop(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, float yaw, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    Transform NewGroup(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    static float FaceYaw(Vector3 from, Vector3 to) { return Mathf.Atan2(-(to.x - from.x), -(to.z - from.z)) * Mathf.Rad2Deg; }

    // ------------------------------------------------------------------ village
    GameObject House(Transform parent, Vector2 pos2, float yaw, float w, float d, float wallH, float roofH,
                     int wallIdx, int roofIdx, string name, bool sign)
    {
        var mb = new MeshBuilder(5); // 0 wall, 1 roof, 2 wood, 3 window, 4 stone
        mb.Box(new Vector3(0f, wallH * 0.5f, 0f), new Vector3(w, wallH, d), 0);
        mb.Box(new Vector3(0f, 0.1f, 0f), new Vector3(w + 0.3f, 0.6f, d + 0.3f), 4);
        float ov = 0.35f, ey = wallH - 0.05f, ry = wallH + roofH;
        Vector3 e00 = new Vector3(-w / 2 - ov, ey, -d / 2 - ov), e10 = new Vector3(w / 2 + ov, ey, -d / 2 - ov);
        Vector3 e01 = new Vector3(-w / 2 - ov, ey, d / 2 + ov), e11 = new Vector3(w / 2 + ov, ey, d / 2 + ov);
        Vector3 r0 = new Vector3(-w / 2 - ov, ry, 0f), r1 = new Vector3(w / 2 + ov, ry, 0f);
        mb.QuadBoth(e00, e10, r1, r0, 1);
        mb.QuadBoth(e01, e11, r1, r0, 1);
        Vector3 inside = new Vector3(0f, wallH * 0.6f, 0f);
        mb.Tri(new Vector3(-w / 2, wallH, -d / 2), new Vector3(-w / 2, wallH, d / 2), new Vector3(-w / 2, ry, 0f), inside, 0);
        mb.Tri(new Vector3(w / 2, wallH, -d / 2), new Vector3(w / 2, wallH, d / 2), new Vector3(w / 2, ry, 0f), inside, 0);
        mb.Box(new Vector3(0f, 0.95f, -d / 2 - 0.03f), new Vector3(1.0f, 1.9f, 0.12f), 2);
        mb.Box(new Vector3(-w * 0.28f, wallH * 0.62f, -d / 2 - 0.03f), new Vector3(0.8f, 0.7f, 0.1f), 3);
        mb.Box(new Vector3(w * 0.28f, wallH * 0.62f, -d / 2 - 0.03f), new Vector3(0.8f, 0.7f, 0.1f), 3);
        mb.Box(new Vector3(w / 2 + 0.03f, wallH * 0.62f, 0f), new Vector3(0.1f, 0.7f, 0.8f), 3);
        mb.Box(new Vector3(-w / 2 - 0.03f, wallH * 0.62f, 0f), new Vector3(0.1f, 0.7f, 0.8f), 3);
        mb.Box(new Vector3(w * 0.25f, wallH + roofH * 0.6f, 0f), new Vector3(0.55f, roofH * 1.1f, 0.55f), 4);
        if (sign)
        {
            mb.Box(new Vector3(-w * 0.32f, wallH - 0.5f, -d / 2 - 0.9f), new Vector3(1.6f, 0.6f, 0.08f), 2);
            mb.Box(new Vector3(-w * 0.32f - 0.8f, wallH - 0.2f, -d / 2 - 0.45f), new Vector3(0.08f, 1.2f, 0.9f), 2);
        }
        var mats = new Material[] { wallMats[wallIdx], roofMats[roofIdx], woodMat, WindowMat, stoneMat };
        Vector3 p = GroundPoint(pos2.x, pos2.y);
        var go = Prop(parent, name, mb.ToMesh(name), mats, p, yaw, 1f);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, wallH * 0.5f, 0f);
        col.size = new Vector3(w, wallH, d);
        return go;
    }

    void BuildVillage(System.Random rng)
    {
        var g = NewGroup("Village");

        // Elder's Hall and the Inn
        Vector2 hall2 = new Vector2(-17f, 6f), inn2 = new Vector2(17f, 9f);
        float hallYaw = FaceYaw(new Vector3(hall2.x, 0, hall2.y), Vector3.zero);
        float innYaw = FaceYaw(new Vector3(inn2.x, 0, inn2.y), Vector3.zero);
        var hall = House(g, hall2, hallYaw, 9f, 6.5f, 3.4f, 2.8f, 0, 0, "ElderHall", false);
        var inn = House(g, inn2, innYaw, 10f, 6.5f, 3.4f, 3.0f, 1, 1, "Inn", true);
        AddLabel(hall, "ELDER'S HALL", 3.4f + 2.8f + 1.2f);
        AddLabel(inn, "THE LANTERN & LOAF INN", 3.4f + 3.0f + 1.2f);

        Vector3 hallFront = Quaternion.Euler(0f, hallYaw, 0f) * Vector3.back;
        Vector3 innFront = Quaternion.Euler(0f, innYaw, 0f) * Vector3.back;
        HallDoor = hall.transform.position + hallFront * (6.5f / 2f + 2.4f);
        HallDoor.y = HeightAt(HallDoor.x, HallDoor.z);
        InnDoor = inn.transform.position + innFront * (6.5f / 2f + 2.4f);
        InnDoor.y = HeightAt(InnDoor.x, InnDoor.z);

        var placed = new List<Vector2> { hall2, inn2 };

        // Kael's campfire spot along the road to the pond
        Vector2 shore = pond2 + (-pond2).normalized * 13f;
        Vector2 dir = shore.normalized, perp = new Vector2(-dir.y, dir.x);
        Vector2 kael2 = dir * (shore.magnitude * 0.38f) + perp * 4.5f;
        Vector2 fire2 = kael2 + perp * 1.8f;
        KaelSpot = GroundPoint(kael2.x, kael2.y);
        BuildCampfire(g, fire2, rng);
        placed.Add(kael2);

        // cottages around the plaza
        int cottages = 0;
        for (int i = 0; i < 80 && cottages < 6; i++)
        {
            float ang = Rand(rng, 0f, Mathf.PI * 2f), r = Rand(rng, 13f, 21f);
            Vector2 p = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            if (PathDist(p.x, p.y) < 6.5f) continue;
            bool ok = true;
            for (int k = 0; k < placed.Count; k++) if (Vector2.Distance(placed[k], p) < 10f) { ok = false; break; }
            if (!ok) continue;
            placed.Add(p);
            float yaw = FaceYaw(new Vector3(p.x, 0, p.y), Vector3.zero) + Rand(rng, -15f, 15f);
            House(g, p, yaw, Rand(rng, 4.6f, 6.4f), Rand(rng, 4f, 5f), Rand(rng, 2.8f, 3.3f), Rand(rng, 1.8f, 2.4f),
                  rng.Next(wallMats.Length), rng.Next(roofMats.Length), "Cottage", false);
            cottages++;
        }

        BuildWell(g, new Vector2(3.8f, -4.5f));
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            BuildLamp(g, new Vector2(Mathf.Cos(a) * 9.5f, Mathf.Sin(a) * 9.5f));
        }
        BuildLamp(g, new Vector2(InnDoor.x + 2.5f, InnDoor.z));
        BuildLamp(g, new Vector2(HallDoor.x - 2.5f, HallDoor.z));
        for (int i = 0; i < 3; i++)
        {
            var b = InnDoor + new Vector3(Rand(rng, 3f, 5f), 0f, Rand(rng, -1.5f, 2f));
            BuildBarrel(g, new Vector2(b.x, b.z));
        }

        PlayerSpawn = GroundPoint(0f, -26f);
        PlayerSpawn.y += 0.2f;
    }

    void AddLabel(GameObject go, string text, float height)
    {
        var l = go.AddComponent<WorldLabel>();
        l.text = text; l.offset = new Vector3(0f, height, 0f); l.maxDistance = 50f;
        l.color = new Color(1f, 0.92f, 0.7f);
    }

    void BuildWell(Transform parent, Vector2 p2)
    {
        var mb = new MeshBuilder(4); // 0 stone, 1 water, 2 wood, 3 roof
        mb.Cylinder(Vector3.zero, 1.3f, 1.3f, 0.9f, 10, 0);
        mb.Cylinder(new Vector3(0f, 0.91f, 0f), 1.0f, 1.0f, 0.01f, 10, 1);
        mb.Box(new Vector3(-1.15f, 1.7f, 0f), new Vector3(0.14f, 1.7f, 0.14f), 2);
        mb.Box(new Vector3(1.15f, 1.7f, 0f), new Vector3(0.14f, 1.7f, 0.14f), 2);
        mb.Box(new Vector3(0f, 2.6f, 0f), new Vector3(2.6f, 0.14f, 0.2f), 2);
        mb.Cone(new Vector3(0f, 2.65f, 0f), 1.9f, 0.9f, 4, 3);
        var go = Prop(parent, "Well", mb.ToMesh("Well"), new Material[] { stoneMat, darkWaterMat, woodMat, roofMats[1] },
                      GroundPoint(p2.x, p2.y), 0f, 1f);
        var c = go.AddComponent<CapsuleCollider>();
        c.center = new Vector3(0f, 0.9f, 0f); c.radius = 1.3f; c.height = 1.8f;
    }

    void BuildLamp(Transform parent, Vector2 p2)
    {
        var mb = new MeshBuilder(2);
        mb.Cylinder(Vector3.zero, 0.09f, 0.07f, 3f, 6, 0);
        mb.Box(new Vector3(0f, 3.2f, 0f), new Vector3(0.32f, 0.38f, 0.32f), 1);
        var go = Prop(parent, "Lamp", mb.ToMesh("Lamp"), new Material[] { woodMat, LampMat }, GroundPoint(p2.x, p2.y), 0f, 1f);
        var c = go.AddComponent<CapsuleCollider>();
        c.center = new Vector3(0f, 1.5f, 0f); c.radius = 0.15f; c.height = 3f;
    }

    void BuildBarrel(Transform parent, Vector2 p2)
    {
        var mb = new MeshBuilder(1);
        mb.Cylinder(Vector3.zero, 0.42f, 0.42f, 0.95f, 7, 0);
        var go = Prop(parent, "Barrel", mb.ToMesh("Barrel"), new Material[] { woodMat }, GroundPoint(p2.x, p2.y), 0f, 1f);
        var c = go.AddComponent<BoxCollider>();
        c.center = new Vector3(0f, 0.47f, 0f); c.size = new Vector3(0.85f, 0.95f, 0.85f);
    }

    void BuildCampfire(Transform parent, Vector2 p2, System.Random rng)
    {
        var mb = new MeshBuilder(3); // 0 stone, 1 wood, 2 flame
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f;
            mb.Blob(new Vector3(Mathf.Cos(a) * 0.9f, 0.12f, Mathf.Sin(a) * 0.9f), new Vector3(0.22f, 0.16f, 0.22f), 3, 5, 0, rng, 0.2f);
        }
        mb.Box(new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.14f, 0.14f), 1);
        mb.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.14f, 0.14f, 0.9f), 1);
        mb.Cone(new Vector3(0f, 0.2f, 0f), 0.3f, 0.9f, 5, 2);
        var go = Prop(parent, "Campfire", mb.ToMesh("Campfire"),
                      new Material[] { stoneMat, woodMat, GameMaterials.Glow(new Color(1f, 0.55f, 0.15f), 2.2f) },
                      GroundPoint(p2.x, p2.y), 0f, 1f);
        var lg = new GameObject("FireLight");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var light = lg.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f, 0.6f, 0.25f); light.range = 12f; light.intensity = 1.6f;
        lg.AddComponent<LightFlicker>();
    }

    // -------------------------------------------------------------------- ruins
    void BuildRuins(System.Random rng)
    {
        var g = NewGroup("Ruins");
        RuinsCenter = GroundPoint(ruins2.x, ruins2.y);
        Vector3 c = RuinsCenter;

        // low stepped dais (walkable) ...
        var mb = new MeshBuilder(2); // 0 stone, 1 moss
        mb.Cylinder(Vector3.zero, 4.6f, 4.2f, 0.25f, 14, 0);
        mb.Cylinder(new Vector3(0f, 0.25f, 0f), 3.4f, 3.2f, 0.25f, 14, 1);
        var dm = mb.ToMesh("Dais");
        var dais = Prop(g, "Dais", dm, new Material[] { stoneMat, mossMat }, c, 0f, 1f);
        var dcol = dais.AddComponent<MeshCollider>();
        dcol.sharedMesh = dm; dcol.convex = true;

        // ... with a solid altar in the middle
        var am = new MeshBuilder(2);
        am.Box(new Vector3(0f, 1.0f, 0f), new Vector3(1.4f, 1.0f, 1.4f), 0);
        am.Box(new Vector3(0f, 1.55f, 0f), new Vector3(1.8f, 0.12f, 1.8f), 1);
        var altar = Prop(g, "Altar", am.ToMesh("Altar"), new Material[] { stoneMat, mossMat }, c, 0f, 1f);
        var acol = altar.AddComponent<BoxCollider>();
        acol.center = new Vector3(0f, 0.9f, 0f); acol.size = new Vector3(1.8f, 1.8f, 1.8f);

        int pillars = 10;
        for (int i = 0; i < pillars; i++)
        {
            float a = i / (float)pillars * Mathf.PI * 2f + Rand(rng, -0.1f, 0.1f);
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 8f, 0f, Mathf.Sin(a) * 8f);
            p.y = HeightAt(p.x, p.z);
            bool broken = rng.NextDouble() < 0.35;
            float h = broken ? Rand(rng, 1.2f, 2.4f) : Rand(rng, 3.8f, 5.8f);
            var pm = new MeshBuilder(1);
            pm.Cylinder(Vector3.zero, 0.62f, 0.5f, h, 8, 0);
            if (!broken) pm.Box(new Vector3(0f, h + 0.12f, 0f), new Vector3(1.4f, 0.25f, 1.4f), 0);
            var pg = Prop(g, "Pillar", pm.ToMesh("Pillar"), new Material[] { rng.Next(2) == 0 ? stoneMat : mossMat }, p, Rand(rng, 0f, 360f), 1f);
            var cap = pg.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, h * 0.5f, 0f); cap.radius = 0.6f; cap.height = h;
        }
        for (int i = 0; i < 16; i++)
        {
            float a = Rand(rng, 0f, Mathf.PI * 2f), r = Rand(rng, 3.5f, 11f);
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            p.y = HeightAt(p.x, p.z) - 0.05f;
            var rb = Prop(g, "Rubble", rockMeshes[rng.Next(rockMeshes.Length)], new Material[] { stoneMat }, p, Rand(rng, 0f, 360f), Rand(rng, 0.35f, 0.9f));
            rb.AddComponent<SphereCollider>().radius = 0.5f;
        }

        // the awakened blue beacon, visible from the village
        var bm = new MeshBuilder(1);
        bm.Cylinder(new Vector3(0f, 1.6f, 0f), 0.28f, 0.28f, 90f, 6, 0, false);
        var beacon = Prop(g, "Beacon", bm.ToMesh("Beacon"), new Material[] { beaconMat }, c, 0f, 1f);
        var bmr = beacon.GetComponent<MeshRenderer>();
        bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        SigilPos = c + new Vector3(0f, 2.9f, 0f);
        var sm = new MeshBuilder(1);
        sm.Cone(Vector3.zero, 0.4f, 0.65f, 6, 0, true);
        sm.Cylinder(new Vector3(0f, -0.65f, 0f), 0.001f, 0.4f, 0.65f, 6, 0, true);
        SigilObject = Prop(g, "AncientSigil", sm.ToMesh("Sigil"), new Material[] { glowMat }, SigilPos, 0f, 1f);
        SigilObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var spin = SigilObject.AddComponent<Spinner>();
        spin.baseY = SigilPos.y;
        var lg = new GameObject("SigilLight");
        lg.transform.SetParent(SigilObject.transform, false);
        var light = lg.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(0.4f, 0.85f, 1f); light.range = 14f; light.intensity = 2.5f;
        SigilObject.SetActive(false);

        var bl = new GameObject("BeaconLight");
        bl.transform.SetParent(g, false);
        bl.transform.position = c + new Vector3(0f, 4f, 0f);
        var bli = bl.AddComponent<Light>();
        bli.type = LightType.Point; bli.color = new Color(0.4f, 0.8f, 1f); bli.range = 28f; bli.intensity = 2f;

        var lab = g.gameObject.AddComponent<WorldLabel>();
        lab.text = "THE NORTHERN RUINS"; lab.offset = c + new Vector3(0f, 9f, 0f); lab.absolute = true;
        lab.maxDistance = 70f; lab.color = new Color(0.6f, 0.9f, 1f);
    }

    void FindWraithSpawn()
    {
        PondCenter = new Vector3(pond2.x, WaterLevel, pond2.y);
        Vector2 toVillage = (-pond2).normalized;
        Vector2 p = pond2;
        for (int i = 0; i < 60; i++)
        {
            p += toVillage;
            if (HeightAt(p.x, p.y) > WaterLevel + 0.9f) break;
        }
        p += toVillage * 3f;
        WraithSpawn = GroundPoint(p.x, p.y);
    }

    // --------------------------------------------------------------- vegetation
    void BuildVegetation(System.Random rng)
    {
        var treesG = NewGroup("Trees");
        var rocksG = NewGroup("Rocks");
        var bushG = NewGroup("Bushes");
        var grassG = NewGroup("Grass");

        const float cell = 3.5f;
        int cells = Mathf.CeilToInt(Size / cell);
        var occ = new bool[cells * cells];

        int trees = 0;
        for (int i = 0; i < 12000 && trees < 780; i++)
        {
            float x = Rand(rng, -Half + 6f, Half - 6f), z = Rand(rng, -Half + 6f, Half - 6f);
            if (!ValidNature(x, z, 24f)) continue;
            float h = HeightAt(x, z);
            if (h < WaterLevel + 0.8f || h > 30f || Slope(x, z) > 0.8f) continue;
            float dens = Fbm(x * 0.025f + 300f, z * 0.025f + 300f);
            if (rng.NextDouble() > Smooth(0.35f, 0.6f, dens)) continue;
            int cx = (int)((x + Half) / cell), cz = (int)((z + Half) / cell);
            if (occ[cz * cells + cx]) continue;
            occ[cz * cells + cx] = true;

            bool pine = h > 13f || rng.NextDouble() < 0.5;
            float s = Rand(rng, 0.85f, 1.5f);
            var go = Prop(treesG, pine ? "Pine" : "Oak", pine ? pineMeshes[rng.Next(pineMeshes.Length)] : oakMeshes[rng.Next(oakMeshes.Length)],
                          pine ? foliageMatsPine : foliageMatsOak, new Vector3(x, h - 0.1f, z), Rand(rng, 0f, 360f), s);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 2f, 0f); cap.radius = 0.45f; cap.height = 4f;
            trees++;
        }

        int rocks = 0;
        for (int i = 0; i < 700 && rocks < 170; i++)
        {
            float x = Rand(rng, -Half + 6f, Half - 6f), z = Rand(rng, -Half + 6f, Half - 6f);
            if (!ValidNature(x, z, 20f)) continue;
            float h = HeightAt(x, z);
            if (h < WaterLevel + 0.1f) continue;
            float s = Rand(rng, 0.8f, 2.6f) * (h > 20f ? 1.4f : 1f);
            var go = Prop(rocksG, "Rock", rockMeshes[rng.Next(rockMeshes.Length)], new Material[] { rockMat },
                          new Vector3(x, h - 0.15f, z), Rand(rng, 0f, 360f), s);
            var sc = go.AddComponent<SphereCollider>();
            sc.center = new Vector3(0f, 0.35f, 0f); sc.radius = 0.75f;
            rocks++;
        }

        int bushes = 0;
        for (int i = 0; i < 1200 && bushes < 320; i++)
        {
            float x = Rand(rng, -Half + 6f, Half - 6f), z = Rand(rng, -Half + 6f, Half - 6f);
            if (!ValidNature(x, z, 20f) || PathDist(x, z) < 6f) continue;
            float h = HeightAt(x, z);
            if (h < WaterLevel + 0.7f || h > 26f || Slope(x, z) > 0.8f) continue;
            Prop(bushG, "Bush", bushMeshes[rng.Next(bushMeshes.Length)], new Material[] { bushMat },
                 new Vector3(x, h - 0.05f, z), Rand(rng, 0f, 360f), Rand(rng, 0.8f, 1.5f));
            bushes++;
        }

        int tufts = 0;
        for (int i = 0; i < 6000 && tufts < 2200; i++)
        {
            float x = Rand(rng, -Half + 6f, Half - 6f), z = Rand(rng, -Half + 6f, Half - 6f);
            float h = HeightAt(x, z);
            if (h < WaterLevel + 0.8f || h > 22f || PathDist(x, z) < 2.4f || Slope(x, z) > 0.6f) continue;
            if (x * x + z * z < 9f * 9f) continue;
            int variant = rng.NextDouble() < 0.28 ? rng.Next(1, 5) : 0;
            Prop(grassG, "Tuft", tuftMeshes[variant], flowerMats, new Vector3(x, h - 0.02f, z), Rand(rng, 0f, 360f), Rand(rng, 0.8f, 1.5f));
            tufts++;
        }

        StaticBatchingUtility.Combine(treesG.gameObject);
        StaticBatchingUtility.Combine(rocksG.gameObject);
        StaticBatchingUtility.Combine(bushG.gameObject);
        StaticBatchingUtility.Combine(grassG.gameObject);
    }

    void BuildClouds(System.Random rng)
    {
        var g = NewGroup("Clouds");
        for (int i = 0; i < 14; i++)
        {
            var mb = new MeshBuilder(1);
            int puffs = 4 + rng.Next(3);
            for (int p = 0; p < puffs; p++)
                mb.Blob(new Vector3(Rand(rng, -14f, 14f), Rand(rng, -1.5f, 1.5f), Rand(rng, -7f, 7f)),
                        new Vector3(Rand(rng, 9f, 16f), Rand(rng, 3f, 5f), Rand(rng, 6f, 10f)), 3, 7, 0, rng, 0.1f);
            var go = Prop(g, "Cloud", mb.ToMesh("Cloud"), new Material[] { cloudMat },
                          new Vector3(Rand(rng, -260f, 260f), Rand(rng, 95f, 125f), Rand(rng, -260f, 260f)), Rand(rng, 0f, 360f), 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<CloudDrifter>().speed = Rand(rng, 1.2f, 3f);
        }
    }
}
