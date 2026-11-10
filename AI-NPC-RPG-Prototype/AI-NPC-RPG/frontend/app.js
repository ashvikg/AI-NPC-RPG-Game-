const API = "http://127.0.0.1:8000";
const sessionId = "player-" + Math.random().toString(36).slice(2, 10);
const npcs = {
  elder_mira: {name:"Elder Mira",role:"Village elder",initial:"M",greeting:"Ah, a traveler. Brindlewood has not seen many new faces lately. The northern forest has grown restless... perhaps you can help us."},
  kael: {name:"Kael",role:"Wandering ranger",initial:"K",greeting:"Keep your voice low. I have been tracking something near the northern ruins. What do you need?"},
  innkeeper_sera: {name:"Sera",role:"Innkeeper",initial:"S",greeting:"Welcome, welcome! The Lantern & Loaf is warm, the stew is hot, and the rumors are practically free."}
};
let currentNpc = "elder_mira", busy = false;
const $ = s => document.querySelector(s);
function addMessage(text, who="npc") {
  const div = document.createElement("div");
  div.className = "message " + (who === "player" ? "player-message" : "npc-message");
  const label = document.createElement("span"); label.className = "speaker";
  label.textContent = who === "player" ? "YOU" : npcs[currentNpc].name.toUpperCase();
  const p = document.createElement("p"); p.textContent = text;
  div.append(label,p); $("#messages").append(div); $("#messages").scrollTop = $("#messages").scrollHeight;
}
function selectNpc(id) {
  currentNpc = id; const n = npcs[id];
  document.querySelectorAll(".npc").forEach(el => el.classList.toggle("selected", el.dataset.npc === id));
  $("#npc-name").textContent = n.name; $("#npc-role").textContent = n.role; $("#portrait").textContent = n.initial;
  $("#messages").innerHTML = ""; addMessage(n.greeting);
}
document.querySelectorAll(".npc").forEach(el => el.addEventListener("click", () => selectNpc(el.dataset.npc)));
document.querySelectorAll(".suggestions button").forEach(btn => btn.addEventListener("click", () => send(btn.textContent)));
async function send(message) {
  message = message.trim(); if (!message || busy) return;
  busy = true; addMessage(message,"player"); $("#message").value = "";
  const waiting = document.createElement("div"); waiting.className = "message npc-message"; waiting.innerHTML = '<span class="speaker">THINKING...</span><p>Listening to your words...</p>'; $("#messages").append(waiting);
  try {
    const res = await fetch(API + "/api/chat", {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId,npc_id:currentNpc,message,player_name:"Traveler"})});
    if (!res.ok) throw new Error("API unavailable");
    const data = await res.json(); waiting.remove(); addMessage(data.reply);
    $("#mode").textContent = data.ai_enabled ? "AI CONNECTED" : "LOCAL MODE";
  } catch (e) {
    waiting.remove(); addMessage("I cannot reach the village network right now. Start the FastAPI backend and try again.");
  } finally { busy = false; }
}
$("#chat-form").addEventListener("submit", e => {e.preventDefault();send($("#message").value)});
$("#reset").addEventListener("click", async () => {
  try { await fetch(API+"/api/session/reset",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId})}); } catch(e) {}
  selectNpc(currentNpc);
});
(async()=>{try{const r=await fetch(API+"/health");if(!r.ok)throw 0;const d=await r.json();$("#connection").textContent="SERVER ONLINE";$("#mode").textContent=d.ai_enabled?"AI CONNECTED":"LOCAL MODE";}catch(e){$("#connection").textContent="SERVER OFFLINE";$("#connection").style.color="#e5a46b";}})();
