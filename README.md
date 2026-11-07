# THE VAULT: OUTBREAK
**In most games, you learn the maze. In this one, the maze learns you.**
WHACK 26, incident.io track: *"Build an agent that reacts to a live feed as it happens, with personality."*

THE BROKER is a reactive AI agent that runs a horror maze. It watches the live event stream of your play (running, hiding, doors, thrown bottles), builds a behavioural memory, predicts your next move and rewires the maze against you. You can trick it, and it learns from being tricked.

## Architecture: fast path + slow path
- **Fast path (browser, every frame, ~0.1-1 ms):** movement, A* chase, line-of-sight vision, hearing, deception learning (trust in noises), BFS fairness checks. No AI on this path, so reactions are instant.
- **Slow path (Python + Claude, every ~5 s or on big events):** reads a compact state + profile, calls one strict tool `broker_decide` (BLOCK_CORRIDOR, INTERCEPT_ROUTE, SEARCH_HIDING_PLACES, ...) with a reason, prediction, confidence and a spoken line.
- **The engine validates every AI action** (cooldowns, never trap the player via BFS reachability). If the AI is slow or offline, rule-based fallback keeps the game running.
- **Live agent panel** shows observations -> memory -> prediction -> decision -> outcome, plus measured latency.
- **Post-mortem** at the end: the BROKER's incident timeline of how it beat you.

## Run
```bash
# backend
cd backend
pip install -r requirements.txt
cp .env.example .env     # then put your key in .env
python -m uvicorn main:app --port 8000

# frontend (second terminal)
cd frontend
npm install
npm run dev              # open http://localhost:5173  (?seed=42 for another maze, ?mode=full for 30 min)
```
Controls: WASD move · SHIFT run (loud) · E interact/hide · SPACE throw bottle · V toggle fog · M simulate a mic shout
