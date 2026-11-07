# THE VAULT: OUTBREAK - build plan v2 (incident.io track)
7 Nov 2026, 14:30. Main idea = teammate's TRACK OVERVIEW.md (the source of truth). This file merges it with the incident.io prompt + latency + demo needs.
Main builder (for now): user. Repo: github.com/cklaw25/partyringmark1 (local C:\partyringmark1)

> "In most horror games, you learn how the enemy behaves. In THE VAULT, the enemy learns how you behave."

---

## 0. incident.io prompt check (how we hit every word)
| Prompt | THE VAULT |
|---|---|
| "agent that reacts to a live feed as it happens" | THE BROKER gets a live event stream from the game (movement, noise, hiding, throws, doors) + optionally the REAL mic |
| "with personality" | THE BROKER speaks: dialogue generated from its actual memory ("Still using the eastern corridor?") |
| "point it at a camera, microphone, screen or a game" | the game = the main feed; the real mic = a bonus feed (shout IRL = noise in-game) |
| "respond in the moment" | fast path (code, <50 ms) + strategist (Claude, every few s / on big events). Latency shown live |

## 1. Where my plan v1 and her plan overlap vs differ (decisions)
**Same in both (keep):** AI-controlled maze changes; BFS reachability so it's never impossible; cooldowns; behavioural profile; hybrid design (code moves the enemy, the LLM picks the strategy); a dev panel showing the AI's brain; personalised taunts; a fallback when the AI is down.

**Decisions:**
- **Name:** THE VAULT: OUTBREAK, the AI = THE BROKER (hers). Drop "The Warden".
- **View:** **top-down 2D** (hers). Maze changes are visible to judges, and it's far faster to build solo. First-person dropped.
- **Engine:** **Phaser 3 (browser)** (hers) instead of Unity. Reasons: solo builder, top-down 2D, judges can open a link, easiest mic access, no Unity merge pain.
- **AI model:** her doc says OpenAI; we use **Claude API** (tool calls with strict schemas). Default `claude-opus-5-5` at effort `low`; switch to `claude-haiku-5-5` if latency hurts (team's call).
- **Game length:** her 30 min / 3 lives -> keep for the full game, BUT add a **DEMO MODE: ~4 min, compact maze**, because judges only have 5 min.
- **Webcam: dropped** (scope). **Real mic: SHOULD-tier bonus** (fits her noise mechanic perfectly).
- **Kept from v1:** the latency split + counters, voice (TTS) for THE BROKER, and the **post-mortem screen** (incident.io nod: "incident timeline: how THE BROKER caught you").
- **Kept from hers, the star features:** deception (player can trick the AI), confidence scores, "Two players, one maze" demo, memory on vs off experiment.

## 2. Core features in priority order
**P0 - MUST (no demo without these)**
1. Top-down playable maze: walk/run, collision, keycards/objectives, exit, win/lose, timer + 3 lives
2. The enemy moves with A* (patrol / investigate / chase) - pure code, fast path
3. Event stream: every player action -> JSON event (run, noise, hide, throw, door, item, spotted)
4. Behavioural memory: profile numbers (running, hiding, corridor counts, distractions, captures) that persist across captures
5. AI-controlled maze changes: lock/unlock doors, seal/open corridors, **BFS check before every change**, cooldowns
6. Claude strategist: state summary -> tool call (BLOCK_CORRIDOR, INTERCEPT_ROUTE, SEARCH_HIDING_PLACE, ...) + one line of dialogue -> validated -> executed. Rule-based fallback if the API fails
7. Live "BROKER STATUS" panel: recent observations, profile, prediction + confidence, decision, outcome, latency

**P1 - SHOULD (what makes it win)**
8. Deception: throw objects; the BROKER's "trust in noises" drops after repeat distractions; the player can fake a preferred route
9. Hiding in lockers + the BROKER learns to search them
10. Voice for the BROKER (ElevenLabs streaming; pre-made stock lines for instant reactions; fallback = browser speech)
11. Post-mortem screen: timeline of what the BROKER saw -> believed -> did, + deception score
12. Demo mode (small maze, 4 min) + same-seed replay for "two players, one maze"
13. Horror atmosphere: darkness/vision cone, flickering lights, alarm, sounds

**P2 - COULD (only if ahead)**
14. Real-mic noise (shout IRL -> noise event at the player's position)
15. Memory on/off toggle for the live A/B experiment
16. Adaptive difficulty (tension meter)
17. Puzzles beyond fetch-the-item (codes from documents)
18. Hosted online build (Vercel + backend) so judges can try it

## 3. Tech stack
| Part | Pick |
|---|---|
| Game | Phaser 3 + Vite (JS), tile grid ~20x20 (demo ~12x12) |
| Fast path | in Phaser: A*, BFS, enemy state machine, noise propagation, profile updates (all JS, per frame) |
| Backend | Python FastAPI + WebSocket (holds the API key, runs the strategist) |
| AI | Claude API, tool use, `strict: true` tools, small cached system prompt, short outputs |
| Voice | ElevenLabs streaming TTS + ~20 pre-generated stock lines |
| Mic (P2) | Web Audio API loudness |
| Run | local laptop for judging; optional deploy |

**Latency design (talking point for the judges):**
- Hot path = the game loop (16 ms frame): movement, chase, alarms, door effects -> instant.
- Cold path = Claude (~1-2 s): strategy only, never blocks the game (async), **plans ahead** (the next 2 moves are queued).
- Shown on the panel: "fast path 12 ms · strategist 1.3 s (p95 2.0 s)".

## 4. Timeline (Sat 14:30 -> Sun 11:00), solo-builder version
| Time | Build | Pitch / video / feedback |
|---|---|---|
| 14:30-15:00 | Scaffold repo: Phaser + Vite, FastAPI, WS link, maze grid renders | |
| **15:00** | | **🔔 incident.io talk: ask the judge for feedback on the idea** (teammate if you're building) |
| 15:00-18:00 | P0 1-3: player, objectives, enemy A*, events | 🔔 16:55 speed networking at 17:00: pitch the idea in 1 line, ask what they'd want to see |
| **18:00** | **CHECKPOINT 1: playable maze + enemy chases** | |
| 18:00-21:00 | P0 4-7: memory, maze changes + BFS, Claude strategist, BROKER panel | teammate: start the pitch SCRIPT draft (v1) |
| 19:30 | dinner (keep it short) | |
| **21:00** | **CHECKPOINT 2: full P0 loop works end to end** | 🔔 show a mentor/organiser (Sam / Edward / Jakub) and ask for feedback |
| 21:00-01:00 | P1 8-13: deception, lockers, voice, post-mortem, demo mode, atmosphere | teammate: script v2 + storyboard for the video |
| 01:00-05:00 | Sleep in shifts. Awake person: playtest with other hackers, **record gameplay clips** | |
| 05:00-07:00 | P2 if ahead, otherwise bug fixing | |
| **07:00** | **FEATURE FREEZE** (earlier than v1 because the video needs footage) | 🔔 ~08:00 final feedback: show the near-final demo to a judge/mentor |
| 07:00-08:30 | | **Record the final video footage** (two-players-one-maze run + panel close-ups) |
| 08:30-09:45 | | **Edit the video** (Suli's tool / Higgsfield for the intro, max 2 min), lock the script |
| 09:45-10:30 | | Devpost write-up + screenshots, rehearse the pitch x3 |
| **10:30** | **SUBMIT** (buffer till 11:00) | |
| 12:30+ | | 5-min live pitch to incident.io |

## 5. Judge feedback reminders (ask different people, each time ask the same 3 questions)
1. "Does this fit the prompt? What would make it fit better?"
2. "What's the most impressive part / the most confusing part?"
3. "What would you want to see in a 5-min demo?"
Times: 15:00 incident.io talk · 17:00 speed networking · ~21:00 organisers · ~08:00 final run-through.
Log the answers in notes/FEEDBACK.md.

## 6. Pitch skeleton (5 min)
- 0:00 Hook + tagline: "In most games, you learn the maze. In ours, the maze learns you."
- 0:30 Live demo: Player A (aggressive) vs Player B (stealth) on the same seed, or a judge plays
- 2:30 The BROKER STATUS panel: perception -> memory -> reasoning -> action -> feedback (her loop)
- 3:30 Tech: fast path vs strategist + latency numbers, BFS fairness, deception
- 4:15 Post-mortem screen (the incident.io nod)
- 4:45 Close: other uses of live reactive agents

## 7. Risks
- Solo-builder scope -> stick to P0 by 21:00 no matter what.
- LLM latency -> never on the hot path; fallback rules always ready.
- Fairness -> BFS + cooldowns + max 1 structural change per ~8 s.
- Demo crash -> recorded backup video + a fixed demo seed.
- Public repo: delete notes/WHACK26_intel.md + the chat exports before submitting.
