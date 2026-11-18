# Brindlewood: The Awakening — v0.4 (Unity 3D edition)

v0.4 turns the 2D browser prototype into a **fully procedural 3D game for Unity**.
The whole world is generated from a seed at runtime — **no scene setup, prefabs or art assets needed.**

## Quick start (Unity)
1. Create a new **3D project** in Unity 2021.3 LTS or newer (Unity 6 works; Built-in or URP both work).
2. Copy `unity/Assets/Scripts` into your project's `Assets` folder.
3. Open any scene (an empty one is fine) and press **Play**. `GameBootstrap` creates everything.
4. (Optional) Start the AI dialogue backend — see below. Without it, NPCs use built-in local dialogue.

> The new Input System and the legacy Input Manager are both supported automatically.

## What is generated for every seed
- Noise terrain with a mountain wall around the map, painted ground texture (grass, sand, rock, snow, roads, plaza)
- Lakes, a pond bowl, winding dirt roads that stay above water
- Village: Elder's Hall, Inn, cottages, well, lamps, barrels, Kael's campfire
- Ancient ruins on a plateau with pillars, altar, floating Ancient Sigil and a tall blue beacon visible from the village
- ~800 pine/oak trees, rocks, bushes, grass tufts and flowers; drifting clouds
- Day/night cycle (sun, moon, stars, fog, glowing windows and lamps)
- Forest Imps roaming the wilds, plus the quest Forest Wraith at the pond

Press **Esc** to open the menu, type any number or word as a seed, and press GENERATE.

## Controls
| Key | Action |
|---|---|
| W A S D / arrows | Move (relative to camera) |
| Shift | Sprint |
| Space | Jump |
| E | Talk / interact / collect |
| F or Left click | Sword attack |
| H | Healing potion (+35 HP) |
| Hold Right mouse + move | Orbit camera (scroll to zoom) |
| Esc | Pause menu / close dialogue |

## Gameplay (ported from v0.3)
Quest "The Sleeping Beacon": talk to Elder Mira → take the Ancient Sigil from the northern ruins → return to Mira →
defeat the Forest Wraith at the eastern pond. Health, gold, potions, XP/levels, inventory, combat and autosave all carry over.
New in 3D: minimap with quest marker, quest "!" / "?" markers, floating damage numbers, enemy health bars,
imps that respawn, defeat/respawn instead of a 1-HP floor, level-ups that restore health.

Progress is saved in `PlayerPrefs` (world seed, position, quest, inventory, time of day).

## Script map (`unity/Assets/Scripts`)
| Folder | Files |
|---|---|
| Core | `GameBootstrap` (auto-start), `GameManager` (state, quests, save, spawning), `GameInput` |
| World | `WorldGenerator`, `DayNightCycle`, `MeshBuilder`, `GameMaterials`, `WorldProps` |
| Characters | `PlayerController`, `ThirdPersonCamera`, `NpcActor`, `EnemyAI`, `CharacterModel` |
| UI | `HudUI`, `DialogueUI`, `UiKit` (IMGUI — no TextMeshPro/uGUI required) |
| (root) | `NpcChatClient` — upgraded; sends quest stage + level to the backend |

`NpcInteractable` and the TMP-based `NpcChatUI` from v0.3 were replaced by `NpcActor` and `DialogueUI`.

## Tuning
Open `WorldGenerator.cs`: `Size`, `Res`, `WaterLevel`, and the counts in `BuildVegetation`.
`GameManager.SpawnImps` controls enemy count. `DayNightCycle.dayLengthSeconds` sets day length (default 8 minutes).

## Backend (optional AI dialogue)
```powershell
cd AI-NPC-RPG\backend
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
Copy-Item .env.example .env
python -m uvicorn app.main:app --reload
```
Set `AI_API_KEY`, `AI_BASE_URL`, `AI_MODEL` in `.env` for real AI replies. Backend upgrades in v0.4:
- Requests carry `quest_stage` and `player_level`, so NPCs know how far the player is
- Local fallback dialogue is quest-aware
- Per-session rate limit (`RATE_LIMIT_PER_MINUTE`) and configurable CORS (`ALLOWED_ORIGINS`)

The Unity client talks to `http://127.0.0.1:8000`. If it is unreachable, dialogue silently falls back to local lines.

## Build notes
- Add `Universal Render Pipeline/Lit` (or `Standard`) and `Sprites/Default` to **Project Settings > Graphics > Always Included Shaders**
  so runtime-created materials survive in standalone builds.
- Plain `http://` requests may need **Player Settings > Other Settings > Allow downloads over HTTP** on some platforms.

## Still to do
Chunked/infinite terrain, animated water shader, audio, real animations/art, enemy variety, multiplayer, cloud saves, accounts.
The 2D browser prototype in `frontend/` is unchanged.
