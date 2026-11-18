using System.Collections.Generic;
using UnityEngine;

/// <summary>Tiny low-poly mesh toolkit. Every face gets its own vertices (flat shading).
/// Faces are auto-wound so they always point away from an "inside" reference point.</summary>
public class MeshBuilder
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int>[] tris;

    public MeshBuilder(int subMeshes = 1)
    {
        tris = new List<int>[Mathf.Max(1, subMeshes)];
        for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();
    }

    public void TriRaw(Vector3 a, Vector3 b, Vector3 c, int sub = 0)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1));
        var t = tris[sub];
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, int sub = 0)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        Vector3 centroid = (a + b + c) / 3f;
        if (Vector3.Dot(n, centroid - inside) < 0f) { Vector3 t = b; b = c; c = t; }
        TriRaw(a, b, c, sub);
    }

    /// <summary>Quad given in perimeter order.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside, int sub = 0)
    {
        Tri(a, b, c, inside, sub);
        Tri(a, c, d, inside, sub);
    }

    public void QuadBoth(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int sub = 0)
    {
        TriRaw(a, b, c, sub); TriRaw(a, c, d, sub);
        TriRaw(a, c, b, sub); TriRaw(a, d, c, sub);
    }

    public void Box(Vector3 center, Vector3 size, int sub = 0)
    {
        Vector3 h = size * 0.5f;
        Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z), p100 = center + new Vector3(h.x, -h.y, -h.z);
        Vector3 p110 = center + new Vector3(h.x, h.y, -h.z), p010 = center + new Vector3(-h.x, h.y, -h.z);
        Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z), p101 = center + new Vector3(h.x, -h.y, h.z);
        Vector3 p111 = center + new Vector3(h.x, h.y, h.z), p011 = center + new Vector3(-h.x, h.y, h.z);
        Quad(p000, p100, p110, p010, center, sub);
        Quad(p001, p101, p111, p011, center, sub);
        Quad(p000, p010, p011, p001, center, sub);
        Quad(p100, p110, p111, p101, center, sub);
        Quad(p000, p100, p101, p001, center, sub);
        Quad(p010, p110, p111, p011, center, sub);
    }

    /// <summary>Vertical (tapered) cylinder. r1 ~ 0 makes a cone.</summary>
    public void Cylinder(Vector3 b, float r0, float r1, float h, int segs, int sub = 0, bool capTop = true, bool capBottom = false)
    {
        Vector3 top = b + Vector3.up * h;
        Vector3 mid = b + Vector3.up * (h * 0.5f);
        bool pointed = r1 < 0.0001f;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 p00 = b + d0 * r0, p10 = b + d1 * r0;
            Vector3 p01 = top + d0 * r1, p11 = top + d1 * r1;
            if (pointed) Tri(p00, p10, top, mid, sub);
            else
            {
                Quad(p00, p10, p11, p01, mid, sub);
                if (capTop) Tri(top, p01, p11, mid, sub);
            }
            if (capBottom) Tri(b, p00, p10, mid, sub);
        }
    }

    public void Cone(Vector3 b, float r, float h, int segs, int sub = 0, bool capBottom = true)
    {
        Cylinder(b, r, 0f, h, segs, sub, false, capBottom);
    }

    /// <summary>Lumpy ellipsoid (rocks, bushes, canopies, heads).</summary>
    public void Blob(Vector3 c, Vector3 radii, int lat, int lon, int sub, System.Random rng, float jitter)
    {
        var pts = new Vector3[(lat + 1) * lon];
        for (int i = 0; i <= lat; i++)
        {
            float v = (float)i / lat * Mathf.PI;
            for (int j = 0; j < lon; j++)
            {
                float u = (float)j / lon * Mathf.PI * 2f;
                float rj = 1f;
                if (rng != null && i != 0 && i != lat) rj += ((float)rng.NextDouble() * 2f - 1f) * jitter;
                pts[i * lon + j] = c + new Vector3(
                    Mathf.Sin(v) * Mathf.Cos(u) * radii.x,
                    Mathf.Cos(v) * radii.y,
                    Mathf.Sin(v) * Mathf.Sin(u) * radii.z) * rj;
            }
        }
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int j2 = (j + 1) % lon;
                Quad(pts[i * lon + j], pts[i * lon + j2], pts[(i + 1) * lon + j2], pts[(i + 1) * lon + j], c, sub);
            }
    }

    public Mesh ToMesh(string name = "Procedural")
    {
        var m = new Mesh();
        m.name = name;
        m.indexFormat = verts.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.subMeshCount = tris.Length;
        for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i, false);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
