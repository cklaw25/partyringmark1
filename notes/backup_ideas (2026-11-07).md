# WHACK 26 backup ideas (7 Nov 2026, written before the problem statements were revealed)

Rules every idea follows (from the intel notes):
- A familiar format (game / pet / sim / physical gadget) attached to the sponsor's real business. No plain dashboards or quiz apps.
- Relatable to the sponsor AND a general audience, so the same build can also win Most Creative / WHACKiest.
- Technically real underneath: own logic, maths, sim or hardware does the work, and any LLM is just one part.
- Hardware where it helps (ESP32 + servo). Judges Jakub and Sam both like hardware and sensors.
- Name the sponsor's product in the pitch. Live demo > slides. Show a measured number.

## Capital One: credit / financial literacy
### C1. "Day Zero": arrive in the UK with no credit file
- Format: a Reigns-style swipe-card life sim. You play an international student landing in the UK, and each card is a real decision (register on the electoral roll? phone contract? credit-builder card? pay in full or minimum?). The game covers 24 months.
- Real part: your own credit-score engine built on real UK factors (electoral roll, utilisation under 30%, on-time payments, hard searches, account age). It simulates month by month and shows WHY the score moved.
- Hardware twist (optional): an ESP32 + servo "credit gauge" needle on the desk that swings live as you play.
- Why it wins: it's Capital One UK's actual customer (thin file, credit-builder card). Your own story. A game format, not a quiz.
- MVP: 15-20 cards + scoring engine + end screen "your score after 2 years + 3 things that mattered".
### C2. "Thirty": a desk pet that lives on your utilisation
- Format: a physical ESP32 pet (OLED face + servo ears). It's happy while your card utilisation is under 30%, sad over it, and grows when you pay on time. A phone app logs spending.
- Real part: hardware + a sim of the statement cycle (statement date vs due date, which is the thing most people don't know).
- Risk: looks like the 2024 Pet PayDay winner. Only pick it if the twist (statement-date timing) is strong.

## Brevan Howard: information accuracy / decisions under uncertainty
### B1. "AI Polygraph"
- Format: a physical lie-detector. Paste a claim, headline or AI answer. Several LLMs answer independently, and the claim is split into checkable sub-claims and checked against sources. A servo needle on paper (like a real polygraph) scribbles the disagreement live.
- Real part: claim decomposition + agreement scoring (your own maths) + a source cross-check, not just "ask GPT if it's true".
- Why it wins: it builds on the 2025 winner's best idea (AI-vs-AI disagreement) and makes it physical and memorable, which also suits Most Creative.
### B2. "Pundit League"
- Format: a football league table for forecasters (economists, news outlets, AI models). It scores past predictions against what actually happened.
- Real part: proper scoring (Brier score, calibration curve: "when they say 80% sure, are they right 80% of the time?"). A new claim shows the source's track record.
- Why it wins: it's the macro trader's real question, "whose forecast do I trust?". It also echoes Marshall Wace's TOPS (scoring contributors), so it could switch tracks.

## Marshall Wace: abstract theme (Chain of events / Bridging Worlds / Emergent Behaviour style)
### M1. "Crowd Exchange"
- Format: a mini stock market of AI agents (some trend-followers, some panic sellers, some "analysts" fed live headlines) trading on YOUR OWN order book / matching engine. Bubbles and crashes emerge on their own. The audience can inject news and watch the chain reaction.
- Real part: the matching engine (Edward and Jakub both built these, so they'll know the difference) + an agent sim. Report a measured number (orders/sec).
- Hardware twist: an ESP32 ticker / servo price needle, or a big red "inject rumour" button.
- Fits any theme: emergent behaviour (crashes), chain of events (one rumour cascading), bridging worlds (news -> prices), TOPS (crowd signals).
### M2. "Mirror": a hand-moved arm copied by a second arm over Wi-Fi
- Bridging worlds / human + machine. Builds on your Wireless Mirror Arm plan. Use a 2-servo pan-tilt version to keep it doable in 24h.
- Risk: a hardware time sink. Only pick it if the parts are already in your bag.

## incident.io: constantly changing information (creative, off-domain reading wins)
### I1. "Plan B": incident response for your trip
- Format: treat a group trip like a production system. It watches live train data, declares an "incident" when your train is cancelled, assigns roles to friends ("you rebook, you message the host"), shows a status page, and writes a post-mortem afterwards.
- Real part: a live data feed + an incident state machine + an auto-written timeline. It uses incident.io's own vocabulary (declare, roles, status page, post-mortem) on real life.
- Why it wins: relatable to everyone (the trains to Warwick this morning!). Creative reading. Reuses your Cathay disruption idea.
### I2. "Code Blue": on-call paging for patient vitals
- Format: ESP32 pulse/SpO2 sensors on "patients" (teammates). Alerts get de-duplicated and escalated like on-call paging. An AI writes the nurse handover from the incident timeline.
- Real part: sensor signal processing + alert logic (thresholds, flapping suppression, escalation). Biomed story = yours.
- Risk: the KY-039 sensor was noisy. Use the MAX30102 if you have it, or fake the sensor stream as a fallback.

## Extras
- 1 Hour Hackathon (22:30): one person does something small and silly on that prompt while the others keep building.
- Costume: cheap, and in 2024 the best-dressed team also won a track.

## Top picks (if the prompts match past years)
1. M1 Crowd Exchange (the judges' sweet spot: matching engine + emergent sim + optional hardware)
2. B1 AI Polygraph (strongest Most Creative overlap)
3. C1 Day Zero (most relatable + your own story)
4. I1 Plan B (most relatable for incident.io)

## Timeline for any idea
- 11:00-12:00 lock the idea + split roles. Core logic working by 18:00. Full MVP by 23:00.
- Polish + demo script overnight. Feature freeze 08:00. Devpost images + 20-30s video 09:00-10:30. Submit before 11:00.
