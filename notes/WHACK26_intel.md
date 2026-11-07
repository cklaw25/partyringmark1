# WHACK 26 Intel Pack (researched 6 Nov 2026)

## 0. Event basics
- **When:** Sat 7 to Sun 8 Nov 2026. Arrivals 09:00–09:45, opening at 10:00 (OC0.03). Hacking runs 11:00 Sat to **11:00 Sun**, when submissions close. Judging starts 12:30 and the closing ceremony is at 17:00.
- **Where:** Oculus Building, University of Warwick, Coventry. From London: Euston to Coventry is about 1h, then about 20 min by bus or taxi to campus. **Leave by ~07:30.**
- **Format:** 24 hours, teams of up to 4, prize pool over £6,000 (it was £4,590 in 2025 and £3,200 in 2024).
- **How winning works (same both past years):** each title sponsor gives a problem statement. **Your team picks ONE**, the organisers record your choice during the event, and you submit on Devpost "with plenty of images and your team name". You then get a **5-minute slot to present live to that sponsor**, in any format: live demo, slides, whatever works. **A video is not required. A live demo is what gets judged.**
- **Judges:** reps from each title sponsor (no individuals named on Devpost) plus the **WHACK Lead Organisers** (most likely for the side prizes).
- Run by UWCS + Warwick AI (WAI) + CodeSoc. Contact: contact@warwickhack.co.uk. The 2025 Discord was https://discord.gg/yR7BFzMfvM (could be the same server this year). Socials: instagram.com/warwickhacks, linkedin.com/company/warwickhacks

### Schedule (provisional)
| Time | Day 1 (Sat) | Time | Day 2 (Sun) |
|---|---|---|---|
| 09:00 | Arrivals | 07:00 | Breakfast |
| 10:00 | Opening (problem statements revealed) | **11:00** | **Submissions close** |
| 11:00 | Hacking begins | 11:30 | Lunch |
| 12:00 | Lunch | 12:30 | Judging begins |
| 13:00 | **Brevan Howard talk** | 17:00 | Closing ceremony |
| 14:00 | **Marshall Wace talk** | | |
| 15:00 | **incident.io talk** | | |
| 16:00 | **Capital One talk** | | |
| 17:00 | **Speed networking** (careers!) | | |
| 18:00 | Switch tournament | | |
| 19:30 | Dinner | | |
| 21:00 | IRL CTF | | |
| 22:30 | **1 Hour Hackathon** (separate prize) | | |

---

## 1. Sponsors 2026, plus what each one is likely to ask for

**Gold (title tracks, about £1k+ each, likely 4 tracks):** Brevan Howard, Marshall Wace (NEW), Capital One, incident.io
**Silver:** Bloomberg (NEW), Netcraft
**Past sponsors:** Coventry City FC, Fetch.ai, Ab Initio, Logicalis, Arm, IMC

### Capital One. Strongest pattern of any sponsor.
- **Who they are:** US bank. Capital One UK (HQ Nottingham, plus London) is a top-10 UK card provider. Their core UK product is **credit-builder cards for people with thin or no credit history**, and they talk a lot about "vulnerable customers". Hires through the Tech Graduate programme and Summer Internship (jobs.capitalone.co.uk/earlycareers).
- **Their past prompts:** **financial literacy / credit, 3 hackathons in a row.**
  - WHACK 2024 theme: "Invest in your future" (financial literacy).
  - WHACK 2025: "help improve financial literacy" for customers.
  - HackNotts 25: Capital One prize went to **SmartCreditCoach** (credit score simulator + quizzes).
- **What won:**
  - 2024: **Giddy Up, Glitch / "Pet PayDay"**. A Tamagotchi/Pou-style pet game for kids where you save money, with savings interest and a mini stock market. Built in PyGame with all-original art. https://devpost.com/software/giddy-up-glitch
  - 2025: **Snackonomics**. Snap a photo of your meal, get a cheaper/healthier swap, and the money you save goes towards a credit goal (pay off card, emergency fund), all gamified. Built in Flutter + Firebase. https://devpost.com/software/snackonomics
- **Insight:** this track gets the most entries (about 15+ fin-lit quiz apps in 2025 alone, e.g. Duolingo clones, Credit Curator, Savvify). The generic "quiz + dashboard" apps all lost. Both winners **took an everyday habit or a game format people already love and attached real money behaviour to it**. They were charming and visual, not dashboards.
- **Angle nobody has used yet:** credit-building for **people new to the UK credit system** (international students, new migrants). That is exactly Capital One UK's customer base, and you're an international student yourself, so the story is authentic and relatable to the judges.

### Brevan Howard
- **Who they are:** global macro and digital assets hedge fund (BH Digital = crypto, also does VC). Technology-heavy. Internships cover AI & Quant, Systematic Trading, Systematic Data Engineering, Macro Trading, Risk, and VC.
- **2025 prompt (exact wording, quoted by a team):** *"Accurate information is really important for making decisions. Create something that helps understand information accuracy, for example social media posts, news headlines, economic predictions, AI responses, etc."*
- **2025 winner: Whackyyy: Information Accuracy Dashboard.** Python + Streamlit, 4 features. "Trust the Bot?" compares answers from several LLMs and highlights where they agree or contradict. An article analyser highlights opinion vs fact. "Opposing stories" are generated from a neutral fact. There was also a financial sentiment dashboard. https://devpost.com/software/whackyyy-information-accuracy-dashboard
- **Losers in that track:** CheckMate, Clarity, Metra, Kachow, Deceptify, ClearReality, Infgest. They were mostly single-feature fake-news detectors. The winner was broader (several angles on "accuracy") and included **AI-vs-AI disagreement**, which was the fresh idea.
- **Prediction for 2026:** something about decisions under uncertainty, such as signal vs noise, information, AI reliability, markets/macro, or crypto. It will be framed broadly so non-finance people can enter.

### Marshall Wace (NEW at WHACK). Has the most consistent pattern.
- **Who they are:** London equity long/short hedge fund, about $85bn AUM. Known for **TOPS** (crowdsourcing trade ideas from broker analysts) and systematic/quant (Eureka). Hires through the Technology Internship and an 18–24 month Technology Graduate Programme. They describe their hackathon sponsorships as focused on "Algorithmic Trading or Computing".
- **They ALWAYS set an abstract one-phrase theme:**
  - DurHack 2024: **"Chain of events"** (suggestions: game with consequences, supply chains, high-throughput data pipeline). Winner: **Pipeline of Peril**, a software Rube Goldberg machine that sends data via Gmail → Morse code on a servo → Discord bots → QR codes → Google Keep → Chrome extension, with ESP32 + Arduino + Minecraft. They also sponsored "Funniest Hack", won by *None of Your Business* (Godot game).
  - DurHack 2025: **"Bridging Worlds"** (map data between domains, interface between independent systems, human-AI collaboration). Winner: **ORION**, a crystal orb that makes predictions by comparing your data with "similar" known data, sometimes in wildly loose ways. Built with Blender, FastAPI, Svelte.
  - IC Hack 26: **"Emergent Behaviour"**. Winner: **Digital Twin**, which makes an AI twin of you and simulates an event with up to 7 Claude agents to predict whether you'd enjoy it. Honourable mention: ERLA, a recursive research-agent swarm with hallucination detection.
- **Insight:** they reward **playful, weird and technically genuine** work, and **hardware/physical chaos has already won** (ESP32 + servo). Multi-agent simulation won the most recent one. Expect another abstract theme. **Your ESP32/servo/hardware background fits this sponsor well.**

### incident.io
- **Who they are:** London startup for incident response (on-call, status pages, Slack-native incident management). Founded by ex-Monzo engineers Stephen Whitworth, Chris Evans and Pete Hamilton. Raised a $62m Series B (Apr 2025). Their flagship push is **"AI SRE"**: AI agents that investigate outages, triage alerts, read recent code changes and draft post-mortems (launched July 2025, led by Lawrence Jones). Strong "product engineer" and polish culture.
- **2025 prompt (inferred, exact text not found):** something about keeping up with information that **constantly evolves in real time**. Several teams quoted "ever-evolving" / "constantly evolving".
- **2025 winner: CharacterCompass.** An AI tool for **authors** that keeps character profiles (personality, motives, relationships) updated as the story is written, and lets you chat with your characters. Built with Flutter + LangChain + OpenAI + SQLite. https://devpost.com/software/charactercompass
- **Insight:** they **rewarded a creative, off-domain reading of the prompt** (novel-writing, not DevOps). Meanwhile the most on-the-nose entry, an SRE voice agent on Discord, won the **1 Hour Hackathon**, not their track. A clever reframe beats a literal answer.
- Other 2025 entry on that theme: Deadline Edger, an evolving schedule manager.

### Bloomberg (silver, probably no title track)
- **Who they are:** Terminal, data and news. Big London engineering office (SWE internship + grad roles), with a strong puzzle culture (Bpuzzled, the Global Trading Challenge). Probably here for talks, booth and networking, maybe a small prize. **Mainly a careers target.**

### Netcraft (silver, event sponsor since 2025)
- **Who they are:** Bath-based (plus London) cybercrime disruption company: phishing detection and takedowns, internet data/surveys. Long record of rewarding UK CS students (top-10 2nd-year prizes, AQA A-level award, Best of Britain programming competition). **Probably tied to the IRL CTF at 21:00.** A careers target if you're interested in security.

---

## 2. Every WHACK winner (with Devpost links)

### WHACK 2025 (152 participants). Devpost: https://whack-2025.devpost.com/ · gallery: https://whack-2025.devpost.com/project-gallery
| Prize | Project | What it does | Stack |
|---|---|---|---|
| Capital One Title (£1k + tablets) | [Snackonomics](https://devpost.com/software/snackonomics) | Meal photo → cheaper swap → savings put towards credit goals, gamified | Flutter, Firebase |
| Brevan Howard Title (£1k + AirPods Pro 3) | [Whackyyy: Info Accuracy Dashboard](https://devpost.com/software/whackyyy-information-accuracy-dashboard) | LLM disagreement checker, fact vs opinion annotation, sentiment | Python, Streamlit |
| incident.io Title (£1k + Polaroids) | [CharacterCompass](https://devpost.com/software/charactercompass) | Live-updating character profiles for authors + chat with characters | Flutter, LangChain, OpenAI |
| WHACKiest Hack (£600) | [Finance Clicker](https://devpost.com/software/finance-clicker) | Cookie-Clicker-style life sim that teaches finance jargon | Vanilla JS |
| Most Creative (£600 + Bose) | [WHACKMOPOLY](https://devpost.com/software/whackmopoly) | **AR Monopoly about credit** on a **3D-printed physical board**, viewed through a phone camera | React, A-Frame, AR.js |
| Best Solo (£200 + Flipper Zero) | [PINN-acle](https://devpost.com/software/isaac-mortiboy) | Physics-informed NNs for options volatility surfaces + news sentiment + Gemini explanations | PyTorch, Streamlit |
| 1 Hour Hackathon (£150 + monitor) | [KB-Assisted SRE Agent](https://devpost.com/software/knowledge-base-assissted-sre-conversational-agent) | Discord voice/text SRE agent with a RAG knowledge base | Go, Whisper, Kokoro TTS |
| Best Costume (£40) | [Kachow](https://devpost.com/software/kachow) | News sentiment analyser (Lightning McQueen costume theme) | Flask, OpenAI |

### WHACK 2024 (95 participants). Devpost: https://ccfc-c1-fai-whack.devpost.com/ · gallery: https://ccfc-c1-fai-whack.devpost.com/project-gallery
| Prize | Project | What it does |
|---|---|---|
| Coventry City FC Title | [Phantom Hackers](https://devpost.com/software/phantom-hackers) | Football match-data dashboard + AI match digest for coaches |
| Capital One Title | [Giddy Up, Glitch (Pet PayDay)](https://devpost.com/software/giddy-up-glitch) | Virtual-pet savings game for kids, PyGame |
| Fetch.ai 1st | [Vyasylum](https://devpost.com/software/safe-drive-focused-journeys) (solo) | Drowsiness detection with OpenCV + agents + calming audio |
| Fetch.ai 2nd | [camelCase / Crisis Compass](https://devpost.com/software/crisis-compass-9d02yw) | Disaster aid matching on a map |
| Fetch.ai 3rd + Best Dressed | [[NULL]](https://devpost.com/software/null-bx9qh3) | Crash detection from g-force + heart rate, calls 999, Unity |
| Most Creative | [Money Mentor](https://devpost.com/software/money-mentor-sl3e76) | 2D RPG where you act as a financial advisor to NPCs, Phaser |
| Best Solo | [Optimal Starting XI](https://devpost.com/software/optimal-starting-xi-generator) | Picks Coventry City's line-up from FotMob data |

Note: **Isaac Mortiboy won Best Solo two years running.** Also, the same people (jonathanlu2005, toluwalase104) were on camelCase in 2024 and **Snackonomics in 2025**, so repeat winners come back.

---

## 3. Judges and organisers: background check
Sponsor judges are **not named publicly**, so expect early-careers / grad-recruiting engineers. **Find their names at the 13:00–16:00 talks and the 17:00 speed networking**, because those same people will probably be judging you.

**WHACK Lead Organisers (judges for the side prizes):**
- **Sam Turner** (github.com/stmio, stmio.net). Google (per GitHub). Rust, Go, LeetCode. A clean-engineering type.
- **Edward O'Dwyer** (github.com/edwardodp). Built a chess-engine framework and competition, an **order book + matching engine in 90 min (Java)**, crowd-flow sim, Svelte. Quant/trading-minded.
- **Jakub Kisielewski** (github.com/Jakub-Kisielewski). "Building knowledge in low-latency engineering and finance", Warwick CS 2027, at **BGC Group**. Has hacked at **OxHack26 (same event as you!)** and HackNotts25. Poker bot for WAI, order-matching system. **Something to bond over.**
- **Founders/advisors:** Louis Tanak (won the **HRT track at IC Hack 26**, lock-free data structures, matching engine) and Joshua Fitzmaurice.
- **Read:** the organisers are **quant/trading-leaning Warwick CS students**. They will appreciate genuine technical depth wrapped in fun (see what won WHACKiest and Most Creative). They also clearly reward personality: a costume prize, karaoke, Switch tournament.

---

## 4. Ways to win (ranked by effort vs payoff)
1. **One gold track (£1k-ish + 4x gadgets).** Pick ONE of Capital One / Brevan Howard / Marshall Wace / incident.io.
2. **Most Creative (£600) / WHACKiest (£600).** These cut across tracks: your track project is still eligible. Physical + AR (WHACKMOPOLY) and silly-but-polished (Finance Clicker) won them.
3. **1 Hour Hackathon (22:30, about £150 + monitor).** A **separate, free extra shot.** One person can do it while the rest keep building.
4. **Best Costume (£40/chocolates).** Just dress up. In 2024 the Best Dressed team ALSO won a track.
5. **Best Solo**, only if you're going solo.
6. Possibly Netcraft/Bloomberg side prizes and the CTF. Confirm at the opening ceremony.

## 5. Strategy notes (your lessons from past hackathons + this research)
- **Overlap:** pick an idea that is relatable to the sponsor judge AND to a general audience, so the same build can win its track and Most Creative/WHACKiest.
- **Pattern across all past winners:** take a familiar format (pet game, snack photo, Monopoly, clicker, crystal ball, Rube Goldberg machine), attach it to the sponsor's domain, and have a **working live demo**. Generic dashboards and quiz apps lose.
- **Your unfair advantages:** hardware (ESP32, servos, sensors), AR/Unity, game building (EduOasis), fintech framing (ClearLane), biomed. Almost nobody brings physical hardware, and hardware has won both Marshall Wace (Pipeline of Peril) and Most Creative (WHACKMOPOLY). **Pack an ESP32 + servo + sensors + cables in your bag.**
- **Presentation = 5 min live to the sponsor.** Rehearse a 60-second hook → live demo → "why this matters to *your* customers/firm". Name the sponsor's real product or business in the pitch (Capital One credit-builder cards, incident.io AI SRE, Marshall Wace TOPS / crowd signals, BH macro/digital assets).
- **Devpost:** "plenty of images" is required. **Suli's submission-video tool + Higgsfield** can give you a 20–30s hero video for Devpost and the first slide. It's a backup if the live demo breaks, and it adds polish. **Don't spend more than 30 min on it.** Generate it around 09:00–10:30 Sunday, before the 11:00 cutoff.
- Submissions close **11:00 Sunday**. Aim for feature-freeze at 08:00.

## 6. Careers: who to talk to and what to ask
Bring the **quant CV** (resume creating\(gresearch) Memphis_Law_CV v2.pdf) plus the main CV, printed or as a QR code. Use the **17:00 speed networking** and catch speakers right after their talks.
- **Marshall Wace:** Technology Internship / Grad Programme. Ask: "How does tech support TOPS and the systematic side?" and "What do tech interns actually ship?"
- **Brevan Howard:** AI & Quant / Systematic Data Engineering internships. Ask: "How is AI used on the macro desk vs BH Digital?"
- **Capital One:** Tech Summer Internship + Grad (Nottingham/London). Ask: "How do you build products for thin-credit-file customers?" Pitch your project to them.
- **incident.io:** startup, product engineering. Ask about the AI SRE team, and whether they hire interns/new grads.
- **Bloomberg:** SWE internship. Ask about the Terminal/data teams in London.
- **Netcraft:** security/phishing. Ask about grad SWE roles in Bath/London.
- Get names + LinkedIn and **connect the same evening** with a one-line follow-up ("we met at WHACK, I built X for your track").

## 7. Sources
- https://warwickhack.co.uk/ · WHACK 2025 site: https://whack-site-8j0.pages.dev
- https://whack-2025.devpost.com/ · https://ccfc-c1-fai-whack.devpost.com/ · https://fetch.ai/events/whack-2024
- Marshall Wace tracks: https://durhack-2024.devpost.com/ · https://durhack-2025.devpost.com/ · https://ic-hack-26.devpost.com/
- Capital One at HackNotts: https://hacknotts-25.devpost.com/ (winner https://devpost.com/software/smartcreditcoach)
- MW winners: https://devpost.com/software/pipeline-of-peril · https://devpost.com/software/orion-efom8k · https://devpost.com/software/eventtwin
- BH prompt quoted in: https://devpost.com/software/clearreality
- incident.io AI SRE: https://incident.io/blog/introducing-ai-sre
- MW student events: https://mwam.com/student-events

---

## 8. Deeper company + judge profiles (added 7 Nov 2026, event morning)

### Companies
- **Capital One:** US bank and card issuer that sees itself as a tech company. Bought Discover in May 2025, so it now owns a card network too. Capital One Europe is based in Nottingham (plus London). Its main UK product is credit-builder cards for thin or patchy credit files, at a high APR (about 30-50%). Applicants with NO UK credit history are often declined, which is a real gap for international students and new arrivals. Pitch by naming the credit-builder card and the thin-file problem, and tell your own story.
- **Brevan Howard:** global macro hedge fund (rates, FX, bonds, central banks), about $33-34bn AUM (estimates vary). Alan Howard stepped back in 2019, and Aron Landy (former chief risk officer) runs it. Growing in the Middle East. BH Digital = the crypto arm (launched Sept 2021, 50+ staff, about $1bn+ multi-strategy fund plus VC stakes). 2025 performance lagged, and the FG Fund is closing by mid-2026. Pitch framing: "which signal do I trust before I put money on it?"
- **Marshall Wace:** London equity long/short fund, about $80bn+ AUM (range $65-85bn), founded 1997 by Paul Marshall and Ian Wace. TOPS (2002) = the first "alpha capture" system: broker analysts submit ideas, and the system scores each contributor's track record and turns ideas into trades, i.e. crowdsourced signals with learned trust scores. Eureka = fundamental, human sector-specialist side. Motto: "man plus machine beats a machine". Pitch by mentioning TOPS for crowd, emergent-signal or human+AI ideas.
- **incident.io:** London startup founded by ex-Monzo engineers. Handles outages: on-call paging, Slack war-room, status pages, post-mortems. $62m Series B (Apr 2025, Insight Partners), about $400m valuation, about $96m raised in total. Customers include Netflix, Linear, Ramp, Etsy and (reportedly) OpenAI. Rival = PagerDuty. AI SRE / "Investigations" is powered by the Nexus engine, which models systems, teams and incident history and reads telemetry, deploys and code. Culture = product polish.
- **Bloomberg:** Terminal (about $30k/seat/yr), news and data. Big London engineering office (City). Careers target.
- **Netcraft:** Bath-based. Finds and takes down phishing sites, fake shops and malware, and runs the web server survey. Likely linked to the 21:00 CTF.

### Judges (lead organisers, likely judging the side prizes; sponsor track judges are unnamed, so get their names at the talks and networking)
- **Sam Turner:** President of Warwick AI. Google SWE (Site Reliability) intern, summer 2026. PufferLib (RL library) contributor, RL drone controller and sim in C, Rust experiment tracker, zero-knowledge password manager, ML weather prediction plus a Raspberry Pi weather station. Likes systems code, RL and real sensors. He's an SRE, so he'll judge incident.io-style ideas closely. Talking point: your UltrasoundMark1 RL work.
- **Edward O'Dwyer:** Warwick CS, London-based. Low-latency, concurrency, backend. C++, Python, Java, Haskell. Chess engine framework, order book in 90 min (Java), crowd-flow sim, A=B esoteric language interpreter in Haskell, IC Algothon 2024. Likes fast, clever engineering, trading mechanics and simulations.
- **Jakub Kisielewski:** Warwick CS 2027. BGC Group SWE intern, summer 2026 (inter-dealer broker). Bio: low-latency + finance, and he explicitly likes "using hardware to solve problems". Exec on Warwick AI and Warwick Computing Society. Order-matching system, text editor in C, airport sim. Was at OxHack26 (opener). Most likely to reward an ESP32 build.
- **Louis Tanak:** co-founder/advisor. The HRT IC Hack 26 win is UNVERIFIED (couldn't re-confirm on 7 Nov).
- **Joshua Fitzmaurice:** advisor, nothing public found.
- **What they share:** Warwick CS with quant/low-latency internships; they've built matching engines and sims; hardware-friendly. They reward real tech under a fun surface, not ChatGPT wrappers.
