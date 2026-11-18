from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field
from dotenv import load_dotenv
from pathlib import Path
import os
import time
from collections import defaultdict, deque
from .npc_engine import DialogueEngine, NPCS

load_dotenv(Path(__file__).resolve().parents[1] / ".env")

app = FastAPI(title="AI NPC RPG API", version="1.0.0",
              description="Conversational NPC backend for the AI NPC RPG prototype.")
app.add_middleware(
    CORSMiddleware,
    allow_origins=[o.strip() for o in os.getenv("ALLOWED_ORIGINS", "*").split(",") if o.strip()],  # Set ALLOWED_ORIGINS in production.
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)
engine = DialogueEngine(
    api_key=os.getenv("AI_API_KEY", ""),
    base_url=os.getenv("AI_BASE_URL", "https://api.openai.com/v1"),
    model=os.getenv("AI_MODEL", "gpt-4o-mini"),
)

RATE_LIMIT = int(os.getenv("RATE_LIMIT_PER_MINUTE", "30"))
_hits = defaultdict(deque)


def check_rate(session_id: str):
    """Simple in-memory per-session limiter (use a real gateway/Redis in production)."""
    now = time.monotonic()
    q = _hits[session_id]
    while q and now - q[0] > 60:
        q.popleft()
    if len(q) >= RATE_LIMIT:
        raise HTTPException(status_code=429, detail="Too many messages. Please slow down.")
    q.append(now)


class ChatRequest(BaseModel):
    session_id: str = Field(min_length=1, max_length=100)
    npc_id: str = Field(min_length=1, max_length=60)
    message: str = Field(min_length=1, max_length=1200)
    player_name: str = Field(default="Traveler", max_length=40)
    quest_stage: int = Field(default=0, ge=0, le=4)
    player_level: int = Field(default=1, ge=1, le=99)

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
    check_rate(payload.session_id)
    try:
        reply = await engine.reply(payload.session_id, payload.npc_id,
                                   payload.player_name, payload.message,
                                   payload.quest_stage, payload.player_level)
        return {"reply": reply, "npc": NPCS[payload.npc_id]["name"],
                "ai_enabled": engine.ai_enabled}
    except Exception:
        raise HTTPException(status_code=502, detail="Dialogue service failed. Check backend logs.")

@app.post("/api/session/reset")
async def reset_session(payload: ResetRequest):
    engine.reset(payload.session_id)
    return {"status": "reset"}
