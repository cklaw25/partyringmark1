# THE LIVING LABYRINTH
**In most games, you learn the maze. In this one, the maze learns you.**
WHACK 26, incident.io track: *"Build an agent that reacts to a live feed as it happens, with personality."*

THE HOST is a reactive AI agent that runs a horror maze. It watches the live event stream of your play (running, hiding, doors, thrown bottles), builds a behavioural memory, predicts your next move and rewires the maze against you. You can trick it, and it learns from being tricked.

## Two builds
- **`unity/TheVault/` - the main game (Unity 3D, first-person).** Open in Unity 2022.3, press Play: the whole world is built from code.
  A backrooms-style labyrinth (open pillared halls, loopy corridors, flickering lights tied to the power room) around dark-facility escape rooms, 3 escape rooms with a 3-lock mechanic, sweeping security cameras (THE HOST can't see everywhere), 3 days, central exit, crouch/hide/throw.
  All textures, models and sounds are generated in code at startup (`TexGen.cs`, `VaultProps.cs`, `SynthGen.cs`); drop a real recording into `unity/TheVault/Assets/Resources/Audio/<name>.wav` (e.g. `step_carpet_1.wav`, `shatter.wav`) to replace any synthesised sound. A heartbeat and screen vignette/blur build up as THE HOST closes in (`DangerFX.reduceEffects` tones it down).
  THE HOST is a glossy black robot with red ring eyes (`HostBody.cs`), animated in code: a stalking walk, arms reaching in a chase, the head tracking you or scanning, eyes flaring.
  Flow: intro title + sting -> start screen -> wake-up (eyelid blinks, blur, opening lines) -> play -> post-mortem (`VaultScreens.cs`). Menus and puzzles scale with the screen.
- **`frontend/` - 2D browser prototype (idea showcase).** The first version of the concept in Phaser, for quickly showing judges the AI loop.

## Architecture: fast path + slow path
- **Fast path (browser, every frame, ~0.1-1 ms):** movement, A* chase, line-of-sight vision, hearing, deception learning (trust in noises), BFS fairness checks. No AI on this path, so reactions are instant.
- **Slow path (Python + Claude, every ~5 s or on big events):** reads a compact state + profile, calls one strict tool `broker_decide` (BLOCK_CORRIDOR, INTERCEPT_ROUTE, SEARCH_HIDING_PLACES, ...) with a reason, prediction, confidence and a spoken line.
- **The engine validates every AI action** (cooldowns, never trap the player via BFS reachability). If the AI is slow or offline, rule-based fallback keeps the game running.
- **Live agent panel** shows observations -> memory -> prediction -> decision -> outcome, plus measured latency.
- **Post-mortem** at the end: THE HOST's incident timeline of how it beat you.

## Run
```bash
# backend
cd backend
pip install -r requirements.txt
cp .env.example .env     # then put your key in .env
python -m uvicorn main:app --port 8000

# 3D game: open unity/TheVault in Unity Hub (2022.3.62f1) and press Play
# 2D prototype (second terminal)
cd frontend
npm install
npm run dev              # open http://localhost:5173  (?seed=42 for another maze, ?mode=full for 30 min)
```
3D controls: WASD + mouse · SHIFT run (loud) · SPACE jump · C hold = crouch (under cameras), tap = slide while sprinting or mid-air (tap again to stand, SPACE to slide-hop; chain them for extra speed) · E interact / hide / terminal · CLICK throw bottle · F3 speed readout · TAB agent panel · V show hunter on minimap
2D controls: WASD move · SHIFT run (loud) · E interact/hide · SPACE throw bottle · V toggle fog · M simulate a mic shout
