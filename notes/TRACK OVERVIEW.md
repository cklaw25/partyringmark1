# TRACK OVERVIEW  
  
## THE VAULT: OUTBREAK  
## Complete Hackathon Development Plan  
Project: A survival-horror maze game powered by a reactive AI agent that learns from the player's behaviour and dynamically changes the environment.  
Hackathon prompt: Create a Reactive AI Agent  
Core concept: In most horror games, you learn how the enemy behaves. In THE VAULT, the enemy learns how you behave.  
  
## 1. Project Overview  
**The storyline**  
An unexplained catastrophe has devastated the outside world. The player wakes up inside an abandoned underground research facility.  
The doors are locked. The electricity is failing. Something is moving through the corridors.  
The entire facility is controlled by an intelligent security system known as THE BROKER.  
Unlike a traditional horror-game enemy, THE BROKER can observe the player's actions, remember their behaviour, predict what they might do next, and alter the maze in response.  
The player has 30 minutes and three lives to explore the facility, solve puzzles, restore critical systems and escape.  
However, every decision reveals information to THE BROKER, making the maze increasingly dangerous.  
**The unique selling point**  
THE BROKER doesn't just chase the player. It controls the maze and learns how to use it against them.  
For example:  
* A player who constantly runs creates noise, encouraging THE BROKER to investigate and intercept them.    
* A player who repeatedly hides in cupboards teaches THE BROKER to search hiding places.    
* A player who repeatedly uses one corridor may find that the AI has sealed it.    
* A player who throws objects to distract THE BROKER might eventually find that it becomes more suspicious of distractions.    
* A player who deliberately creates misleading patterns can trick THE BROKER into guarding the wrong location.    
The result is a game in which different players experience different threats, even when starting in the same maze.  
  
## 2. The Three Features That Could Make This a Winning Project  
**Feature 1: Behavioural learning and adaptation**  
THE BROKER continuously gathers information about how the player behaves.  
It builds a behavioural profile containing information such as:  

| Behaviour | What THE BROKER learns | Potential reaction |
| ------------------------ | ----------------------------------------- | ----------------------------------- |
| Frequent running | Player prioritises speed | Investigate noisy corridors |
| Repeated hiding | Player prefers concealment | Search cupboards more frequently |
| Reusing corridors | Player follows predictable routes | Intercept or block preferred routes |
| Throwing distractions | Player uses deception | Investigate more cautiously |
| Frequent flashlight use | Player relies on visibility | Monitor illuminated areas |
| Repeated escape attempts | Player is targeting particular objectives | Guard likely escape routes |
  
These adaptations should accumulate throughout the game, rather than resetting after every encounter.  
Why this matters: The intelligence is not simply reacting to the player's current location. It is using historical information to make better decisions.  
**Feature 2: A dynamically changing maze**  
This should be the game's most visually impressive mechanic.  
THE BROKER has control over the facility's security systems and can physically alter the maze.  
It can:  
* Lock and unlock security doors.    
* Seal corridors.    
* Open alternative passages.    
* Activate alarms.    
* Turn lights on or off.    
* Change patrol routes.    
* Redirect the player towards dangerous areas.    
For example, suppose a player repeatedly travels along the eastern corridor.  
THE BROKER identifies this preference and closes a door along that route. The player must find another way through the maze.  
The important distinction is that the decision to change the maze is made by the AI agent in response to what it has learnt, rather than through a predetermined scripted sequence.  
One essential rule: THE BROKER must never create an impossible maze. The game engine should check that the player can still reach the necessary objectives before allowing structural changes.  
**Feature 3: The player can deceive the AI**  
This is the mechanic I would add to make the project particularly distinctive.  
Rather than simply trying to avoid THE BROKER, the player can attempt to manipulate what it believes.  
For example:  
1. The player repeatedly runs through the same corridor.    
2. THE BROKER learns that this is their preferred route.    
3. The AI begins guarding that corridor.    
4. The player deliberately takes another route.    
5. THE BROKER realises its prediction was wrong and adapts.    
Another example involves noise.  
The player repeatedly throws objects into one area. Initially, THE BROKER investigates every sound. Eventually, it learns that some noises may be deliberate distractions and changes its response.  
This creates a fascinating strategic dynamic:  
The AI learns from the player, but the player can also learn how to manipulate the AI.  
That makes the game more interesting than a conventional maze with an intelligent enemy.  
  
## 3. How the Reactive AI Agent Actually Works  
This is the most important technical section because it demonstrates how the project fulfils the hackathon prompt.  
The game needs to contain a genuine autonomous agent, rather than merely adding an AI chatbot to a conventional game.  
I recommend a hybrid architecture, combining real-time game logic with a higher-level AI decision-making system.  
## Stage 1: Perception  
THE BROKER receives a continuous stream of events from the game.  
These might include:  

| Event                | Data received                                  |
| -------------------- | ---------------------------------------------- |
| Player movement      | Current location, speed, direction             |
| Running              | Position, duration, noise level                |
| Hiding               | Hiding location, duration                      |
| Object thrown        | Object type, landing position, sound intensity |
| Flashlight activated | Position, nearby visibility                    |
| Puzzle completed     | Puzzle type, location                          |
| Key collected        | Item type, progress towards escape             |
| Door opened          | Door location, route taken                     |
| Player spotted       | Location, time, confidence                     |
| Player captured      | Location and circumstances                     |
  
An event might look like:  
```
{
  "event": "player_hiding",
  "location": "storage_room",
  "hiding_place": "locker_2",
  "duration": 12,
  "timestamp": 145
}

```
This data becomes the input to the AI's decision-making process.  
## Stage 2: Memory  
THE BROKER maintains a persistent memory of the player's behaviour within the game.  
This could include:  
```
{
  "player_profile": {
    "running_frequency": 0.8,
    "stealth_frequency": 0.3,
    "preferred_corridor": "east",
    "frequent_hiding_place": "storage_room",
    "distraction_count": 4,
    "capture_count": 1
  }
}

```
These values can be updated continuously as new events occur.  
We should also store individual important events, such as where the player was last discovered and which escape routes have been used successfully.  
This gives THE BROKER two types of memory:  
* Short-term memory: Recent sounds, sightings and interactions.    
* Long-term memory: Behavioural patterns established over the course of the game.    
## Stage 3: Reasoning and decision-making  
THE BROKER uses the incoming events, stored memory and current maze state to decide what to do.  
It could choose between actions such as:  
* PATROL    
* INVESTIGATE_NOISE    
* SEARCH_HIDING_PLACE    
* CHASE_PLAYER    
* INTERCEPT_ROUTE    
* BLOCK_CORRIDOR    
* OPEN_PASSAGE    
* GUARD_OBJECTIVE    
* ACTIVATE_ALARM    
* COMMUNICATE_WITH_PLAYER    
The interesting part is that it should be able to combine several pieces of evidence.  
For example:  
The player has been running, has used the same corridor four times, and has just collected the final keycard.  
THE BROKER might reason that the player is likely to approach the exit through their preferred corridor.  
It could therefore decide to block that passage and guard an alternative route.  
This is substantially more sophisticated than simply moving towards the player's current location.  
## Stage 4: Action  
Once THE BROKER selects an action, the game engine executes it.  
For example:  
```
{
  "action": "BLOCK_CORRIDOR",
  "target": "east_corridor",
  "reason": "Player repeatedly uses this route and is approaching the exit"
}

```
Before executing the action, the engine validates it.  
The game must check that:  
1. The corridor can legally be blocked.    
2. The player is not trapped.    
3. Essential objectives remain reachable.    
4. The action respects the AI's cooldown and resource limits.    
If the action is invalid, the engine rejects it and uses a safe alternative.  
## Stage 5: Adaptation  
After taking an action, THE BROKER observes the outcome.  
Did the player turn around? Did they find another path? Did they escape the interception?  
This information is fed back into the memory system.  
THE BROKER can then update its predictions and choose a different strategy.  
The overall cycle is:  
Perception → Memory → Reasoning → Action → Feedback → Adaptation  
This loop is what makes THE BROKER a reactive AI agent.  
  
## 4. How to Incorporate an Actual AI Model  
I would avoid building the entire enemy using an LLM.  
Language models are not well suited to controlling movement, collisions and animations dozens of times per second.  
Instead, separate THE BROKER into two systems.  
**System A: Real-time game controller**  
This handles:  
* Player and enemy movement.    
* Collision detection.    
* Sound propagation.    
* Pathfinding.    
* Doors and maze connectivity.    
* Immediate pursuit behaviour.    
Use JavaScript or a game engine for these mechanics.  
**System B: AI strategic controller**  
This handles:  
* Interpreting behavioural patterns.    
* Choosing higher-level strategies.    
* Predicting likely player objectives.    
* Deciding when and where to change the maze.    
* Generating personalised dialogue.    
* Updating strategic decisions when new events occur.    
The AI model would receive structured game-state summaries when significant events occur or at controlled intervals.  
It could respond with a structured action:  
```
{
  "strategy": "intercept",
  "target_area": "north_corridor",
  "maze_action": "lock_door",
  "dialogue": "You really are becoming predictable."
}

```
The game's deterministic systems then validate and execute the instruction.  
The model should control strategy, not individual movement frames.  
This is the best way to create an intelligent game that remains responsive and technically reliable.  
  
## 5. Detailed Game Design  
## A. Maze layout  
For the hackathon, I recommend one reasonably complex maze containing five key areas.  

| Area | Purpose | Gameplay |
| ---------------- | ---------------------------------- | ------------------------------- |
| Starting chamber | Introduce movement and environment | Explore, discover clues |
| Storage room | Find tools and first items | Search lockers, hide |
| Generator room | Restore electricity | Find fuse, activate generator |
| Security office | Gain access to restricted areas | Discover code, retrieve keycard |
| Exit chamber | Final escape objective | Unlock and reach exit |
  
These areas should be connected by a network of corridors, junctions and alternative routes.  
The layout should contain enough loops that THE BROKER can redirect players without trapping them.  
For an initial implementation, aim for a maze around 20 × 20 cells, although the exact dimensions can change during testing.  
## B. Player mechanics  
The player should be able to:  
* Walk and run.    
* Explore corridors and rooms.    
* Interact with objects.    
* Collect items.    
* Open doors.    
* Hide in cupboards.    
* Throw objects to create distractions.    
* Solve puzzles.    
* Escape from THE BROKER.    
I recommend starting with a top-down game and adding first-person presentation only if time allows.  
Although first-person gameplay would be more immersive, top-down gameplay makes it considerably easier to implement and demonstrate dynamic maze changes.  
## C. Puzzles and progression  
The player needs to complete three main objectives.  
**Objective 1: Restore power**  
The generator is offline.  
The player must search for a replacement fuse and return it to the generator room.  
Activating the generator creates a loud noise that THE BROKER can investigate.  
**Objective 2: Access the security system**  
The player must discover a security code to unlock the security office.  
The code can be hidden in a document or discovered through environmental clues.  
Accessing the security office reveals information about the facility and allows the player to collect a keycard.  
**Objective 3: Escape**  
The player must reach the exit with the necessary access items.  
THE BROKER identifies that the player is close to escaping and may prioritise guarding key routes.  
The final escape sequence should be the game's most intense moment.  
Importantly, the objectives should allow some flexibility in their completion order, rather than requiring one rigid sequence.  
## D. Enemy encounters  
THE BROKER should have several different states.  

| State         | Behaviour                                      |
| ------------- | ---------------------------------------------- |
| Observing     | Monitors player behaviour and gathers evidence |
| Patrolling    | Moves between strategic locations              |
| Investigating | Travels towards a sound or suspected location  |
| Searching     | Checks rooms and hiding places                 |
| Pursuing      | Chases a player whose position is known        |
| Intercepting  | Attempts to reach a predicted future location  |
| Manipulating  | Changes the maze to influence player movement  |
  
These states should be selected dynamically based on the agent's goals and observations.  
## E. Capture system  
Keep the three-life system from your original document.  
First capture: The player wakes up in another room. THE BROKER remembers where and how it caught them.  
Second capture: The player returns with fewer resources or faces increased surveillance.  
Third capture: Game over.  
The important feature is that THE BROKER's behavioural memory persists between captures.  
This makes each encounter different and creates a feeling that the AI is becoming more dangerous.  
  
## 6. Technology Stack  
For a short hackathon, I would recommend the following.  

| Component | Recommended technology | Reason |
| --------------- | ----------------------------------- | ---------------------------------------------- |
| Frontend | HTML, CSS, JavaScript | Fast to develop and easy to demonstrate |
| Game rendering | Phaser 3 | Designed for browser-based 2D games |
| Agent backend | Python + FastAPI | Convenient for AI integration |
| AI model | OpenAI API | High-level reasoning and personalised dialogue |
| Pathfinding | A* or BFS | Enemy navigation and maze reachability |
| Memory | In-memory JavaScript/Python objects | Simple and sufficient for one game session |
| Maze generation | DFS maze generation | Produces varied procedural layouts |
| Deployment | Static frontend + hosted backend | Accessible browser demonstration |
  
A traditional database is unnecessary for the minimum viable product.  
If we later want to store profiles across multiple sessions, we can introduce SQLite or PostgreSQL.  
**Why this stack?**  
It offers a good balance between technical sophistication and realistic hackathon implementation.  
The sophisticated part of the project is the agent's decision-making, not the graphics engine or database infrastructure.  
  
## 7. Implementation Roadmap  
I'll assume a 24-hour hackathon and prioritise having a complete, playable project before adding more ambitious functionality.  
## Phase 1 — Build the maze game  
Hours 0–5  
Goal: Create a playable game with working objectives.  
Tasks:  
1. Set up the project structure.    
2. Create the maze grid.    
3. Generate connected corridors.    
4. Add player movement.    
5. Add collision detection.    
6. Implement doors and interactive objects.    
7. Implement the inventory.    
8. Add the three essential objectives.    
9. Add the exit and winning condition.    
10. Add the timer and three-life system.    
Deliverable: A player can explore the maze, complete puzzles and escape, even without a functioning AI enemy.  
## Phase 2 — Build the reactive agent  
Hours 5–9  
Goal: THE BROKER observes the player and reacts intelligently.  
Tasks:  
1. Create a structured game-event system.    
2. Record movement, noise, hiding and item collection.    
3. Implement an agent memory structure.    
4. Track frequently used corridors.    
5. Track hiding preferences.    
6. Implement enemy movement and pathfinding.    
7. Add patrol, investigate, search and chase behaviours.    
8. Add decisions based on multiple player events.    
Deliverable: Different player behaviours produce noticeably different responses.  
## Phase 3 — Make the maze dynamic  
Hours 9–12  
Goal: THE BROKER can alter the maze in response to player behaviour.  
Tasks:  
1. Implement lockable security doors.    
2. Add corridors that can open or close.    
3. Give THE BROKER permission to request structural changes.    
4. Add maze reachability checks using BFS.    
5. Prevent impossible layouts.    
6. Add cooldowns between structural changes.    
7. Connect maze manipulation to the agent's memory.    
Deliverable: THE BROKER can strategically alter the player's available routes.  
This is one of the highest-priority features for the finished demonstration.  
## Phase 4 — Integrate AI model reasoning  
Hours 12–15  
Goal: Connect THE BROKER's strategic decisions to an actual language model.  
Tasks:  
1. Create a Python FastAPI backend.    
2. Define a structured state summary.    
3. Define the permitted agent actions.    
4. Connect the backend to the AI model.    
5. Request strategic decisions after significant events.    
6. Parse and validate model responses.    
7. Execute valid actions through the game engine.    
8. Add fallback behaviour if the model is unavailable.    
9. Display the model's chosen strategy and relevant evidence in a developer panel.    
Deliverable: THE BROKER makes higher-level strategy decisions using an AI model, rather than relying exclusively on predefined rules.  
## Phase 5 — Improve atmosphere and presentation  
Hours 15–18  
Goal: Turn the functional prototype into an immersive horror game.  
Tasks:  
1. Improve lighting and environmental colours.    
2. Add warning lights when THE BROKER approaches.    
3. Add sound effects for movement, doors and alarms.    
4. Add computer terminals and security-camera visuals.    
5. Give THE BROKER contextual dialogue.    
6. Improve the inventory interface.    
7. Add an introductory screen and game instructions.    
8. Create a memorable escape sequence.    
Deliverable: A game that looks polished and creates tension.  
## Phase 6 — Testing and improvement  
Hours 18–21  
Goal: Ensure the AI is reliable, fair and genuinely adaptive.  
Test the following situations:  

| Test | Expected result |
| ----------------------------------- | ------------------------------------------------------ |
| Player repeatedly runs | THE BROKER investigates noisy locations |
| Player frequently hides | THE BROKER searches hiding places more often |
| Player uses the same corridor | THE BROKER may block or intercept that route |
| Player throws an object | THE BROKER responds to its landing location |
| Player repeatedly uses distractions | THE BROKER updates its assessment of suspicious sounds |
| Player collects final key | THE BROKER becomes more focused on escape routes |
| THE BROKER closes a door | The player can still reach required objectives |
| AI API becomes unavailable | Local fallback keeps the game running |
  
Ideally, compare multiple playthroughs with the same maze seed to demonstrate that different behaviour leads to different outcomes.  
Deliverable: A stable game with clear evidence of adaptive behaviour.  
## Phase 7 — Prepare the final demonstration  
Hours 21–24  
Goal: Demonstrate the project in a way that clearly highlights the reactive AI.  
Tasks:  
1. Create a presentation.    
2. Record or prepare a reliable live gameplay demonstration.    
3. Explain the agent architecture.    
4. Show how memory affects decisions.    
5. Demonstrate dynamic maze manipulation.    
6. Compare two different player behaviours.    
7. Explain technical challenges and solutions.    
8. Prepare a short final pitch.    
Deliverable: A polished submission that clearly fulfils the hackathon prompt.  
  
## 8. The Most Important Demonstration: Two Players, One Maze  
I would make this the centrepiece of the judging presentation.  
Start two games using the same maze layout.  
**Player A: Aggressive playstyle**  
The player runs through corridors, rarely hides and repeatedly approaches objectives directly.  
THE BROKER observes:  
* High running frequency.    
* High noise levels.    
* Repeated corridor usage.    
* Direct movement towards objectives.    
It responds by:  
* Investigating noise.    
* Predicting the player's likely route.    
* Blocking strategic corridors.    
* Attempting to intercept the player.    
**Player B: Stealth playstyle**  
The player walks quietly, hides frequently and uses objects to distract the AI.  
THE BROKER observes:  
* Low movement noise.    
* Frequent hiding.    
* Repeated distractions.    
* Less predictable movement routes.    
It responds by:  
* Searching cupboards.    
* Monitoring previously used hiding locations.    
* Becoming more cautious about distractions.    
* Focusing on likely hiding areas rather than chasing noise.    
**Why this demonstration is effective**  
The judges can see that identical starting conditions produce different gameplay based on the agent's observations and memory.  
We can reinforce this with a small developer panel displaying:  
* Recent events received.    
* Current behavioural profile.    
* Agent's selected goal.    
* Action chosen.    
* Outcome of the previous action.    
This makes the AI's adaptation verifiable instead of merely claiming that the enemy is intelligent.  
  
## 9. Extra Features That Could Make the Project Stand Out  
These are optional extensions. I would only implement them once the core game and AI agent are reliable.  
**A. AI-generated psychological dialogue**  
THE BROKER remembers specific player actions and references them.  
For example:  
* "You hid in that cupboard last time."    
* "Still using the eastern corridor?"    
* "I know what you're looking for."    
* "You think that noise will distract me again?"    
Dialogue should be generated from the AI's actual memory, not randomly selected lines.  
**B. Adaptive difficulty**  
THE BROKER can change its aggression depending on how well the player is performing.  
For example, a struggling player may receive slightly more time to react, while a successful player faces stronger interception strategies.  
This needs limits to keep the game fair.  
**C. AI confidence and uncertainty**  
Give THE BROKER a confidence score for its predictions.  
For example:  
* 85% confidence that the player favours the eastern corridor.    
* 60% confidence that the player is hiding.    
* 30% confidence that a noise represents the player's actual location.    
An uncertain agent should investigate rather than instantly know where the player is.  
This would make the intelligence more believable and also strengthen the technical explanation.  
**D. A deception score**  
Track how often the player successfully misleads THE BROKER.  
For example, the player creates a noise in one corridor, causing THE BROKER to investigate while they escape through another.  
This could be shown at the end of the game alongside survival time and capture count.  
**E. Explainable AI dashboard**  
This is particularly valuable for a hackathon.  
The developer dashboard could display something like:  
```
THE BROKER — LIVE AGENT STATUS

Current Strategy: INTERCEPT

Recent Observations:
- Player visited East Corridor 5 times
- Player collected the security keycard
- Player ran during the last 3 movements

Memory:
- Preferred Route: East
- Running Tendency: High
- Hiding Tendency: Low

Prediction:
Player likely approaching eastern exit passage

Decision:
Seal East Corridor Door 2

Outcome:
Player changed direction
Prediction confidence decreased

```
This makes the agent's behaviour easy to understand and demonstrates the underlying technical sophistication.  
  
## 10. What We Should Prioritise to Maximise Our Chances of Winning  
A winning project does not need dozens of features. It needs a compelling idea, convincing technical execution and a memorable demonstration.  
I'd prioritise the following:  

| Priority | Feature | Importance |
| -------- | ------------------------------------- | ----------------------------------------- |
| 1 | Working, playable maze | Essential |
| 2 | Agent receives live game events | Essential |
| 3 | Behavioural memory | Essential |
| 4 | Adaptive enemy strategies | Essential |
| 5 | AI-controlled maze changes | Essential |
| 6 | Model-powered strategic decisions | Essential to our proposed AI architecture |
| 7 | Developer dashboard showing decisions | Very high |
| 8 | Ability to deceive the AI | Very high |
| 9 | Horror atmosphere and sound | High |
| 10 | Advanced first-person graphics | Optional |
  
I would rather have a polished top-down game with exceptional AI than an impressive-looking first-person environment with simplistic enemy behaviour.  
  
## 11. How We'll Evaluate Whether the AI Is Actually Intelligent  
It's worth defining success criteria before implementing the agent.  

| Evaluation | What we'd measure |
| ------------------------ | -------------------------------------------------------------------------- |
| Reactivity | Delay between player event and appropriate AI response |
| Behavioural adaptation | Whether decisions differ after different player histories |
| Memory | Whether previous events influence later actions |
| Strategic ability | Whether THE BROKER predicts routes and intercepts rather than only chasing |
| Environment manipulation | Whether maze changes reflect the agent's objective |
| Fairness | Percentage of maze changes that preserve reachability |
| Reliability | Whether gameplay continues when AI requests fail |
| Replayability | How differently two playstyles experience the same level |
  
I'd also perform an important experiment: compare THE BROKER with its adaptive memory enabled and disabled.  
If the memory-enabled version produces meaningful strategic differences, we have stronger evidence that our agent is truly adaptive.  
  
## 12. The Final Hackathon Pitch  
**Project title**  
THE VAULT: OUTBREAK — The Maze That Learns You  
**Tagline**  
You can escape the maze. But can you escape something that learns how you think?  
**Pitch**  
Most horror games rely on enemies with predictable patrol routes and scripted behaviour. Once players understand these patterns, they can exploit them.  
We wanted to create something different.  
THE VAULT: OUTBREAK is a survival-horror maze controlled by a reactive AI agent called THE BROKER.  
Rather than following fixed instructions, THE BROKER continuously observes the player's actions, builds a behavioural memory and uses this information to predict what the player will do next.  
It can investigate sounds, learn hiding preferences, anticipate escape routes and dynamically restructure the maze.  
But there's a twist: players can deliberately manipulate the AI's predictions, creating a strategic battle between human and machine.  
The result is a game where the environment is not simply an obstacle. It's an intelligent adversary that evolves in response to every decision you make.  
In most games, you learn the maze. In ours, the maze learns you.  
