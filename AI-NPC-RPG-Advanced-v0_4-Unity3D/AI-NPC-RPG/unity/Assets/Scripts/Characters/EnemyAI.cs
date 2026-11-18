using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Chase / attack / return enemy. Used for the quest Forest Wraith and ambient Forest Imps.</summary>
public class EnemyAI : MonoBehaviour
{
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    public string displayName = "Enemy";
    public int maxHp = 20, hp = 20, damage = 5, xpReward = 10, goldReward = 2;
    public float moveSpeed = 3f, aggroRange = 12f, attackRange = 1.9f, attackCooldown = 1.4f, leash = 40f, bodyRadius = 0.5f;
    public bool isBoss;
    public Vector3 home;
    public float headHeight = 2.2f;
    public bool Dead { get { return hp <= 0; } }

    enum State { Idle, Chase, Return }
    State state = State.Idle;
    CharacterController cc;
    Transform visual;
    float nextAttack, wanderTimer, flash, bob;
    Vector3 wander, knock;
    bool provoked;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // ------------------------------------------------------------------ factories
    public static EnemyAI CreateWraith(Vector3 pos, int hp)
    {
        var mb = new MeshBuilder(2); // 0 cloak, 1 glow
        mb.Cylinder(new Vector3(0f, 0.35f, 0f), 0.85f, 0.28f, 1.7f, 8, 0, true);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            mb.Cylinder(new Vector3(Mathf.Cos(a) * 0.7f, 0.05f, Mathf.Sin(a) * 0.7f), 0.001f, 0.18f, 0.4f, 4, 0, false);
        }
        mb.Blob(new Vector3(0f, 2.2f, 0f), new Vector3(0.38f, 0.42f, 0.38f), 5, 8, 0, null, 0f);
        mb.Box(new Vector3(-0.12f, 2.2f, 0.33f), new Vector3(0.1f, 0.06f, 0.05f), 1);
        mb.Box(new Vector3(0.12f, 2.2f, 0.33f), new Vector3(0.1f, 0.06f, 0.05f), 1);
        mb.Box(new Vector3(-0.55f, 1.5f, 0.35f), new Vector3(0.12f, 0.12f, 0.7f), 0);
        mb.Box(new Vector3(0.55f, 1.5f, 0.35f), new Vector3(0.12f, 0.12f, 0.7f), 0);
        var e = Build("Forest Wraith", mb.ToMesh("Wraith"),
            new Material[] { GameMaterials.Make(new Color(0.18f, 0.12f, 0.28f), 0.3f), GameMaterials.Glow(new Color(0.4f, 1f, 1f), 3f) },
            pos, 2.2f, 0.55f);
        e.maxHp = 30; e.hp = hp; e.damage = 8; e.moveSpeed = 3.3f; e.aggroRange = 16f; e.attackRange = 2.2f;
        e.attackCooldown = 1.6f; e.xpReward = 75; e.goldReward = 30; e.isBoss = true; e.bodyRadius = 0.7f; e.headHeight = 2.9f;
        e.leash = 60f;
        return e;
    }

    public static EnemyAI CreateImp(Vector3 pos)
    {
        var mb = new MeshBuilder(3); // 0 body, 1 glow, 2 horn
        mb.Blob(new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 0.38f, 0.35f), 4, 7, 0, null, 0f);
        mb.Blob(new Vector3(0f, 1.1f, 0.05f), new Vector3(0.28f, 0.26f, 0.26f), 4, 7, 0, null, 0f);
        mb.Cone(new Vector3(-0.15f, 1.28f, 0f), 0.07f, 0.25f, 4, 2);
        mb.Cone(new Vector3(0.15f, 1.28f, 0f), 0.07f, 0.25f, 4, 2);
        mb.Box(new Vector3(-0.1f, 1.12f, 0.24f), new Vector3(0.07f, 0.05f, 0.04f), 1);
        mb.Box(new Vector3(0.1f, 1.12f, 0.24f), new Vector3(0.07f, 0.05f, 0.04f), 1);
        mb.Box(new Vector3(-0.15f, 0.15f, 0f), new Vector3(0.14f, 0.3f, 0.14f), 0);
        mb.Box(new Vector3(0.15f, 0.15f, 0f), new Vector3(0.14f, 0.3f, 0.14f), 0);
        mb.Box(new Vector3(-0.45f, 0.65f, 0.1f), new Vector3(0.1f, 0.4f, 0.1f), 0);
        mb.Box(new Vector3(0.45f, 0.65f, 0.1f), new Vector3(0.1f, 0.4f, 0.1f), 0);
        var e = Build("Forest Imp", mb.ToMesh("Imp"),
            new Material[] { GameMaterials.Make(new Color(0.55f, 0.18f, 0.14f)), GameMaterials.Glow(new Color(1f, 0.85f, 0.2f), 3f),
                             GameMaterials.Make(new Color(0.2f, 0.1f, 0.08f)) },
            pos, 1.4f, 0.4f);
        e.maxHp = 14; e.hp = 14; e.damage = 4; e.moveSpeed = 3.6f; e.aggroRange = 11f; e.attackRange = 1.6f;
        e.attackCooldown = 1.2f; e.xpReward = 12; e.goldReward = 3; e.bodyRadius = 0.45f; e.headHeight = 1.8f;
        e.visual.localScale = Vector3.one * 1.1f;
        return e;
    }

    static EnemyAI Build(string name, Mesh mesh, Material[] mats, Vector3 pos, float height, float radius)
    {
        var go = new GameObject(name);
        go.layer = 2;
        go.transform.position = pos;
        var cc = go.AddComponent<CharacterController>();
        cc.height = height; cc.radius = radius; cc.center = new Vector3(0f, height * 0.5f, 0f);
        cc.stepOffset = 0.4f; cc.slopeLimit = 55f;
        var v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        v.AddComponent<MeshFilter>().sharedMesh = mesh;
        v.AddComponent<MeshRenderer>().sharedMaterials = mats;
        v.layer = 2;
        var e = go.AddComponent<EnemyAI>();
        e.displayName = name; e.cc = cc; e.visual = v.transform; e.home = pos;
        e.bob = Random.value * 6f;
        return e;
    }

    // ---------------------------------------------------------------------- AI
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.Paused || Dead) return;
        float dt = Time.deltaTime;

        Vector3 to = Flat(gm.PlayerPos - transform.position);
        float d = to.magnitude;
        float homeDist = Flat(transform.position - home).magnitude;
        float aggro = aggroRange * (1f + 0.5f * gm.Night);
        bool alive = !gm.PlayerDead;

        if (state == State.Chase && (!alive || homeDist > leash || d > aggro * 2.2f)) { state = State.Return; provoked = false; }
        else if (state != State.Chase && alive && homeDist < leash && (d < aggro || provoked)) state = State.Chase;
        if (state == State.Return && homeDist < 2f) state = State.Idle;

        Vector3 vel = Vector3.zero, face = Vector3.zero;
        if (state == State.Chase)
        {
            face = to;
            if (d > attackRange * 0.85f) vel = to.normalized * moveSpeed;
            else TryAttack();
        }
        else if (state == State.Return)
        {
            Vector3 back = Flat(home - transform.position);
            vel = back.normalized * moveSpeed * 0.8f; face = back;
        }
        else
        {
            wanderTimer -= dt;
            if (wanderTimer <= 0f)
            {
                wanderTimer = Random.Range(2f, 5f);
                wander = Random.value < 0.5f ? transform.position
                       : home + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            }
            Vector3 w = Flat(wander - transform.position);
            if (w.magnitude > 0.7f) { vel = w.normalized * moveSpeed * 0.4f; face = w; }
        }

        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face.normalized), 1f - Mathf.Exp(-9f * dt));
        cc.SimpleMove(vel + knock);
        knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);

        flash = Mathf.MoveTowards(flash, 0f, 4f * dt);
        float baseScale = displayName == "Forest Imp" ? 1.1f : 1f;
        visual.localScale = Vector3.one * baseScale * (1f + flash * 0.25f);
        float hover = isBoss ? 0.3f + Mathf.Sin(Time.time * 2f + bob) * 0.15f : 0f;
        visual.localPosition = new Vector3(0f, hover, 0f);
    }

    void TryAttack()
    {
        if (Time.time < nextAttack) return;
        nextAttack = Time.time + attackCooldown;
        StartCoroutine(Strike());
    }

    IEnumerator Strike()
    {
        flash = 1f;
        yield return new WaitForSeconds(0.35f);
        var gm = GameManager.I;
        if (gm == null || Dead) yield break;
        if (Flat(gm.PlayerPos - transform.position).magnitude <= attackRange + 0.6f)
            gm.DamagePlayer(damage, displayName, transform.position);
    }

    public void TakeHit(int dmg, Vector3 from)
    {
        if (Dead) return;
        hp = Mathf.Max(0, hp - dmg);
        provoked = true; state = State.Chase;
        Vector3 k = Flat(transform.position - from);
        knock = (k.sqrMagnitude > 0.01f ? k.normalized : -transform.forward) * 7f;
        flash = 1f;
        var gm = GameManager.I;
        Vector3 hitPos = transform.position + Vector3.up * (headHeight * 0.6f);
        SparkBurst.Spawn(hitPos, isBoss ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.6f, 0.2f), 8);
        if (gm != null)
        {
            gm.Float(transform.position + Vector3.up * headHeight, "-" + dmg, new Color(1f, 0.85f, 0.3f));
            if (isBoss) gm.wraithHp = hp;
        }
        if (hp <= 0) StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        All.Remove(this);
        cc.enabled = false;
        SparkBurst.Spawn(transform.position + Vector3.up, isBoss ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.5f, 0.2f), 24);
        if (GameManager.I != null) GameManager.I.OnEnemyKilled(this);
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            visual.localScale = Vector3.one * Mathf.Max(0.01f, 1f - t / 0.6f);
            transform.Rotate(0f, 400f * Time.deltaTime, 0f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
