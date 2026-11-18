
# 🦇 BRINDLEWOOD: THE AWAKENING 🦇

### *The village sleeps. The beacon is dark. Someone has to answer the call.*

**An AI-powered NPC RPG that runs in your browser: top-down village, living characters, one dangerous quest.**

![Python](https://img.shields.io/badge/Python-3.10%2B-111111?style=for-the-badge&logo=python&logoColor=F5C518)
![FastAPI](https://img.shields.io/badge/FastAPI-Backend-111111?style=for-the-badge&logo=fastapi&logoColor=F5C518)
![JavaScript](https://img.shields.io/badge/Frontend-Vanilla%20JS-111111?style=for-the-badge&logo=javascript&logoColor=F5C518)
![Unity](https://img.shields.io/badge/Unity-C%23%20Starter-111111?style=for-the-badge&logo=unity&logoColor=F5C518)
![Status](https://img.shields.io/badge/Status-Prototype-F5C518?style=for-the-badge&labelColor=111111)

</div>

---

## 🌑 Table of Contents

- [Mission Briefing](#-mission-briefing)
- [The Quest: The Sleeping Beacon](#-the-quest-the-sleeping-beacon)
- [Features](#-features)
- [Requirements](#-requirements)
- [Project Structure](#-project-structure)
- [Deploy to the Batcave (Setup)](#-deploy-to-the-batcave-setup)
- [Controls](#-controls)
- [Activate the AI (Optional)](#-activate-the-ai-optional)
- [API Endpoints](#-api-endpoints)
- [Troubleshooting](#-troubleshooting)
- [Unity Scripts](#-unity-scripts)
- [Known Limitations](#-known-limitations)
- [Security Protocol](#-security-protocol)
- [Customization](#-customization)

---

## 🦇 Mission Briefing

**Brindlewood: The Awakening** is a small, browser-playable RPG prototype. Walk a top-down village, talk to NPCs powered by a FastAPI backend, and complete a quest, all from your keyboard.

The dialogue system works in two modes:

| Mode | What it does |
|------|--------------|
| 🕶️ **Stealth Mode (default)** | Local fallback dialogue. No API key needed. |
| 🤖 **Oracle Mode (optional)** | Live AI dialogue through any OpenAI-compatible chat API. |

> *"It's not who you are underneath, it's what you do that defines you."*
> Talk to everyone. Trust the right people.

---

## 🔦 The Quest: The Sleeping Beacon

| Step | Objective |
|:----:|-----------|
| 1 | Talk to **Elder Mira** and accept the quest. |
| 2 | Search the **northern part of the village** for the glowing **Ancient Sigil**. |
| 3 | Move close to the Sigil and press **E** to collect it. |
| 4 | Return to **Elder Mira** and press **E** to complete the quest. |

A built-in **quest tracker** shows your current objective and progress.

---

## ⚡ Features

- 🕹️ **WASD / Arrow-key movement** around the village
- 💬 **NPC interaction**: walk close and press `E`, or click an NPC to select them
- 🗺️ **Quest system** with a live quest tracker
- 🧠 **FastAPI backend** for NPC dialogue and session handling
- 🔌 **Optional AI dialogue** via an OpenAI-compatible chat API, with local fallback when no key is set
- 🎮 **Unity C# integration scripts** as a starting point for a future Unity version

---

## 🧰 Requirements

- Windows, macOS, or Linux
- **Python 3.10** or newer
- A modern web browser
- VS Code *(recommended, not required)*

---

## 📁 Project Structure

```
AI-NPC-RPG/
├── backend/
│   ├── app/
│   │   ├── main.py            # FastAPI app and routes
│   │   └── npc_engine.py      # NPC dialogue logic
│   ├── .env.example           # Template for AI settings
│   └── requirements.txt
├── frontend/
│   ├── index.html
│   ├── style.css
│   └── app.js
├── unity/
│   └── Assets/
│       └── Scripts/           # C# integration starters
└── README.md
```

---

## 🚀 Deploy to the Batcave (Setup)

Open the extracted **AI-NPC-RPG** folder in VS Code. You will need **two terminals**: one for the backend, one for the frontend.

### Step 1: Start the Backend

#### Windows (PowerShell)

```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
```

> **PowerShell blocking activation?** Either allow scripts for this terminal session only:
> ```powershell
> Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
> .\.venv\Scripts\Activate.ps1
> ```
> Or skip activation and call the environment's Python directly:
> ```powershell
> .\.venv\Scripts\python.exe -m pip install -r requirements.txt
> ```

#### macOS / Linux

```bash
cd backend
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements.txt
```

#### Launch the API

```bash
python -m uvicorn app.main:app --reload
```

Leave this terminal running. You should see Uvicorn serving at **http://127.0.0.1:8000**.

Verify the backend is alive:

- Health check: <http://127.0.0.1:8000/health>
- API docs: <http://127.0.0.1:8000/docs>

### Step 2: Start the Frontend

Open a **second terminal** at the project root:

```bash
cd frontend
python -m http.server 5500
```

Leave it running, then open **<http://localhost:5500>** in your browser.

> ⚠️ Keep **both** terminals running while you play.

---

## 🎮 Controls

| Key / Action | What it does |
|--------------|--------------|
| `W` / `↑` | Move up |
| `A` / `←` | Move left |
| `S` / `↓` | Move down |
| `D` / `→` | Move right |
| `E` | Interact with a nearby NPC or quest item |
| **Mouse click on an NPC** | Select that character for conversation |

> 💡 Move close enough to a character or the Ancient Sigil before pressing `E`.

---

## 🤖 Activate the AI (Optional)

The backend runs fine without a key using local fallback dialogue. To enable AI responses:

1. Stop the backend with `Ctrl+C`.
2. In the `backend` folder, copy the example environment file:

   ```powershell
   # Windows (PowerShell)
   Copy-Item .env.example .env
   ```
   ```bash
   # macOS / Linux
   cp .env.example .env
   ```

3. Open `backend/.env` and set your provider details:

   ```env
   AI_API_KEY=your_api_key_here
   AI_BASE_URL=https://api.openai.com/v1
   AI_MODEL=gpt-4o-mini
   ```

4. Save the file and restart the backend:

   ```bash
   python -m uvicorn app.main:app --reload
   ```

The `/health` endpoint reports whether AI is enabled.

---

## 📡 API Endpoints

| Method | Endpoint | Purpose |
|:------:|----------|---------|
| `GET` | `/health` | Checks that the backend is running and whether AI is enabled |
| `GET` | `/api/npcs` | Lists the available NPCs |
| `POST` | `/api/chat` | Sends a message to an NPC |
| `POST` | `/api/session/reset` | Resets a conversation session |

**Example chat request:**

```json
{
  "session_id": "player-1",
  "npc_id": "elder_mira",
  "message": "Do you have a quest?",
  "player_name": "Traveler"
}
```

---

## 🛠️ Troubleshooting

<details>
<summary><b><code>cd backend</code> says the folder does not exist</b></summary>

Make sure your terminal is at the **project root**, the folder that contains `backend`, `frontend`, and `unity`. If the extracted ZIP contains an outer folder, open the inner `AI-NPC-RPG` folder. Run `dir` (PowerShell) or `ls` (macOS/Linux) to see what is in your current location.
</details>

<details>
<summary><b><code>cd index.html</code> says the path does not exist</b></summary>

`index.html` is a file, not a folder. Do not use `cd` to enter it. Start the frontend server from the `frontend` directory:

```bash
python -m http.server 5500
```
</details>

<details>
<summary><b><code>uvicorn</code> is not recognized</b></summary>

Launch it through Python instead:

```bash
python -m uvicorn app.main:app --reload
```

If the module is missing, install the requirements from the `backend` directory:

```bash
python -m pip install -r requirements.txt
```
</details>

<details>
<summary><b>The page at port 8000 says <code>{"detail":"Not Found"}</code></b></summary>

That is the FastAPI root URL, and this prototype does not define a homepage there. Open the game at **http://localhost:5500** instead, and use **http://127.0.0.1:8000/docs** for API documentation.
</details>

<details>
<summary><b>The game says the server is offline</b></summary>

Confirm the backend is running and that <http://127.0.0.1:8000/health> opens successfully. Keep the backend terminal open while playing.
</details>

---

## 🎞️ Unity Scripts

The `unity/Assets/Scripts` folder contains example C# scripts for connecting a Unity project to the FastAPI dialogue service. They are **integration starting points**. This repository does not include a complete Unity scene or a built Unity game.

---

## 🚧 Known Limitations

- The browser game is a **2D prototype**, not a finished 3D RPG.
- Quest progression is handled in the **frontend** and is **not saved** between page reloads.
- Movement and interaction are basic. **Collision, combat, inventory, multiplayer, and persistent save/load** are not included yet.

---

## 🔐 Security Protocol

Even the Dark Knight has a code. Follow it:

- 🚫 **Never** share your API key.
- 🚫 **Never** commit your `.env` file to a public repository (add it to `.gitignore`).
- 🚫 **Never** put the key in frontend JavaScript or Unity scripts. Keep it on the backend.
- 🛡️ **Before public deployment**, restrict backend CORS settings and add authentication, rate limiting, and input controls.

---

## 🎨 Customization

This prototype is built for learning and further development. Make Gotham your own by editing:

- **NPC dialogue** in `backend/app/npc_engine.py`
- **Village art and styling** in `frontend/style.css`
- **Quest text and game mechanics** in `frontend/app.js`

---

<div align="center">

### 🦇 *The night is darkest just before the dawn. Now go wake the beacon.* 🦇

Made for learning, tinkering, and building something legendary.

</div>
