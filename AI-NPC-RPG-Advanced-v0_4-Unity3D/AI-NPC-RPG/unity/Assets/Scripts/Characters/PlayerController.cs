using System.Collections;
using UnityEngine;

/// <summary>WASD movement relative to the camera, sprint, jump, sword attack, interact and heal.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f, sprintMultiplier = 1.65f, jumpSpeed = 7.5f, gravity = -24f;
    public float attackRange = 3.1f, attackCooldown = 0.55f;

    CharacterController cc;
    CharacterModel model;
    ThirdPersonCamera cam;
    float vy, nextAttack;
    Vector3 knock;

    public static GameObject Create(ThirdPersonCamera camera)
    {
        var go = new GameObject("Player");
        go.layer = 2; // "Ignore Raycast": the camera never collides with the player
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.45f; cc.slopeLimit = 55f; cc.skinWidth = 0.05f;
        var pc = go.AddComponent<PlayerController>();
        pc.cam = camera;
        var look = new CharacterModel.Look
        {
            tunic = new Color(0.22f, 0.42f, 0.74f), pants = new Color(0.28f, 0.22f, 0.18f),
            hair = CharacterModel.HairStyle.Short, hairColor = new Color(0.32f, 0.2f, 0.1f),
            accent = new Color(0.85f, 0.7f, 0.25f), sword = true, cape = true
        };
        pc.model = CharacterModel.Build(go.transform, look);
        CharacterModel.SetLayerRecursive(go, 2);
        return go;
    }

    void Awake() { cc = GetComponent<CharacterController>(); }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        vy = 0f; knock = Vector3.zero;
        cc.enabled = true;
    }

    public void Knockback(Vector3 from)
    {
        Vector3 d = transform.position - from; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = -transform.forward;
        knock = d.normalized * 6f;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.Paused) return;
        float dt = Time.deltaTime;

        Vector2 mv = GameInput.Move;
        float yaw = cam != null ? cam.Yaw : 0f;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 dir = yawRot * Vector3.forward * mv.y + yawRot * Vector3.right * mv.x;
        bool moving = dir.sqrMagnitude > 0.001f && !gm.PlayerDead;
        bool inWater = transform.position.y < WorldGenerator.WaterLevel - 0.35f;
        bool sprint = GameInput.Sprint && !inWater;
        float speed = walkSpeed * (sprint ? sprintMultiplier : 1f) * (inWater ? 0.55f : 1f);

        if (moving)
        {
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-14f * dt));
        }

        if (cc.isGrounded)
        {
            if (vy < 0f) vy = -2f;
            if (GameInput.JumpDown && !inWater) vy = jumpSpeed;
        }
        else vy += gravity * dt;

        Vector3 velocity = (moving ? dir.normalized * speed : Vector3.zero) + Vector3.up * vy + knock;
        cc.Move(velocity * dt);
        knock = Vector3.MoveTowards(knock, Vector3.zero, 25f * dt);

        float targetWalk = moving ? (sprint ? 1f : 0.7f) : 0f;
        model.walkAmount = Mathf.Lerp(model.walkAmount, targetWalk, 1f - Mathf.Exp(-10f * dt));

        if (GameInput.InteractDown) gm.TryInteract();
        if (GameInput.HealDown) gm.UsePotion();
        if (GameInput.AttackDown && Time.time >= nextAttack && !gm.PlayerDead) Attack();

        if (transform.position.y < -40f) gm.RespawnAtVillage(false);
    }

    void Attack()
    {
        nextAttack = Time.time + attackCooldown;
        // snap to the closest enemy so swings land reliably
        EnemyAI nearest = null; float best = 4f;
        for (int i = 0; i < EnemyAI.All.Count; i++)
        {
            var e = EnemyAI.All[i];
            float d = FlatDistance(e.transform.position);
            if (d < best) { best = d; nearest = e; }
        }
        if (nearest != null)
        {
            Vector3 to = nearest.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
        model.Swing(0.3f);
        StartCoroutine(ApplyHit(0.13f));
    }

    IEnumerator ApplyHit(float delay)
    {
        yield return new WaitForSeconds(delay);
        var gm = GameManager.I;
        if (gm == null) yield break;
        // iterate over a copy: enemies may be removed while we hit them
        var hits = new System.Collections.Generic.List<EnemyAI>();
        for (int i = 0; i < EnemyAI.All.Count; i++)
        {
            var e = EnemyAI.All[i];
            Vector3 to = e.transform.position - transform.position; to.y = 0f;
            if (to.magnitude > attackRange + e.bodyRadius) continue;
            if (Vector3.Angle(transform.forward, to) > 80f) continue;
            hits.Add(e);
        }
        for (int i = 0; i < hits.Count; i++) hits[i].TakeHit(gm.PlayerDamage, transform.position);
    }

    float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        return d.magnitude;
    }
}
