using System.Collections.Generic;
using UnityEngine;

/// <summary>A 3D villager the player can talk to. Replaces the old NpcInteractable.</summary>
public class NpcActor : MonoBehaviour
{
    public static readonly List<NpcActor> All = new List<NpcActor>();

    public string npcId, displayName, role, greeting;
    public Transform playerTarget;

    CharacterModel model;
    float homeYaw, phase;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static NpcActor Create(string id, string name, string role, string greeting,
                                  Vector3 pos, float yaw, CharacterModel.Look look, Transform player)
    {
        var go = new GameObject("NPC_" + name);
        go.layer = 2;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f); col.radius = 0.4f; col.height = 1.8f;
        var npc = go.AddComponent<NpcActor>();
        npc.npcId = id; npc.displayName = name; npc.role = role; npc.greeting = greeting;
        npc.homeYaw = yaw; npc.playerTarget = player; npc.phase = Random.value * 6f;
        npc.model = CharacterModel.Build(go.transform, look);
        CharacterModel.SetLayerRecursive(go, 2);

        var label = go.AddComponent<WorldLabel>();
        label.text = name.ToUpper(); label.offset = new Vector3(0f, 2.35f, 0f);
        label.maxDistance = 28f; label.color = new Color(1f, 0.95f, 0.75f);
        return npc;
    }

    public string Initial { get { return string.IsNullOrEmpty(displayName) ? "?" : displayName.Substring(0, 1).ToUpper(); } }

    void Update()
    {
        if (model == null) return;
        float targetYaw = homeYaw;
        if (playerTarget != null)
        {
            Vector3 to = playerTarget.position - transform.position; to.y = 0f;
            if (to.magnitude < 7f && to.sqrMagnitude > 0.01f) targetYaw = Quaternion.LookRotation(to).eulerAngles.y;
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetYaw, 0f), 1f - Mathf.Exp(-5f * Time.deltaTime));
        model.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 1.6f + phase) * 0.012f, 0f);
    }
}
