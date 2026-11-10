# AI NPC RPG — Conversational NPC Prototype

A small playable RPG conversation prototype with:
- A browser-based top-down village demo (no build step)
- FastAPI backend for NPC dialogue and session memory
- Optional OpenAI-compatible chat API integration (API key stays on the server)
- Unity C# client scripts for connecting a Unity project to the same API

## Quick start

### 1. Start the backend
```bash
cd backend
python -m venv .venv
# Windows: .venv\Scripts\activate
# macOS/Linux: source .venv/bin/activate
pip install -r requirements.txt
copy .env.example .env   # Windows
# cp .env.example .env   # macOS/Linux
uvicorn app.main:app --reload
```
Open `http://127.0.0.1:8000/docs` to inspect the API.

### 2. Play the browser prototype
Open `frontend/index.html` in a browser. It connects to `http://127.0.0.1:8000`.
If your browser blocks local requests from a `file://` page, serve the folder:
```bash
cd frontend
python -m http.server 5500
```
Then visit `http://127.0.0.1:5500`.

### 3. Enable AI dialogue (optional)
Set these values in `backend/.env`:
```env
AI_API_KEY=your_key_here
AI_BASE_URL=https://api.openai.com/v1
AI_MODEL=gpt-4o-mini
```
Restart the backend. Without a key, the demo uses a local fallback dialogue engine, so it still works.

## API
`POST /api/chat`
```json
{
  "session_id": "player-1",
  "npc_id": "elder_mira",
  "message": "What happened to the village?",
  "player_name": "Traveler"
}
```
`POST /api/session/reset` with `{"session_id":"player-1"}` clears conversation memory.
`GET /health` checks service status.

## Unity integration
Copy `unity/Assets/Scripts/` into your Unity project's `Assets/Scripts/`.
Add `NpcChatClient` to a GameObject, assign the NPC ID and API URL, then call
`SendMessageToNpc(...)` from your dialogue UI. See `NpcChatUI.cs` for a minimal UI example.

For an Android build, use your computer's LAN IP instead of `localhost` in the API URL,
and configure CORS/network permissions appropriately. Do not put AI provider keys in Unity.

## Project structure
```text
AI-NPC-RPG/
  backend/       FastAPI service and NPC dialogue logic
  frontend/      Playable browser prototype
  unity/         Unity C# integration scripts
```

This is a prototype scaffold, not a finished commercial game. Add your own Unity scene,
art, animations, audio, and production authentication/rate limiting before deployment.
