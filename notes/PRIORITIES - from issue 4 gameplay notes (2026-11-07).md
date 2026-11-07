# Build priorities after issue #4 (gameplay notes) - 7 Nov ~15:40
Source: GitHub issues #1-4 (pizza-py: maze env / broker character / broker AI; sjacob2707: THE VAULT gameplay notes).
Main build = Unity 3D first-person. The 2D Phaser version (frontend/) stays only as the idea showcase.

## Already built (Unity)
maze + rooms + lockers + bottles · BROKER-controlled doors/shutters with BFS fairness · hunter with sight + hearing ·
chase/search/investigate/guard/intercept · memory that persists after capture · deception learning · Claude strategist (Haiku) ·
live agent panel · post-mortem · voice · minimap

## P0 - building now (the core of the notes)
1. **Three escape rooms + three-lock mechanic** (Power / Surveillance / Emergency Release). Entering seals the room and alerts THE BROKER;
   90 s timer; every mistake breaks a lock; 3 broken = THE BROKER can enter; timer runs out = 1 lock breaks + extra time.
   Puzzles: Power = breaker sequence memory · Surveillance = read 4 monitors in the 3D room -> keypad code (BROKER says a FAKE code) · Emergency = timed lever override.
2. **Security cameras + incomplete knowledge.** Visible sweeping cameras with a light cone. THE BROKER only knows where you are from cameras,
   the hunter's own eyes, sounds and room alerts. Crouch (C / CTRL) to slip under cameras.
3. **Three-day system** (replaces lives). Caught = lose a day, wake in containment, rooms you finished stay finished, memory persists.
   Day 3 = more surveillance.
4. **Central exit**: blast door in the middle opens only after all 3 rooms are done.
5. **Threat states** shown on the HUD: EXPLORATION / INVESTIGATION / ACTIVE HUNT. No hunting at the start; hunter slower than your run.

## P1 - next
- BROKER speaks through room speakers during puzzles (taunts using your mistakes), more Claude actions (activate camera)
- route types: dark maintenance corridors vs bright surveillance corridors
- better BROKER model/animation (issue #2), sounds (footsteps, alarms)
- real mic noise

## P2 - only if time
- truly circular ring-shaped maze (we use rings as zones: outer / middle / inner on a grid)
- end screen polish, difficulty tuning, multiple seeds

## Not doing (scope)
- 3 in-game "days" of real time: a day = one life
