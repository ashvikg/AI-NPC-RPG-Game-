
const API = "http://127.0.0.1:8000";
const sessionId = "player-" + Math.random().toString(36).slice(2, 10);
const npcs = {
  elder_mira: {name:"Elder Mira",role:"Village elder",initial:"M",greeting:"Ah, a traveler. Brindlewood has not seen many new faces lately. The northern forest has grown restless... perhaps you can help us."},
  kael: {name:"Kael",role:"Wandering ranger",initial:"K",greeting:"Keep your voice low. I have been tracking something near the northern ruins. What do you need?"},
  innkeeper_sera: {name:"Sera",role:"Innkeeper",initial:"S",greeting:"Welcome, welcome! The Lantern & Loaf is warm, the stew is hot, and the rumors are practically free."}
};
let currentNpc = "elder_mira", busy = false;
const $ = s => document.querySelector(s);
const player = $("#player"), world = $("#world");
const keys = new Set();
let playerPos = {x:49, y:78}, lastFrame = 0, questStage = 0;
// Quest stages: 0 not accepted, 1 find sigil, 2 return to Mira, 3 complete.
const quest = {
  title: "The Sleeping Beacon",
  get objective() {
    return ["Speak with Elder Mira to learn what troubles Brindlewood.",
      "Find the glowing Ancient Sigil in the northern part of the village. Walk close and press E.",
      "Return to Elder Mira and press E to report your discovery.",
      "Quest complete! The village is one step closer to understanding the beacon."][questStage];
  }
};
function updateQuestUI() {
  $("#quest-title").textContent = quest.title;
  $("#quest-objective").textContent = quest.objective;
  $("#quest-status").textContent = ["NOT STARTED","IN PROGRESS","RETURN TO MIRA","COMPLETED"][questStage];
  $("#quest-progress").style.width = ["0%","35%","75%","100%"][questStage];
  $("#quest-shrine").classList.toggle("visible", questStage === 1);
  $("#map-hint").textContent = ["FIND ELDER MIRA TO BEGIN YOUR QUEST","FIND THE ANCIENT SIGIL · PRESS E","RETURN TO ELDER MIRA · PRESS E","QUEST COMPLETE · EXPLORE BRINDLEWOOD"][questStage];
}
function addMessage(text, who="npc") {
  const div = document.createElement("div");
  div.className = "message " + (who === "player" ? "player-message" : "npc-message");
  const label = document.createElement("span"); label.className = "speaker";
  label.textContent = who === "player" ? "YOU" : npcs[currentNpc].name.toUpperCase();
  const p = document.createElement("p"); p.textContent = text;
  div.append(label,p); $("#messages").append(div); $("#messages").scrollTop = $("#messages").scrollHeight;
}
function selectNpc(id, showGreeting=true) {
  currentNpc = id; const n = npcs[id];
  document.querySelectorAll(".npc").forEach(el => el.classList.toggle("selected", el.dataset.npc === id));
  $("#npc-name").textContent = n.name; $("#npc-role").textContent = n.role; $("#portrait").textContent = n.initial;
  if (showGreeting) { $("#messages").innerHTML = ""; addMessage(n.greeting); }
}
function distanceTo(x,y) {
  const rect = world.getBoundingClientRect();
  const px = rect.width * playerPos.x / 100, py = rect.height * playerPos.y / 100;
  return Math.hypot(px - rect.width*x/100, py - rect.height*y/100);
}
function npcPosition(el) {
  return {x:parseFloat(el.style.getPropertyValue("--x")), y:parseFloat(el.style.getPropertyValue("--y"))};
}
function nearestNpc() {
  let nearest=null, best=Infinity;
  document.querySelectorAll(".npc").forEach(el => {
    const p=npcPosition(el), d=distanceTo(p.x,p.y);
    el.classList.toggle("nearby",d<78);
    if(d<best){best=d;nearest=el;}
  });
  return best<78 ? nearest : null;
}
function setPlayerPosition() {
  player.style.left=playerPos.x+"%"; player.style.top=playerPos.y+"%";
}
function showPrompt(text) { $("#interaction-prompt").textContent=text; }
function interact() {
  const npc=nearestNpc();
  const shrine=questStage===1 && distanceTo(55,23)<75;
  if(shrine){
    questStage=2; updateQuestUI(); selectNpc("elder_mira",false);
    addMessage("You found the Ancient Sigil! Its surface is warm, and a faint blue light pulses inside it. Take it back to Elder Mira.", "npc");
    showPrompt("SIGIL FOUND · RETURN TO ELDER MIRA");
    return;
  }
  if(npc){
    selectNpc(npc.dataset.npc,false);
    if(npc.dataset.npc==="elder_mira" && questStage===0){
      questStage=1; updateQuestUI();
      addMessage("The northern beacon has awakened. Please find the Ancient Sigil near the old shrine to the north, then bring it back to me.", "npc");
    } else if(npc.dataset.npc==="elder_mira" && questStage===2){
      questStage=3; updateQuestUI();
      addMessage("You found it! The sigil matches the mark in our oldest records. You have done Brindlewood a great service. Take this village token as a sign of our gratitude.", "npc");
    } else if(npc.dataset.npc==="elder_mira" && questStage===3){
      addMessage("The sigil is safe. Thank you again, traveler. The beacon's mystery is not solved, but now we have a place to begin.", "npc");
    } else {
      addMessage(npc.dataset.npc==="kael" ? "If Mira has sent you north, watch for a strange blue glimmer near the old shrine." : "I heard Mira needs someone to investigate the northern shrine. Sounds like quite the story!", "npc");
    }
  } else {
    showPrompt("MOVE CLOSER TO A CHARACTER OR QUEST MARKER");
  }
}
function gameLoop(timestamp) {
  const dt=Math.min((timestamp-lastFrame)/1000 || 0,0.05); lastFrame=timestamp;
  let dx=0,dy=0;
  if(keys.has("a")||keys.has("arrowleft"))dx--;
  if(keys.has("d")||keys.has("arrowright"))dx++;
  if(keys.has("w")||keys.has("arrowup"))dy--;
  if(keys.has("s")||keys.has("arrowdown"))dy++;
  if(dx||dy){
    const length=Math.hypot(dx,dy); dx/=length; dy/=length;
    const rect=world.getBoundingClientRect();
    playerPos.x=Math.max(4,Math.min(96,playerPos.x+dx*190*dt/rect.width*100));
    playerPos.y=Math.max(9,Math.min(91,playerPos.y+dy*190*dt/rect.height*100));
    setPlayerPosition();
  }
  const near=nearestNpc();
  const nearShrine=questStage===1 && distanceTo(55,23)<75;
  if(nearShrine) showPrompt("PRESS E TO COLLECT THE ANCIENT SIGIL");
  else if(near) showPrompt("PRESS E TO TALK TO "+npcs[near.dataset.npc].name.toUpperCase());
  else showPrompt("WASD / ARROWS TO MOVE · E TO INTERACT");
  requestAnimationFrame(gameLoop);
}
document.querySelectorAll(".npc").forEach(el => el.addEventListener("click", () => selectNpc(el.dataset.npc)));
document.querySelectorAll(".suggestions button").forEach(btn => btn.addEventListener("click", () => send(btn.textContent)));
async function send(message) {
  message=message.trim(); if(!message||busy)return;
  busy=true; addMessage(message,"player"); $("#message").value="";
  const waiting=document.createElement("div"); waiting.className="message npc-message";
  const label=document.createElement("span");label.className="speaker";label.textContent="THINKING...";
  const p=document.createElement("p");p.textContent="Listening to your words...";
  waiting.append(label,p);$("#messages").append(waiting);
  try{
    const res=await fetch(API+"/api/chat",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId,npc_id:currentNpc,message,player_name:"Traveler"})});
    if(!res.ok)throw new Error("API unavailable");
    const data=await res.json();waiting.remove();addMessage(data.reply);
    $("#mode").textContent=data.ai_enabled?"AI CONNECTED":"LOCAL MODE";
  }catch(e){waiting.remove();addMessage("I cannot reach the village network right now. Start the FastAPI backend and try again.");}
  finally{busy=false;}
}
$("#chat-form").addEventListener("submit",e=>{e.preventDefault();send($("#message").value);});
$("#reset").addEventListener("click",async()=>{
  try{await fetch(API+"/api/session/reset",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId})});}catch(e){}
  selectNpc(currentNpc);questStage=0;updateQuestUI();playerPos={x:49,y:78};setPlayerPosition();
});
window.addEventListener("keydown",e=>{
  if(["INPUT","TEXTAREA"].includes(document.activeElement.tagName))return;
  const key=e.key.toLowerCase();
  if(["w","a","s","d","arrowup","arrowdown","arrowleft","arrowright"," "].includes(key))e.preventDefault();
  if(key==="e"&&!e.repeat)interact();
  if(["w","a","s","d","arrowup","arrowdown","arrowleft","arrowright"].includes(key))keys.add(key);
});
window.addEventListener("keyup",e=>keys.delete(e.key.toLowerCase()));
window.addEventListener("blur",()=>keys.clear());
updateQuestUI();setPlayerPosition();requestAnimationFrame(gameLoop);
(async()=>{try{const r=await fetch(API+"/health");if(!r.ok)throw 0;const d=await r.json();$("#connection").textContent="SERVER ONLINE";$("#mode").textContent=d.ai_enabled?"AI CONNECTED":"LOCAL MODE";}catch(e){$("#connection").textContent="SERVER OFFLINE";$("#connection").style.color="#e5a46b";}})();
