Brindlewood: The Awakening --- AI NPC RPG

A small browser-playable RPG prototype featuring a top-down village, NPC
conversations, keyboard movement, and a simple quest.
Features
- WASD / arrow-key movement around the village.
- NPC interaction: walk close to an NPC and press E, or click an
  NPC to select them.
- Quest: The Sleeping Beacon
  1. Talk to Elder Mira to accept the quest.
  2. Find the glowing Ancient Sigil in the northern part of the
     village.
  3. Walk close to the sigil and press E to collect it.
  4. Return to Elder Mira and press E to complete the quest.
- Quest tracker showing the current objective and progress.
- FastAPI backend for NPC dialogue and session handling.
- Optional AI dialogue through an OpenAI-compatible chat API. The
  game can use local fallback dialogue when no API key is configured.
- Unity C# integration scripts are included as a starting point
  for a future Unity version.
Requirements
- Windows, macOS, or Linux
- Python 3.10 or newer
- A modern web browser
- VS Code (recommended, but not required)
Project structure
AI-NPC-RPG/
├── backend/
│   ├── app/
│   │   ├── main.py
│   │   └── npc_engine.py
│   ├── .env.example
│   └── requirements.txt
├── frontend/
│   ├── index.html
│   ├── style.css
│   └── app.js
├── unity/
│   └── Assets/
│       └── Scripts/
└── README.md
Run the project on Windows (PowerShell)
Open the extracted AI-NPC-RPG folder in VS Code.
1. Start the backend
Open a VS Code terminal and run:
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
If PowerShell blocks virtual-environment activation, either use a new
terminal with this command:
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\.venv\Scripts\Activate.ps1
Or skip activation and install using the Python executable in the
environment:
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
Start the API:
python -m uvicorn app.main:app --reload
Leave this terminal running. You should see a message that Uvicorn is
running at:
http://127.0.0.1:8000
Check the API health page:
- http://127.0.0.1:8000/health
- http://127.0.0.1:8000/docs
2. Start the game frontend
Open a second VS Code terminal. From the project root, run:
cd frontend
python -m http.server 5500
Leave this terminal running and open:
http://localhost:5500
Keep both terminals running while you play: one for FastAPI and one
for the frontend web server.

Controls
  Key / action            What it does
  W / ↑               Move up
A / ←               Move left
S / ↓               Move down
D / →               Move right
E                     Interact with the nearby NPC or quest item
Mouse click on an NPC   Select that character for conversation

Move close enough to a character or the Ancient Sigil before pressing
E.
Optional: enable AI-powered conversations
The backend works without an AI key by using its local fallback
dialogue. To enable AI responses:
1. Stop the backend with Ctrl+C.
2. In a terminal inside the backend folder, copy the example
   environment file:
   Copy-Item .env.example .env
3. Open backend/.env and set your provider details:
   AI_API_KEY=your_api_key_here
   AI_BASE_URL=https://api.openai.com/v1
   AI_MODEL=gpt-4o-mini
4. Save the file and restart the backend:
   python -m uvicorn app.main:app --reload
Security: Never share your API key, commit the .env file to a
public repository, or put the key in frontend JavaScript or Unity
scripts. Keep it on the backend.
API endpoints
  Method                  Endpoint                Purpose
  GET                   /health               Checks whether the
backend is running and
whether AI is enabled

  GET                   /api/npcs             Lists the available
NPCs

  POST                  /api/chat             Sends a message to an
NPC
POST                  /api/session/reset    Resets a conversation
session
Example chat request:
{
  "session_id": "player-1",
  "npc_id": "elder_mira",
  "message": "Do you have a quest?",
  "player_name": "Traveler"
}
Troubleshooting
cd backend says the folder does not exist
Make sure the VS Code terminal is at the project root --- the folder
that contains backend, frontend, and unity. If the extracted ZIP
contains an outer folder, open the inner AI-NPC-RPG folder.

Run dir in PowerShell to see the folders in your current location.
cd index.html says the path does not exist
index.html is a file, not a folder. Do not use cd to enter it. Start
the frontend server from the frontend directory with:
python -m http.server 5500
uvicorn is not recognized
Use Python to launch it:
python -m uvicorn app.main:app --reload
If the module is missing, install the requirements from the backend
directory:
python -m pip install -r requirements.txt
The page says {"detail":"Not Found"} at port 8000
That is the FastAPI root URL, and this prototype does not define a
homepage there. Open the frontend at http://localhost:5500 instead.
Use http://127.0.0.1:8000/docs for the API documentation.
The game says the server is offline
Confirm the backend is running and that http://127.0.0.1:8000/health
opens successfully. Keep the backend terminal open while playing.
Unity scripts
The unity/Assets/Scripts folder contains example C# scripts for
connecting a Unity project to the FastAPI dialogue service. They are
integration starting points; this ZIP does not include a complete Unity
scene or a built Unity game.
Current prototype limitations
- The browser game is a 2D prototype, not a finished 3D RPG.
- The current quest progression is handled in the frontend and is not
  saved permanently between page reloads.
- Movement and interaction are basic prototype mechanics; collision,
  combat, inventory, multiplayer, and persistent save/load are not
  included yet.
- Before public deployment, restrict backend CORS settings and add
  suitable authentication, rate limiting, and input controls.
Credits / customization
This is a prototype intended for learning and further development. You
can customize the NPC dialogue, village art, quest text, and game
mechanics in the source files.
