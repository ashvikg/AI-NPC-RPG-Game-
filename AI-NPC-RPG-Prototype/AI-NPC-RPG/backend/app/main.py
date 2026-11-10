from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from dotenv import load_dotenv
from pathlib import Path
import os
from .npc_engine import DialogueEngine, NPCS

load_dotenv(Path(__file__).resolve().parents[1] / ".env")

app = FastAPI(title="AI NPC RPG API", version="1.0.0",
              description="Conversational NPC backend for the AI NPC RPG prototype.")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Restrict this to your game domain before production.
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)
engine = DialogueEngine(
    api_key=os.getenv("AI_API_KEY", ""),
    base_url=os.getenv("AI_BASE_URL", "https://api.openai.com/v1"),
    model=os.getenv("AI_MODEL", "gpt-4o-mini"),
)

class ChatRequest(BaseModel):
    session_id: str = Field(min_length=1, max_length=100)
    npc_id: str = Field(min_length=1, max_length=60)
    message: str = Field(min_length=1, max_length=1200)
    player_name: str = Field(default="Traveler", max_length=40)

class ResetRequest(BaseModel):
    session_id: str = Field(min_length=1, max_length=100)

@app.get("/health")
async def health():
    return {"status": "ok", "ai_enabled": engine.ai_enabled}

@app.get("/api/npcs")
async def list_npcs():
    return {"npcs": [{"id": key, "name": value["name"], "role": value["role"]}
                     for key, value in NPCS.items()]}

@app.post("/api/chat")
async def chat(payload: ChatRequest):
    if payload.npc_id not in NPCS:
        raise HTTPException(status_code=404, detail="NPC not found")
    try:
        reply = await engine.reply(payload.session_id, payload.npc_id,
                                   payload.player_name, payload.message)
        return {"reply": reply, "npc": NPCS[payload.npc_id]["name"],
                "ai_enabled": engine.ai_enabled}
    except Exception:
        raise HTTPException(status_code=502, detail="Dialogue service failed. Check backend logs.")

@app.post("/api/session/reset")
async def reset_session(payload: ResetRequest):
    engine.reset(payload.session_id)
    return {"status": "reset"}
