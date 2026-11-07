# THE WARDEN: build plan (incident.io track) - v1, 7 Nov 2026 ~14:00
Repo: https://github.com/cklaw25/partyringmark1 (empty right now)
Working name: **THE WARDEN** (rename freely)
Status: v1 plan. Merge with the team's full idea prompt when it arrives.

---

## 1. The idea (fine-tuned)
**One line:** a first-person escape maze where the antagonist is a live AI agent. It watches you through the game, your webcam and your mic, reacts in the moment with a big personality, and learns which tricks throw you off, so the maze changes around you.

**How it maps to the prompt:**
| Prompt says | We do |
|---|---|
| "reacts to a live feed as it happens" | 3 live feeds: game telemetry (60/s), webcam (face), mic (loudness) |
| "with personality" | The Warden: a theatrical game-show-host villain who remembers your habits and taunts you by voice |
| "point it at a camera, microphone, screen or a game" | We use all of them at once |
| "respond in the moment" | Reflex layer reacts in <100 ms; strategist re-plans every ~3-5 s |

**Game loop (one run = 3-4 min, perfect for a judge to play):**
1. You spawn in a dark grid maze with a flashlight. Goal: find 3 keys, then the exit.
2. The Warden speaks from the start ("Ah, a new guest. Let's see how you think.").
3. As you play, it builds a profile of you and attacks your weaknesses:
   - you always turn left -> it starts closing left turns;
   - you stand still when scared -> it whispers and moves walls behind you;
   - you shout (mic spike) -> it hears you and sends the Hunter;
   - you look away from the screen (no face on webcam) -> the maze shifts while you're "not looking" (Weeping Angels style);
   - you rely on the minimap -> it scrambles the minimap.
4. Escape or get caught, then the **POST-MORTEM screen**: the Warden's report on how it beat you, with a timeline of what it saw and what it did and why. This is a direct nod to incident.io's own product (incident timeline + post-mortem).

**What makes it NOT a wrapper (our own code does the work):**
- Maze generator + **BFS/A\* checks**: every wall shift must keep the exit reachable (it's always fair).
- **Player profiler**: turn bias, hesitation, backtracking, panic score (mic + mouse jerk + speed), face-away time.
- **Trap learning = multi-armed bandit** (epsilon-greedy / UCB): every trap type is scored by how much it slowed or panicked YOU, so the Warden literally learns which trick works on this player.
- **Two-brain latency design**: instant code reflexes + slower AI strategy. We measure and show both latencies live.
- **Hunter enemy** with A\* pathfinding.
- Claude only does: choosing the strategy (via tool calls) + writing the lines.

**On-screen "Warden's Brain" panel (key for the judges):** live player profile bars, trap win-rates, the current plan, the last tool call, and latency counters (reflex 30 ms / strategist 1.4 s). This shows the tech is real while people watch.

---

## 2. Architecture
```
 BROWSER (Three.js game)                         BACKEND (Python FastAPI)
 ┌─────────────────────────────┐   WebSocket    ┌──────────────────────────────┐
 │ Game: maze, player, hunter  │ ──events────▶  │ Strategist loop (every 3-5s  │
 │ Sensors:                    │                │  or on big events)           │
 │  - telemetry (pos, turns)   │                │  - compact state JSON        │
 │  - webcam (MediaPipe face)  │                │  - Claude + tools            │
 │  - mic (Web Audio loudness) │  ◀──actions──  │  - returns tool calls + line │
 │ REFLEX layer (rules, <100ms)│                │ Bandit + profile (or in JS)  │
 │ Executes actions in world   │                │ TTS request (ElevenLabs)     │
 │ Brain panel UI              │  ◀──audio────  │ Event log -> post-mortem     │
 └─────────────────────────────┘                └──────────────────────────────┘
```
**Warden tools (what Claude can call; each one has a cooldown + fairness rules):**
- `shift_walls(region, intensity)`: only where the player isn't looking (raycast check), keep the exit reachable
- `cut_lights(seconds)` / `flicker(seconds)`
- `send_hunter(speed, duration)`
- `spawn_fake_exit(near_player)`
- `scramble_minimap(seconds)`
- `lock_door(id, seconds)`
- `taunt(text, emotion)` -> TTS
- `change_rule(rule)`, e.g. "red floor tiles are now traps" (a nice "game changes" moment)

**Director / fairness:** a tension meter (like a horror-game "AI director"). If the player is struggling, the Warden eases off; if they're cruising, it escalates. Max 1 big trap per 8 s. The exit is always reachable.

---

## 3. Tech stack
| Part | Pick | Why / fallback |
|---|---|---|
| Game | **Three.js + Vite** (JS/TS), PointerLockControls, box-grid maze, fog + spotlight flashlight | Web = easy webcam/mic + easy demo on any laptop. Unity only if someone is fast in it |
| Webcam | **MediaPipe Tasks Vision - Face Landmarker** (in browser) | face present / looking away / mouth open. Fallback: just "face present" |
| Mic | **Web Audio API** AnalyserNode (loudness) | Optional extra: Web Speech API so it hears words ("where's the key?") |
| Backend | **Python FastAPI + WebSockets** (or Node if the team prefers) | keeps the API key off the browser |
| AI | **Claude API** with tool use (`strict: true` tools), streaming, small prompt, cached system prompt | model below |
| Voice | **ElevenLabs** streaming TTS (best villain voice) | fallback: browser speechSynthesis, or Piper (from Jarvis) |
| Sounds | free horror SFX (freesound / pixabay) | |
| Run | locally on the demo laptop; frontend can go on Vercel | wired headphones + a mic for the demo |

**Model choice:** default = `claude-opus-5-5` with `output_config: {effort: "low"}` for the strategist. Fastest option if latency hurts = `claude-haiku-5-5` (team's call). NEVER put the LLM on the instant-reaction path; the reflex code handles anything that needs to be instant.

**Keys to get NOW:** Anthropic API key (check for hackathon credits), ElevenLabs free key.

**Repo layout:**
```
partyringmark1/
  frontend/  (vite: src/game/, src/sensors/, src/ui/brainPanel, src/net/)
  backend/   (main.py ws server, warden.py strategist, tools.py, profile.py, bandit.py, prompts/)
  assets/    (sfx, textures)
  README.md  (Devpost-ready)
```

---

## 4. Roles (4 people; merge roles if fewer)
- **A - Game dev:** maze gen, movement, keys/exit, hunter + A\*, executing actions (walls/lights).
- **B - AI/backend:** FastAPI WS, Claude tool loop, prompt/persona, strategist timing, post-mortem generation.
- **C - Sensors + learning:** webcam/mic -> events, player profiler, bandit, reflex rules.
- **D - UX/voice/pitch:** Warden voice + lines, Brain panel, post-mortem screen, sounds, Devpost, video, pitch. Also does the talks/networking.

---

## 5. Timeline (now -> 11:00 Sun)
| Time | Goal | Who |
|---|---|---|
| 14:00-14:45 | Lock the design (this doc), roles, repo scaffold, API keys, agree the event + action JSON formats | all |
| 15:00 | **incident.io talk: someone MUST attend.** Ask the judge: "is a game OK? what impresses you most?" | D |
| 14:45-18:00 | **Phase 1, skeletons in parallel:** A walkable maze + keys/exit; B WS + Claude returns tool calls from fake events; C webcam/mic -> event stream; D voice working + Brain panel mock | split |
| 16:00-17:00 | Capital One talk (optional) / **17:00 speed networking** (CVs!) | D (+1) |
| **18:00** | **Checkpoint 1:** walk the maze, Warden speaks when you pick up a key | all |
| 18:00-22:00 | **Phase 2, connect it all:** tools change the world (walls + BFS check, lights, hunter), reflex rules, live profile | split |
| 19:30 | Dinner (eat together = a quick sync) | |
| **22:00** | **Checkpoint 2: full playable loop start -> escape/caught** | all |
| 22:30 | Optional: 1-person 1 Hour Hackathon | spare person |
| 22:00-02:00 | **Phase 3, the "wow":** bandit learning, personality tuning, post-mortem screen, latency counters, change_rule | split |
| 02:00-06:00 | Sleep in 2 shifts. Awake shift: playtest with OTHER hackers (record clips + reactions = Devpost gold) | shifts |
| 06:00-08:00 | Bug fixing only | all |
| **08:00** | **FEATURE FREEZE** | |
| 08:00-10:30 | Devpost text + screenshots + 20-30s video (Suli tool / Higgsfield), pitch rehearsal x3, backup demo video recorded | D + all |
| **10:30** | **Submit** (30 min buffer before 11:00) | |
| 12:30+ | Judging: 5-min pitch | |

**Cut list if behind (drop from the bottom first):**
- MUST: maze + keys/exit, telemetry feed, Claude strategist w/ 3 tools (walls, lights, taunt), voice, post-mortem
- SHOULD: webcam face-away, mic shout, hunter, Brain panel, latency counters
- COULD: bandit learning, change_rule, fake exits, speech recognition, minimap scramble

---

## 6. Feedback / risks (honest)
1. **Scope is the #1 risk.** 3D + AI + sensors in 21 h is a lot. Keep graphics dead simple (boxes + fog + flashlight = scary enough). Hit checkpoints or cut.
2. **AI latency:** a 1-3 s LLM call is too slow for "in the moment" on its own. The reflex layer fixes this; say it proudly in the pitch ("instant reflexes, deliberate strategy").
3. **Fairness:** an AI that just blocks you = frustrating. BFS reachability + a tension meter keep it fun. Judges must be able to escape (or nearly) in 3 min.
4. **Noisy judging room:** calibrate the mic threshold at start ("say hi to the Warden" = calibration, which doubles as an intro). Have a keyboard fallback for every sensor.
5. **Originality:** Granny = genre inspiration only. Our own villain, look and name. The new part = an antagonist that perceives the REAL you and learns.
6. **Don't over-sell finance (scrubbed)** or incident tooling. A light touch only: the post-mortem + timeline nod.
7. **Double-dip:** it's fun and weird, so it's also eligible for Most Creative / WHACKiest. Costume idea: dress as the Warden.

---

## 7. 5-min pitch skeleton
- 0:00-0:30 Hook: "Most AI waits for you to ask. The Warden doesn't wait. It's watching you right now." (webcam light on)
- 0:30-3:00 **Live demo**: a judge plays. Point out the Brain panel: "it just noticed you always turn left."
- 3:00-3:45 How it works: 3 live feeds -> reflex (<100 ms) + strategist (Claude tools) -> bandit learns your weakness. Show the latency numbers.
- 3:45-4:30 The post-mortem screen: "like incident.io, it writes up the incident... except you were the incident."
- 4:30-5:00 What's next + thanks. Other uses of live reactive agents: training sims, coaching (gym/shooting form).

---

## 8. Open questions for the team
- Team size + who's strongest at JS / Python / 3D?
- Anyone very fast in Unity (only then consider Unity)?
- Do we have API credits (Anthropic / ElevenLabs)?
- Paste the team's full idea prompt -> merge into v2 of this plan.
