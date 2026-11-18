const API="http://127.0.0.1:8000",SAVE_KEY="brindlewood-save-v03";
const sessionId=localStorage.getItem("brindlewood-session")||("player-"+Math.random().toString(36).slice(2,10));
localStorage.setItem("brindlewood-session",sessionId);
const npcs={
 elder_mira:{name:"Elder Mira",role:"Village elder",initial:"M",greeting:"Ah, a traveler. Brindlewood has not seen many new faces lately. The northern forest has grown restless... perhaps you can help us."},
 kael:{name:"Kael",role:"Wandering ranger",initial:"K",greeting:"Keep your voice low. I have been tracking something near the northern ruins. What do you need?"},
 innkeeper_sera:{name:"Sera",role:"Innkeeper",initial:"S",greeting:"Welcome, welcome! The Lantern & Loaf is warm, the stew is hot, and the rumors are practically free."}
};
const $=s=>document.querySelector(s),player=$("#player"),world=$("#world"),keys=new Set();
let currentNpc="elder_mira",busy=false,pos={x:49,y:78},last=0,stage=0,hp=100,gold=0,potions=2,xp=0,level=1,items=[],wraithHP=3,wraithDead=false;
const objectives=["Speak with Elder Mira to begin your adventure.","Find the glowing Ancient Sigil in the northern village and press E.","Return to Elder Mira and report your discovery.","Defeat the Forest Wraith near the eastern pond.","Quest complete! Brindlewood is safer."];
function save(){try{localStorage.setItem(SAVE_KEY,JSON.stringify({stage,hp,gold,potions,xp,level,items,wraithHP,wraithDead,pos}));}catch(e){}}
function load(){try{const d=JSON.parse(localStorage.getItem(SAVE_KEY)||"null");if(!d)return;stage=d.stage??0;hp=d.hp??100;gold=d.gold??0;potions=d.potions??2;xp=d.xp??0;level=d.level??1;items=d.items||[];wraithHP=d.wraithHP??3;wraithDead=!!d.wraithDead;pos=d.pos||pos;}catch(e){console.warn("Save could not be loaded",e);}}
function ui(){
 $("#quest-title").textContent="The Sleeping Beacon";$("#quest-objective").textContent=objectives[stage];
 $("#quest-status").textContent=["NOT STARTED","IN PROGRESS","RETURN TO MIRA","DEFEAT WRAITH","COMPLETED"][stage];
 $("#quest-progress").style.width=["0%","25%","55%","80%","100%"][stage];
 $("#quest-shrine").classList.toggle("visible",stage===1);$("#enemy").classList.toggle("visible",stage===3&&!wraithDead);
 $("#enemy-health").style.width=(wraithHP/3*100)+"%";
 $("#map-hint").textContent=["FIND ELDER MIRA TO BEGIN","FIND THE ANCIENT SIGIL · PRESS E","RETURN TO ELDER MIRA · PRESS E","DEFEAT THE FOREST WRAITH","QUEST COMPLETE · BRINDLEWOOD IS SAFER"][stage];
 $("#health-value").textContent=hp;$("#health-bar").style.width=hp+"%";$("#gold-value").textContent=gold;$("#potion-value").textContent=potions;
 $("#xp-value").textContent=xp;$("#level-value").textContent=level;
 $("#inventory-items").textContent=items.length?items.join(", "):"Empty · 0 items";save();
}
function addMessage(t,who="npc"){const d=document.createElement("div");d.className="message "+(who==="player"?"player-message":"npc-message");const s=document.createElement("span");s.className="speaker";s.textContent=who==="player"?"YOU":npcs[currentNpc].name.toUpperCase();const p=document.createElement("p");p.textContent=t;d.append(s,p);$("#messages").append(d);$("#messages").scrollTop=$("#messages").scrollHeight;}
function select(id,greet=true){currentNpc=id;const n=npcs[id];document.querySelectorAll(".npc").forEach(e=>e.classList.toggle("selected",e.dataset.npc===id));$("#npc-name").textContent=n.name;$("#npc-role").textContent=n.role;$("#portrait").textContent=n.initial;if(greet){$("#messages").innerHTML="";addMessage(n.greeting);}}
function dist(x,y){const r=world.getBoundingClientRect();return Math.hypot(r.width*(pos.x-x)/100,r.height*(pos.y-y)/100);}
function nearNpc(){let found=null,best=Infinity;document.querySelectorAll(".npc").forEach(e=>{const x=parseFloat(e.style.getPropertyValue("--x")),y=parseFloat(e.style.getPropertyValue("--y")),d=dist(x,y);e.classList.toggle("nearby",d<78);if(d<best){best=d;found=e;}});return best<78?found:null;}
function prompt(t){$("#interaction-prompt").textContent=t;}
function interact(){
 const n=nearNpc();
 if(stage===1&&dist(55,23)<75){stage=2;items.push("Ancient Sigil");ui();select("elder_mira",false);addMessage("You found the Ancient Sigil! Its blue light pulses in your hand. Return it to Elder Mira.");return;}
 if(!n){prompt("MOVE CLOSER TO AN NPC OR QUEST MARKER");return;}
 select(n.dataset.npc,false);
 if(n.dataset.npc==="elder_mira"&&stage===0){stage=1;ui();addMessage("The northern beacon has awakened. Find the Ancient Sigil near the old shrine and bring it to me.");}
 else if(n.dataset.npc==="elder_mira"&&stage===2){stage=3;gold+=15;xp+=25;items.push("Village Token");ui();addMessage("Thank you! Here are 15 gold, 25 XP and a Village Token. A Forest Wraith has appeared near the eastern pond. Defeat it!");}
 else if(n.dataset.npc==="elder_mira"&&stage===3)addMessage("The wraith lurks by the eastern pond. Get close and strike with SPACE.");
 else if(n.dataset.npc==="elder_mira"&&stage===4)addMessage("You saved Brindlewood. The beacon's deeper mystery awaits in the forest.");
 else if(n.dataset.npc==="kael")addMessage("The wraith is vulnerable. Get close and press SPACE. Press H to drink a healing potion.");
 else addMessage("Mira's quest has stirred the village. I have a feeling your adventure is only beginning.");
}
function attack(){if(stage!==3||wraithDead)return;if(dist(88,76)>95){prompt("MOVE CLOSER TO THE WRAITH");return;}wraithHP--;if(wraithHP<=0){wraithDead=true;stage=4;gold+=30;xp+=75;items.push("Wraith Essence");if(xp>=100){level++;xp-=100;}addMessage("The Forest Wraith dissolves into silver sparks! You earned 30 gold, 75 XP and collected Wraith Essence.");}else{hp=Math.max(1,hp-8);addMessage("You strike the Wraith! "+wraithHP+" hits remain. It retaliates and drains 8 HP.");}ui();}
function heal(){if(!potions){prompt("NO POTIONS LEFT");return;}if(hp>=100){prompt("HEALTH IS ALREADY FULL");return;}potions--;const old=hp;hp=Math.min(100,hp+35);ui();prompt("POTION USED · +"+(hp-old)+" HP");}
function loop(t){const dt=Math.min((t-last)/1000||0,.05);last=t;let x=0,y=0;if(keys.has("a")||keys.has("arrowleft"))x--;if(keys.has("d")||keys.has("arrowright"))x++;if(keys.has("w")||keys.has("arrowup"))y--;if(keys.has("s")||keys.has("arrowdown"))y++;if(x||y){const l=Math.hypot(x,y),r=world.getBoundingClientRect();pos.x=Math.max(4,Math.min(96,pos.x+x/l*190*dt/r.width*100));pos.y=Math.max(9,Math.min(91,pos.y+y/l*190*dt/r.height*100));player.style.left=pos.x+"%";player.style.top=pos.y+"%";save();}
 const n=nearNpc();if(stage===1&&dist(55,23)<75)prompt("PRESS E TO COLLECT THE ANCIENT SIGIL");else if(n)prompt("PRESS E TO TALK TO "+npcs[n.dataset.npc].name.toUpperCase());else if(stage===3&&!wraithDead&&dist(88,76)<95)prompt("PRESS SPACE TO ATTACK THE WRAITH");else prompt("WASD MOVE · E INTERACT · SPACE ATTACK · H HEAL");requestAnimationFrame(loop);}
document.querySelectorAll(".npc").forEach(e=>e.addEventListener("click",()=>select(e.dataset.npc)));
document.querySelectorAll(".suggestions button").forEach(e=>e.addEventListener("click",()=>send(e.textContent)));
async function send(m){m=m.trim();if(!m||busy)return;busy=true;addMessage(m,"player");$("#message").value="";try{const r=await fetch(API+"/api/chat",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId,npc_id:currentNpc,message:m,player_name:"Traveler"})});if(!r.ok)throw Error("API "+r.status);const d=await r.json();addMessage(d.reply);$("#mode").textContent=d.ai_enabled?"AI CONNECTED":"LOCAL MODE";}catch(e){addMessage("I cannot reach the village network. Start the FastAPI backend and try again.");}finally{busy=false;}}
$("#chat-form").addEventListener("submit",e=>{e.preventDefault();send($("#message").value);});
$("#reset").addEventListener("click",async()=>{localStorage.removeItem(SAVE_KEY);try{await fetch(API+"/api/session/reset",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({session_id:sessionId})});}catch(e){}stage=0;hp=100;gold=0;potions=2;xp=0;level=1;items=[];wraithHP=3;wraithDead=false;pos={x:49,y:78};player.style.left=pos.x+"%";player.style.top=pos.y+"%";ui();select(currentNpc);});
window.addEventListener("keydown",e=>{if(["INPUT","TEXTAREA"].includes(document.activeElement.tagName))return;const k=e.key.toLowerCase();if(["w","a","s","d","arrowup","arrowdown","arrowleft","arrowright"," ","e","h"].includes(k))e.preventDefault();if(k==="e"&&!e.repeat)interact();if(k===" "&&!e.repeat)attack();if(k==="h"&&!e.repeat)heal();if(["w","a","s","d","arrowup","arrowdown","arrowleft","arrowright"].includes(k))keys.add(k);});
window.addEventListener("keyup",e=>keys.delete(e.key.toLowerCase()));window.addEventListener("blur",()=>keys.clear());
load();player.style.left=pos.x+"%";player.style.top=pos.y+"%";ui();requestAnimationFrame(loop);
(async()=>{try{const r=await fetch(API+"/health");if(!r.ok)throw 0;const d=await r.json();$("#connection").textContent="SERVER ONLINE";$("#mode").textContent=d.ai_enabled?"AI CONNECTED":"LOCAL MODE";}catch(e){$("#connection").textContent="SERVER OFFLINE";$("#connection").style.color="#e5a46b";}})();
